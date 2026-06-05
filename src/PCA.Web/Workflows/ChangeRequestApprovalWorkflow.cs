using Microsoft.Extensions.DependencyInjection;
using PCA.Modules.Approvals.Services;
using PCA.Modules.ChangeManagement.Services;
using PCA.Shared.Enums;

namespace PCA.Web.Workflows;

public class ChangeRequestApprovalWorkflow : IApprovalWorkflow
{
    public string EntityType => "ChangeRequest";
    public string RedirectController => "ChangeRequests";
    public string RedirectAction => "Details";

    public async Task<string?> GetEntitySubTypeAsync(int entityId, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IChangeRequestService>();
        var cr = await svc.GetByIdAsync(entityId);
        return cr?.Type.ToString();
    }

    public async Task<string> GetDisplayLabelAsync(int entityId, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IChangeRequestService>();
        var cr = await svc.GetByIdAsync(entityId);
        return cr != null ? $"{cr.SerialNumber} — {cr.Title}" : $"CR #{entityId}";
    }

    public async Task OnFlowInitiatedAsync(int entityId, string initiatedById, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IChangeRequestService>();
        await svc.UpdateStatusAsync(entityId, ChangeStatus.UnderReview, initiatedById);
    }

    public async Task OnStepApprovedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp)
    {
        if (outcome == ApprovalOutcome.AllApproved)
        {
            var svc = sp.GetRequiredService<IChangeRequestService>();
            await svc.UpdateStatusAsync(entityId, ChangeStatus.Approved, actorId);
        }
    }

    public async Task OnStepRejectedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp)
    {
        if (outcome == ApprovalOutcome.AnyRejected)
        {
            var svc = sp.GetRequiredService<IChangeRequestService>();
            await svc.UpdateStatusAsync(entityId, ChangeStatus.Rejected, actorId);
        }
    }

    public async Task OnStepReturnedAsync(int entityId, string actorId, string comment, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IChangeRequestService>();
        await svc.UpdateStatusAsync(entityId, ChangeStatus.Draft, actorId);
    }
}
