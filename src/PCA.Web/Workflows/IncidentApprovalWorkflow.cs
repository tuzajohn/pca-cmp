using Microsoft.Extensions.DependencyInjection;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Incidents.Services;
using PCA.Shared.Enums;

namespace PCA.Web.Workflows;

public class IncidentApprovalWorkflow : IApprovalWorkflow
{
    public string EntityType => "Incident";
    public string RedirectController => "Incidents";
    public string RedirectAction => "Details";

    public async Task<string?> GetEntitySubTypeAsync(int entityId, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IIncidentService>();
        var inc = await svc.GetByIdAsync(entityId);
        return inc?.Severity.ToString();
    }

    public async Task<string> GetDisplayLabelAsync(int entityId, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IIncidentService>();
        var inc = await svc.GetByIdAsync(entityId);
        return inc != null ? $"{inc.SerialNumber} — {inc.Title}" : $"Incident #{entityId}";
    }

    public async Task OnFlowInitiatedAsync(int entityId, string initiatedById, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IIncidentService>();
        await svc.UpdateStatusAsync(entityId, IncidentStatus.InProgress, initiatedById, "Approval flow initiated.");
    }

    public async Task OnStepApprovedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp)
    {
        // Fully approved — incident cleared to proceed, no status change needed beyond InProgress
    }

    public async Task OnStepRejectedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp)
    {
        if (outcome == ApprovalOutcome.AnyRejected)
        {
            var svc = sp.GetRequiredService<IIncidentService>();
            await svc.UpdateStatusAsync(entityId, IncidentStatus.Open, actorId, "Approval rejected — returned to Open.");
        }
    }

    public async Task OnStepReturnedAsync(int entityId, string actorId, string comment, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IIncidentService>();
        await svc.UpdateStatusAsync(entityId, IncidentStatus.Open, actorId, $"Returned for edit: {comment}");
    }
}
