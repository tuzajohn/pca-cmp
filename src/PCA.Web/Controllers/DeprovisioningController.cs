using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.AccessManagement.Models;
using PCA.Modules.AccessManagement.Services;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;
using PCA.Web.Models;
using PCA.Web.Services;

namespace PCA.Web.Controllers;

[Authorize(Policy = "Module:AccessManagement")]
public class DeprovisioningController : Controller
{
    private readonly IAccessManagementService _svc;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApprovalService _approvalService;
    private readonly IEmailService _email;

    public DeprovisioningController(IAccessManagementService svc, UserManager<ApplicationUser> userManager,
        IApprovalService approvalService, IEmailService email)
    {
        _svc             = svc;
        _userManager     = userManager;
        _approvalService = approvalService;
        _email           = email;
    }

    public async Task<IActionResult> Index(string? status, bool allTime = false, int page = 1, int pageSize = 25)
    {
        var result = await _svc.GetDeprovisioningPagedAsync(status, allTime, page, pageSize);

        ViewBag.StatusFilter = status;
        ViewBag.AllTime = allTime;
        ViewBag.OverdueCount = result.Items.Count(e => e.Status == DeprovisioningStatus.Overdue);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_DeprovisioningList", result);

        return View(result);
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

        // Notify all recipients configured in the Deprovisioning template
        var template = await _approvalService.GetTemplateForEntityAsync("Deprovisioning", null);
        if (template != null)
        {
            var viewLink = Url.Action(nameof(Details), "Deprovisioning", new { id = evt.Id }, Request.Scheme)!;
            foreach (var step in template.Steps)
            {
                var recipient = await _userManager.FindByIdAsync(step.ApproverId);
                if (recipient?.Email != null)
                {
                    try
                    {
                        await _email.SendDeprovisioningNoticeAsync(
                            recipient.Email, recipient.FullName,
                            evt.EmployeeName, evt.EmployeeId, evt.Department,
                            evt.Trigger.ToString(), evt.SlaDeadline, viewLink);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail the request if email delivery fails
                        HttpContext.RequestServices
                            .GetRequiredService<ILogger<DeprovisioningController>>()
                            .LogError(ex, "Failed to send deprovisioning notice to {Email}", recipient.Email);
                    }
                }
            }
        }

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
