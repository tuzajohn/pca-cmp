using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.AccessManagement.Models;
using PCA.Modules.AccessManagement.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize(Policy = "Module:AccessManagement")]
public class AccessReviewsController : Controller
{
    private readonly IAccessManagementService _svc;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccessReviewsController(IAccessManagementService svc, UserManager<ApplicationUser> userManager)
    {
        _svc = svc;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var reviews = await _svc.GetAllAccessReviewsAsync();
        return View(reviews);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create() => View(new AccessReviewCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(AccessReviewCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _userManager.GetUserAsync(User);
        var review = new AccessReview
        {
            Title = vm.Title,
            Cycle = vm.Cycle,
            Year = vm.Year,
            Quarter = vm.Quarter,
            ReviewPeriodStart = vm.ReviewPeriodStart,
            ReviewPeriodEnd = vm.ReviewPeriodEnd,
            DueDate = vm.DueDate,
            Notes = vm.Notes,
            CreatedById = user!.Id,
            Entries = vm.Entries.Select(e => new AccessReviewEntry
            {
                EmployeeName = e.EmployeeName,
                Department = e.Department,
                SystemName = e.SystemName,
                CurrentAccessLevel = e.CurrentAccessLevel,
                Outcome = AccessReviewEntryOutcome.Pending
            }).ToList()
        };

        await _svc.CreateAccessReviewAsync(review);
        TempData["Success"] = "Access review campaign created.";
        return RedirectToAction(nameof(Details), new { id = review.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var review = await _svc.GetAccessReviewByIdAsync(id);
        if (review == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        ViewBag.CurrentUserId = user?.Id;
        return View(review);
    }

    [HttpGet]
    public async Task<IActionResult> EntriesData(
        int id, int page = 1, int pageSize = 20,
        string? sortCol = null, string? sortDir = "asc")
    {
        var result = await _svc.GetEntriesPagedAsync(id, page, pageSize, sortCol, sortDir);

        return Json(new {
            items = result.Collection.Select(e => new {
                entryId       = e.Id,
                employeeName  = e.EmployeeName,
                department    = e.Department ?? "",
                systemName    = e.SystemName,
                currentAccess = e.CurrentAccessLevel ?? "",
                outcome       = e.Outcome.ToString(),
                reviewedBy    = e.ReviewedBy?.FullName ?? "",
                notes         = e.ReviewerNotes ?? ""
            }),
            totalCount  = result.TotalCount,
            currentPage = result.CurrentPage,
            totalPages = result.TotalPages
        });
    }

    [HttpGet]
    public async Task<IActionResult> IndexData(int page = 1, int pageSize = 20, string? sortCol = null, string? sortDir = "desc")
    {
        var all = await _svc.GetAllAccessReviewsAsync();
        var sorted = sortCol switch {
            "title"   => sortDir == "asc" ? all.OrderBy(r => r.Title).ToList() : all.OrderByDescending(r => r.Title).ToList(),
            "cycle"   => sortDir == "asc" ? all.OrderBy(r => r.Cycle).ToList() : all.OrderByDescending(r => r.Cycle).ToList(),
            "dueDate" => sortDir == "asc" ? all.OrderBy(r => r.DueDate).ToList() : all.OrderByDescending(r => r.DueDate).ToList(),
            "status"  => sortDir == "asc" ? all.OrderBy(r => r.Status).ToList() : all.OrderByDescending(r => r.Status).ToList(),
            _         => all.OrderByDescending(r => r.DueDate).ToList()
        };
        var totalCount = sorted.Count;
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        int totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

        return Json(new {
            items = items.Select(r => {
                var total = r.Entries.Count;
                var done  = r.Entries.Count(e => e.Outcome != AccessReviewEntryOutcome.Pending);
                var isOverdue = r.Status != AccessReviewStatus.Completed && r.DueDate < DateTime.UtcNow;
                return new {
                    id        = r.Id,
                    title     = r.Title,
                    cycle     = r.Cycle.ToString(),
                    period    = $"{r.ReviewPeriodStart:dd MMM} – {r.ReviewPeriodEnd:dd MMM yyyy}",
                    dueDate   = r.DueDate.ToString("dd MMM yyyy"),
                    isOverdue,
                    status    = isOverdue && r.Status != AccessReviewStatus.Completed ? "Overdue" : r.Status.ToString(),
                    entryCount = total,
                    doneCount  = done,
                    pct        = total > 0 ? done * 100 / total : 0
                };
            }),
            totalCount,
            currentPage = page,
            totalPages = totalPages
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEntry(int id, int entryId, string outcome, string? notes)
    {
        if (!Enum.TryParse<AccessReviewEntryOutcome>(outcome, out var o))
        {
            TempData["Error"] = "Invalid outcome.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var user = await _userManager.GetUserAsync(User);
        await _svc.UpdateAccessReviewEntryAsync(entryId, o, user!.Id, notes);
        TempData["Success"] = "Entry updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Complete(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var success = await _svc.CompleteAccessReviewAsync(id, user!.Id);
        TempData[success ? "Success" : "Error"] = success
            ? "Access review marked as completed."
            : "Unable to complete review.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
