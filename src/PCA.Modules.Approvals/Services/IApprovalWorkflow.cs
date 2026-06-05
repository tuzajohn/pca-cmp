using PCA.Shared.Enums;

namespace PCA.Modules.Approvals.Services;

public interface IApprovalWorkflow
{
    string EntityType { get; }
    string RedirectController { get; }
    string RedirectAction { get; }

    Task<string?> GetEntitySubTypeAsync(int entityId, IServiceProvider sp);
    Task<string> GetDisplayLabelAsync(int entityId, IServiceProvider sp);

    Task OnFlowInitiatedAsync(int entityId, string initiatedById, IServiceProvider sp);
    Task OnStepApprovedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp);
    Task OnStepRejectedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp);
}
