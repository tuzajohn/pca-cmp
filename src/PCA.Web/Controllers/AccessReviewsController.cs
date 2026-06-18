using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.AccessManagement.Models;
using PCA.Modules.AccessManagement.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize]
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
