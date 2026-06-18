using Microsoft.Extensions.DependencyInjection;
using PCA.Modules.AccessManagement.Services;
using PCA.Modules.Approvals.Services;
using PCA.Shared.Enums;

namespace PCA.Web.Workflows;

public class ServerRoomAccessApprovalWorkflow : IApprovalWorkflow
{
    public string EntityType => "ServerRoomAccessRequest";
    public string RedirectController => "ServerRoomAccess";
    public string RedirectAction => "Details";

    public async Task<string?> GetEntitySubTypeAsync(int entityId, IServiceProvider sp)
    {
        await Task.CompletedTask;
        return null;
    }

    public async Task<string> GetDisplayLabelAsync(int entityId, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IAccessManagementService>();
        var req = await svc.GetServerRoomRequestByIdAsync(entityId);
        return req != null ? $"{req.SerialNumber} — {req.VisitorName}" : $"SRA #{entityId}";
    }

    public async Task OnFlowInitiatedAsync(int entityId, string initiatedById, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IAccessManagementService>();
        await svc.UpdateServerRoomStatusAsync(entityId, ServerRoomAccessStatus.UnderReview, initiatedById);
    }

    public async Task OnStepApprovedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp)
    {
        if (outcome == ApprovalOutcome.AllApproved)
        {
            var svc = sp.GetRequiredService<IAccessManagementService>();
            await svc.UpdateServerRoomStatusAsync(entityId, ServerRoomAccessStatus.Approved, actorId);
        }
    }

    public async Task OnStepRejectedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp)
    {
        if (outcome == ApprovalOutcome.AnyRejected)
        {
            var svc = sp.GetRequiredService<IAccessManagementService>();
            await svc.UpdateServerRoomStatusAsync(entityId, ServerRoomAccessStatus.Rejected, actorId);
        }
    }

    public async Task OnStepReturnedAsync(int entityId, string actorId, string comment, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IAccessManagementService>();
        await svc.UpdateServerRoomStatusAsync(entityId, ServerRoomAccessStatus.Draft, actorId);
    }
}
