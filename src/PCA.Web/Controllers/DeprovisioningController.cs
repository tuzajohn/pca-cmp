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
public class DeprovisioningController : Controller
{
    private readonly IAccessManagementService _svc;
    private readonly UserManager<ApplicationUser> _userManager;

    public DeprovisioningController(IAccessManagementService svc, UserManager<ApplicationUser> userManager)
    {
        _svc = svc;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? status, bool allTime = false)
    {
        var events = allTime
            ? await _svc.GetAllDeprovisioningEventsAsync()
            : await _svc.GetDeprovisioningEventsLast12MonthsAsync();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DeprovisioningStatus>(status, out var s))
            events = events.Where(x => x.Status == s).ToList();

        // Sync overdue status in-memory for display (actual DB update done by background worker)
        foreach (var e in events)
        {
            if (e.Status != DeprovisioningStatus.Completed && e.SlaDeadline < DateTime.UtcNow)
                e.Status = DeprovisioningStatus.Overdue;
        }

        ViewBag.StatusFilter = status;
        ViewBag.AllTime = allTime;
        ViewBag.OverdueCount = events.Count(e => e.Status == DeprovisioningStatus.Overdue);
        return View(events);
    }

    [Authorize(Roles = "Admin,Approver")]
    public IActionResult Create() => View(new DeprovisioningCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Approver")]
    public async Task<IActionResult> Create(DeprovisioningCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _userManager.GetUserAsync(User);
        var evt = new DeprovisioningEvent
        {
            EmployeeName = vm.EmployeeName,
            EmployeeId = vm.EmployeeId,
            Department = vm.Department,
            JobTitle = vm.JobTitle,
            Trigger = vm.Trigger,
            TriggerDetails = vm.TriggerDetails,
            HrNotificationReceivedAt = vm.HrNotificationReceivedAt,
            Notes = vm.Notes,
            NotifiedById = user!.Id,
            SystemEntries = vm.SystemEntries.Select(s => new DeprovisioningSystemEntry
            {
                SystemName = s.SystemName,
                AccessDescription = s.AccessDescription
            }).ToList()
        };

        await _svc.CreateDeprovisioningEventAsync(evt);
        TempData["Success"] = $"Deprovisioning event logged. SLA deadline: {evt.SlaDeadline:dd MMM yyyy HH:mm} UTC.";
        return RedirectToAction(nameof(Details), new { id = evt.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var evt = await _svc.GetDeprovisioningEventByIdAsync(id);
        if (evt == null) return NotFound();

        // Compute remaining time
        var remaining = evt.SlaDeadline - DateTime.UtcNow;
        ViewBag.SlaRemaining = remaining;
        ViewBag.SlaBreached = remaining.TotalSeconds <= 0;
        ViewBag.CurrentUserId = (await _userManager.GetUserAsync(User))?.Id;
        return View(evt);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Approver")]
    public async Task<IActionResult> UpdateSystemEntry(int id, int entryId, bool isDeactivated)
    {
        var user = await _userManager.GetUserAsync(User);
        await _svc.UpdateSystemEntryAsync(entryId, isDeactivated, user!.Id);
        TempData["Success"] = isDeactivated ? "System marked as deactivated." : "System deactivation reversed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Complete(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var success = await _svc.CompleteDeprovisioningAsync(id, user!.Id);
        TempData[success ? "Success" : "Error"] = success
            ? "Deprovisioning event marked as completed."
            : "Cannot complete — not all systems have been deactivated.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
