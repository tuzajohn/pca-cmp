using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Approvals.Services;
using PCA.Modules.ChangeManagement.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin,Approver")]
public class ApprovalsController : Controller
{
    private readonly IApprovalService _approvalService;
    private readonly IChangeRequestService _crService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApprovalsController(IApprovalService approvalService, IChangeRequestService crService,
        UserManager<ApplicationUser> userManager)
    {
        _approvalService = approvalService;
        _crService = crService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var steps = await _approvalService.GetPendingStepsForApproverAsync(user!.Id);

        var crIds = steps.Where(s => s.EntityType == "ChangeRequest")
                         .Select(s => s.EntityId).Distinct().ToList();
        var crs = new List<PCA.Modules.ChangeManagement.Models.ChangeRequest>();
        foreach (var crId in crIds)
        {
            var cr = await _crService.GetByIdAsync(crId);
            if (cr != null) crs.Add(cr);
        }

        ViewBag.PendingSteps = steps;
        ViewBag.ChangeRequests = crs;
        return View(steps);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int stepId, string entityType, int entityId, string? comment)
    {
        var user = await _userManager.GetUserAsync(User);
        var outcome = await _approvalService.ApproveStepAsync(stepId, user!.Id, comment);

        if (outcome != ApprovalOutcome.StillPending)
        {
            if (entityType == "ChangeRequest")
            {
                var status = outcome == ApprovalOutcome.AllApproved ? ChangeStatus.Approved : ChangeStatus.Rejected;
                await _crService.UpdateStatusAsync(entityId, status, user.Id);
            }
        }

        TempData["Success"] = "Step approved.";
        return RedirectToEntityDetails(entityType, entityId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int stepId, string entityType, int entityId, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            TempData["Error"] = "A rejection comment is required.";
            return RedirectToEntityDetails(entityType, entityId);
        }

        var user = await _userManager.GetUserAsync(User);
        var outcome = await _approvalService.RejectStepAsync(stepId, user!.Id, comment);

        if (outcome == ApprovalOutcome.AnyRejected && entityType == "ChangeRequest")
            await _crService.UpdateStatusAsync(entityId, ChangeStatus.Rejected, user.Id);

        TempData["Success"] = "Step rejected.";
        return RedirectToEntityDetails(entityType, entityId);
    }

    private IActionResult RedirectToEntityDetails(string entityType, int entityId)
    {
        return entityType switch
        {
            "Incident" => RedirectToAction("Details", "Incidents", new { id = entityId }),
            _ => RedirectToAction("Details", "ChangeRequests", new { id = entityId })
        };
    }
}
