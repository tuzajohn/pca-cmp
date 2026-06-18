using Microsoft.Extensions.DependencyInjection;
using PCA.Modules.AccessManagement.Services;
using PCA.Modules.Approvals.Services;
using PCA.Shared.Enums;

namespace PCA.Web.Workflows;

public class AccessRequestApprovalWorkflow : IApprovalWorkflow
{
    public string EntityType => "AccessRequest";
    public string RedirectController => "AccessRequests";
    public string RedirectAction => "Details";

    public async Task<string?> GetEntitySubTypeAsync(int entityId, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IAccessManagementService>();
        var req = await svc.GetAccessRequestByIdAsync(entityId);
        return req?.IsPrivileged == true ? "Privileged" : "Standard";
    }

    public async Task<string> GetDisplayLabelAsync(int entityId, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IAccessManagementService>();
        var req = await svc.GetAccessRequestByIdAsync(entityId);
        return req != null ? $"{req.SerialNumber} — {req.SystemName} ({req.EmployeeName})" : $"AR #{entityId}";
    }

    public async Task OnFlowInitiatedAsync(int entityId, string initiatedById, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IAccessManagementService>();
        await svc.UpdateAccessRequestStatusAsync(entityId, AccessRequestStatus.UnderReview, initiatedById);
    }

    public async Task OnStepApprovedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp)
    {
        if (outcome == ApprovalOutcome.AllApproved)
        {
            var svc = sp.GetRequiredService<IAccessManagementService>();
            await svc.UpdateAccessRequestStatusAsync(entityId, AccessRequestStatus.Approved, actorId);
        }
    }

    public async Task OnStepRejectedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp)
    {
        if (outcome == ApprovalOutcome.AnyRejected)
        {
            var svc = sp.GetRequiredService<IAccessManagementService>();
            await svc.UpdateAccessRequestStatusAsync(entityId, AccessRequestStatus.Rejected, actorId);
        }
    }

    public async Task OnStepReturnedAsync(int entityId, string actorId, string comment, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IAccessManagementService>();
        await svc.UpdateAccessRequestStatusAsync(entityId, AccessRequestStatus.Draft, actorId);
    }
}
