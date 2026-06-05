using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin,Approver")]
public class ApprovalsController : Controller
{
    private readonly IApprovalService _approvalService;
    private readonly IApprovalWorkflowRegistry _registry;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApprovalsController(IApprovalService approvalService, IApprovalWorkflowRegistry registry,
        UserManager<ApplicationUser> userManager)
    {
        _approvalService = approvalService;
        _registry = registry;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var steps = await _approvalService.GetPendingStepsForApproverAsync(user!.Id);

        // Build display labels for each step using the registered workflows
        var labels = new Dictionary<(string, int), string>();
        foreach (var step in steps)
        {
            var key = (step.EntityType, step.EntityId);
            if (!labels.ContainsKey(key))
            {
                try
                {
                    var workflow = _registry.Resolve(step.EntityType);
                    labels[key] = await workflow.GetDisplayLabelAsync(step.EntityId, HttpContext.RequestServices);
                }
                catch
                {
                    labels[key] = $"{step.EntityType} #{step.EntityId}";
                }
            }
        }

        ViewBag.Labels = labels;
        return View(steps);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int stepId, string entityType, int entityId, string? comment)
    {
        var user = await _userManager.GetUserAsync(User);
        var outcome = await _approvalService.ApproveStepAsync(stepId, user!.Id, comment);

        var workflow = _registry.Resolve(entityType);
        await workflow.OnStepApprovedAsync(entityId, outcome, user.Id, HttpContext.RequestServices);

        TempData["Success"] = outcome == ApprovalOutcome.AllApproved
            ? "All steps approved — entity has been approved."
            : "Step approved.";

        return RedirectToAction(workflow.RedirectAction, workflow.RedirectController, new { id = entityId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int stepId, string entityType, int entityId, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            TempData["Error"] = "A rejection comment is required.";
            var wf = _registry.Resolve(entityType);
            return RedirectToAction(wf.RedirectAction, wf.RedirectController, new { id = entityId });
        }

        var user = await _userManager.GetUserAsync(User);
        var outcome = await _approvalService.RejectStepAsync(stepId, user!.Id, comment);

        var workflow = _registry.Resolve(entityType);
        await workflow.OnStepRejectedAsync(entityId, outcome, user.Id, HttpContext.RequestServices);

        TempData["Success"] = "Step rejected.";
        return RedirectToAction(workflow.RedirectAction, workflow.RedirectController, new { id = entityId });
    }
}
