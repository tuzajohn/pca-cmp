using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Approvals.Services;
using PCA.Modules.ChangeManagement.Services;
using PCA.Modules.Identity.Models;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin,Approver")]
public class ApprovalsController : Controller
{
    private readonly IApprovalService _approvalService;
    private readonly IChangeRequestService _crService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApprovalsController(IApprovalService approvalService, IChangeRequestService crService, UserManager<ApplicationUser> userManager)
    {
        _approvalService = approvalService;
        _crService = crService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var steps = await _approvalService.GetPendingStepsForApproverAsync(user!.Id);
        var crIds = steps.Select(s => s.ChangeRequestId).Distinct().ToList();
        var crs = new List<PCA.Modules.ChangeManagement.Models.ChangeRequest>();
        foreach (var crId in crIds)
        {
            var cr = await _crService.GetByIdAsync(crId);
            if (cr != null) crs.Add(cr);
        }
        ViewBag.PendingSteps = steps;
        return View(crs);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int stepId, int changeRequestId, string? comment)
    {
        var user = await _userManager.GetUserAsync(User);
        var success = await _approvalService.ApproveStepAsync(stepId, user!.Id, comment);
        if (success)
            TempData["Success"] = "Step approved successfully.";
        else
            TempData["Error"] = "Unable to approve step.";
        return RedirectToAction("Details", "ChangeRequests", new { id = changeRequestId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int stepId, int changeRequestId, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            TempData["Error"] = "A rejection comment is required.";
            return RedirectToAction("Details", "ChangeRequests", new { id = changeRequestId });
        }
        var user = await _userManager.GetUserAsync(User);
        var success = await _approvalService.RejectStepAsync(stepId, user!.Id, comment);
        if (success)
            TempData["Success"] = "Step rejected.";
        else
            TempData["Error"] = "Unable to reject step.";
        return RedirectToAction("Details", "ChangeRequests", new { id = changeRequestId });
    }
}
