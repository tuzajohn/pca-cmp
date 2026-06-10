using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;
using PCA.Web.Services;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin,Approver")]
public class ApprovalsController : Controller
{
    private readonly IApprovalService _approvalService;
    private readonly IApprovalWorkflowRegistry _registry;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<ApprovalsController> _logger;

    public ApprovalsController(IApprovalService approvalService, IApprovalWorkflowRegistry registry,
        UserManager<ApplicationUser> userManager, IEmailService emailService, ILogger<ApprovalsController> logger)
    {
        _approvalService = approvalService;
        _registry = registry;
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
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

        // Send notifications
        try
        {
            var entityLabel = await workflow.GetDisplayLabelAsync(entityId, HttpContext.RequestServices);

            if (outcome == ApprovalOutcome.AllApproved)
            {
                // Notify submitter that all approvals are complete
                var flow = await _approvalService.GetActiveFlowAsync(entityType, entityId);
                if (flow?.InitiatedBy != null && !string.IsNullOrEmpty(flow.InitiatedBy.Email))
                {
                    await _emailService.SendApprovalCompletedAsync(
                        flow.InitiatedBy.Email,
                        flow.InitiatedBy.FullName,
                        entityLabel
                    );
                }
            }
            else if (outcome == ApprovalOutcome.StillPending)
            {
                // Notify next approver
                var nextStep = await _approvalService.GetNextPendingStepAsync(entityType, entityId);
                if (nextStep?.Approver != null && !string.IsNullOrEmpty(nextStep.Approver.Email))
                {
                    var viewLink = Url.Action(workflow.RedirectAction, workflow.RedirectController, 
                        new { id = entityId }, Request.Scheme);

                    await _emailService.SendApprovalRequestAsync(
                        nextStep.Approver.Email,
                        nextStep.Approver.FullName,
                        entityLabel,
                        nextStep.RoleName ?? "Approver",
                        viewLink ?? ""
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send approval notification for {EntityType} {EntityId}", entityType, entityId);
        }

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

        // Send notification to submitter
        try
        {
            var flow = await _approvalService.GetActiveFlowAsync(entityType, entityId);
            if (flow?.InitiatedBy != null && !string.IsNullOrEmpty(flow.InitiatedBy.Email))
            {
                var entityLabel = await workflow.GetDisplayLabelAsync(entityId, HttpContext.RequestServices);

                await _emailService.SendApprovalRejectedAsync(
                    flow.InitiatedBy.Email,
                    flow.InitiatedBy.FullName,
                    entityLabel,
                    user!.FullName,
                    comment
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send rejection notification for {EntityType} {EntityId}", entityType, entityId);
        }

        TempData["Error"] = "Step rejected.";
        return RedirectToAction(workflow.RedirectAction, workflow.RedirectController, new { id = entityId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnForEdit(int stepId, string entityType, int entityId, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            TempData["Error"] = "A comment explaining what needs to be corrected is required.";
            var wf = _registry.Resolve(entityType);
            return RedirectToAction(wf.RedirectAction, wf.RedirectController, new { id = entityId });
        }

        var user = await _userManager.GetUserAsync(User);
        await _approvalService.ReturnStepAsync(stepId, user!.Id, comment);

        var workflow = _registry.Resolve(entityType);
        await workflow.OnStepReturnedAsync(entityId, user.Id, comment, HttpContext.RequestServices);

        // Send notification to submitter
        try
        {
            var flow = await _approvalService.GetActiveFlowAsync(entityType, entityId);
            if (flow?.InitiatedBy != null && !string.IsNullOrEmpty(flow.InitiatedBy.Email))
            {
                var entityLabel = await workflow.GetDisplayLabelAsync(entityId, HttpContext.RequestServices);

                await _emailService.SendApprovalReturnedAsync(
                    flow.InitiatedBy.Email,
                    flow.InitiatedBy.FullName,
                    entityLabel,
                    user!.FullName,
                    comment
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send return notification for {EntityType} {EntityId}", entityType, entityId);
        }

        TempData["Warning"] = "Returned for edit. The submitter has been notified.";
        return RedirectToAction(workflow.RedirectAction, workflow.RedirectController, new { id = entityId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendReminder(string entityType, int entityId)
    {
        try
        {
            var nextStep = await _approvalService.GetNextPendingStepAsync(entityType, entityId);
            if (nextStep?.Approver != null && !string.IsNullOrEmpty(nextStep.Approver.Email))
            {
                var workflow = _registry.Resolve(entityType);
                var entityLabel = await workflow.GetDisplayLabelAsync(entityId, HttpContext.RequestServices);
                var viewLink = Url.Action(workflow.RedirectAction, workflow.RedirectController, 
                    new { id = entityId }, Request.Scheme);

                await _emailService.SendApprovalReminderAsync(
                    nextStep.Approver.Email,
                    nextStep.Approver.FullName,
                    entityLabel,
                    nextStep.RoleName ?? "Approver",
                    viewLink ?? ""
                );

                TempData["Success"] = $"Reminder sent to {nextStep.Approver.FullName}.";
            }
            else
            {
                TempData["Error"] = "No pending approver found.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reminder for {EntityType} {EntityId}", entityType, entityId);
            TempData["Error"] = "Failed to send reminder.";
        }

        var wf = _registry.Resolve(entityType);
        return RedirectToAction(wf.RedirectAction, wf.RedirectController, new { id = entityId });
    }
}
