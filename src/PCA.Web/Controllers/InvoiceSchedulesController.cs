using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PCA.Modules.Identity.Models;
using PCA.Modules.Invoicing.Models;
using PCA.Modules.Invoicing.Services;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize(Policy = "Module:Invoicing")]
public class InvoiceSchedulesController : Controller
{
    private readonly IInvoicingService _svc;
    private readonly UserManager<ApplicationUser> _users;
    private readonly InvoiceRunOrchestrator _orchestrator;
    private readonly string _storageRoot;

    public InvoiceSchedulesController(
        IInvoicingService svc,
        UserManager<ApplicationUser> users,
        InvoiceRunOrchestrator orchestrator,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _svc = svc;
        _users = users;
        _orchestrator = orchestrator;
        _storageRoot = config["InvoiceStoragePath"]
            ?? Path.Combine(env.ContentRootPath, "uploads", "documents");
    }

    public async Task<IActionResult> Index() => View(await _svc.GetSchedulesAsync());

    public async Task<IActionResult> Create()
    {
        var recipients = await _svc.GetRecipientsAsync();
        ViewBag.Lenders    = await _svc.GetLendersAsync();
        ViewBag.Recipients = recipients;
        return View(new InvoiceScheduleCreateViewModel
        {
            SelectedRecipientIds = recipients.Where(r => r.IsDefault).Select(r => r.Id).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceScheduleCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Lenders    = await _svc.GetLendersAsync();
            ViewBag.Recipients = await _svc.GetRecipientsAsync();
            return View(vm);
        }

        var user = await _users.GetUserAsync(User);
        var schedule = new InvoiceSchedule
        {
            Name        = vm.Name,
            LenderId    = vm.LenderId,
            Frequency   = vm.Frequency,
            DayOfWeek   = vm.DayOfWeek,
            DayOfMonth  = vm.DayOfMonth,
            TimeOfDay   = vm.TimeOfDay,
            IsEnabled   = vm.IsEnabled,
            SplitSheets = vm.SplitSheets,
            CreatedById = user?.Id
        };
        schedule.NextRunAt = ScheduleCronHelper.NextOccurrence(schedule);

        await _svc.CreateScheduleAsync(schedule, vm.SelectedRecipientIds);
        TempData["Success"] = "Schedule created.";
        return RedirectToAction(nameof(Details), new { id = schedule.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var schedule = await _svc.GetScheduleByIdAsync(id);
        if (schedule == null) return NotFound();
        ViewBag.Description  = ScheduleCronHelper.Describe(schedule);
        ViewBag.HcmRefFiles  = await _svc.GetHcmRefFilesAsync(id);
        return View(schedule);
    }

    [HttpGet]
    public async Task<IActionResult> RunsData(int id, int page = 1, int pageSize = 20)
    {
        var result = await _svc.GetRunsPagedAsync(id, page, pageSize);

        return Json(new {
            items = result.Collection.Select(r => new {
                runId       = r.Id,
                triggeredAt = r.TriggeredAt.ToString("dd MMM yyyy HH:mm"),
                triggeredBy = r.TriggeredBy?.FullName ?? "Scheduler",
                ippsRows    = r.IppsRowCount,
                hcmRows     = r.HcmRowCount,
                finalRows   = r.FinalRowCount,
                status      = r.Status.ToString(),
                hasFile     = r.Status == InvoiceRunStatus.Completed && !string.IsNullOrEmpty(r.FilePath)
            }),
            totalCount  = result.TotalCount,
            currentPage = result.CurrentPage,
            totalPages = result.TotalPages
        });
    }

    [HttpGet]
    public async Task<IActionResult> IndexData(int page = 1, int pageSize = 20, string? sortCol = null, string? sortDir = "asc")
    {
        var all = await _svc.GetSchedulesAsync();
        var sorted = sortCol switch {
            "name"      => sortDir == "asc" ? all.OrderBy(s => s.Name).ToList() : all.OrderByDescending(s => s.Name).ToList(),
            "lender"    => sortDir == "asc" ? all.OrderBy(s => s.Lender?.Name).ToList() : all.OrderByDescending(s => s.Lender?.Name).ToList(),
            "nextRunAt" => sortDir == "asc" ? all.OrderBy(s => s.NextRunAt).ToList() : all.OrderByDescending(s => s.NextRunAt).ToList(),
            _           => all.OrderBy(s => s.Name).ToList()
        };
        var totalCount = sorted.Count;
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        int totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

        return Json(new {
            items = items.Select(s => new {
                id          = s.Id,
                name        = s.Name,
                lender      = s.Lender?.Name ?? "",
                companyType = s.Lender?.CompanyType ?? "",
                frequency   = ScheduleCronHelper.Describe(s),
                isEnabled   = s.IsEnabled,
                nextRunAt   = s.NextRunAt.HasValue ? s.NextRunAt.Value.ToString("dd MMM yyyy HH:mm") : "",
                lastRunAt   = s.LastRunAt.HasValue ? s.LastRunAt.Value.ToString("dd MMM yyyy HH:mm") : ""
            }),
            totalCount,
            currentPage = page,
            totalPages = totalPages
        });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var s = await _svc.GetScheduleByIdAsync(id);
        if (s == null) return NotFound();
        ViewBag.Lenders    = await _svc.GetLendersAsync();
        ViewBag.Recipients = await _svc.GetRecipientsAsync();
        return View(new InvoiceScheduleEditViewModel
        {
            Id                   = s.Id,
            Name                 = s.Name,
            LenderId             = s.LenderId,
            Frequency            = s.Frequency,
            DayOfWeek            = s.DayOfWeek,
            DayOfMonth           = s.DayOfMonth,
            TimeOfDay            = s.TimeOfDay,
            IsEnabled            = s.IsEnabled,
            SplitSheets          = s.SplitSheets,
            SelectedRecipientIds = s.ScheduleRecipients.Select(sr => sr.InvoiceRecipientId).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InvoiceScheduleEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Lenders    = await _svc.GetLendersAsync();
            ViewBag.Recipients = await _svc.GetRecipientsAsync();
            return View(vm);
        }

        var s = await _svc.GetScheduleByIdAsync(vm.Id);
        if (s == null) return NotFound();
        s.Name        = vm.Name;
        s.LenderId    = vm.LenderId;
        s.Frequency   = vm.Frequency;
        s.DayOfWeek   = vm.DayOfWeek;
        s.DayOfMonth  = vm.DayOfMonth;
        s.TimeOfDay   = vm.TimeOfDay;
        s.IsEnabled   = vm.IsEnabled;
        s.SplitSheets = vm.SplitSheets;
        s.NextRunAt   = ScheduleCronHelper.NextOccurrence(s);

        await _svc.UpdateScheduleAsync(s, vm.SelectedRecipientIds);
        TempData["Success"] = "Schedule updated.";
        return RedirectToAction(nameof(Details), new { id = s.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteScheduleAsync(id);
        TempData["Success"] = "Schedule deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(int id)
    {
        var schedule = await _svc.GetScheduleByIdAsync(id);
        if (schedule == null) return NotFound();

        var user = await _users.GetUserAsync(User);
        await _orchestrator.ExecuteAsync(schedule, user?.Id, HttpContext.RequestAborted);

        TempData["Success"] = "Invoice run triggered.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadHcmRef(int id, IFormFile refFile, string monthYear)
    {
        var schedule = await _svc.GetScheduleByIdAsync(id);
        if (schedule == null) return NotFound();

        if (refFile == null || refFile.Length == 0)
        {
            TempData["Error"] = "Please select an Excel file to upload.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var dir = Path.Combine(_storageRoot, "invoices", "hcm-ref");
        Directory.CreateDirectory(dir);
        var safeMonth = monthYear.Replace("/", "-").Replace("\\", "-");
        var fileName  = $"hcm_ref_{id}_{safeMonth}.xlsx";
        var filePath  = Path.Combine(dir, fileName);

        using (var fs = System.IO.File.Create(filePath))
            await refFile.CopyToAsync(fs);

        var user = await _users.GetUserAsync(User);
        await _svc.SaveHcmRefFileAsync(new InvoiceHcmRefFile
        {
            ScheduleId       = id,
            MonthYear        = safeMonth,
            FilePath         = filePath,
            OriginalFileName = refFile.FileName,
            UploadedAt       = DateTime.UtcNow,
            UploadedById     = user?.Id
        });

        TempData["Success"] = $"HCM ref file uploaded for {safeMonth}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHcmRef(int id, int refFileId)
    {
        var files = await _svc.GetHcmRefFilesAsync(id);
        var refFile = files.FirstOrDefault(f => f.Id == refFileId);
        if (refFile != null && System.IO.File.Exists(refFile.FilePath))
            System.IO.File.Delete(refFile.FilePath);

        await _svc.DeleteHcmRefFileAsync(refFileId);
        TempData["Success"] = "HCM ref file deleted.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
