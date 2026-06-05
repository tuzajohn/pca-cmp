using Microsoft.Extensions.DependencyInjection;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Documents.Services;
using PCA.Shared.Enums;

namespace PCA.Web.Workflows;

public class DocumentApprovalWorkflow : IApprovalWorkflow
{
    public string EntityType => "Document";
    public string RedirectController => "Documents";
    public string RedirectAction => "Details";

    public Task<string?> GetEntitySubTypeAsync(int entityId, IServiceProvider sp)
        => Task.FromResult<string?>(null); // documents don't have subtypes for approval

    public async Task<string> GetDisplayLabelAsync(int entityId, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IDocumentService>();
        var doc = await svc.GetByIdAsync(entityId);
        return doc != null ? $"{doc.SerialNumber} — {doc.Title}" : $"Document #{entityId}";
    }

    public async Task OnFlowInitiatedAsync(int entityId, string initiatedById, IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IDocumentService>();
        await svc.UpdateStatusAsync(entityId, DocumentStatus.UnderReview);
    }

    public async Task OnStepApprovedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp)
    {
        if (outcome == ApprovalOutcome.AllApproved)
        {
            var svc = sp.GetRequiredService<IDocumentService>();
            await svc.UpdateStatusAsync(entityId, DocumentStatus.Active);
            await svc.MarkReviewedAsync(entityId, actorId);
        }
    }

    public async Task OnStepRejectedAsync(int entityId, ApprovalOutcome outcome, string actorId, IServiceProvider sp)
    {
        if (outcome == ApprovalOutcome.AnyRejected)
        {
            var svc = sp.GetRequiredService<IDocumentService>();
            await svc.UpdateStatusAsync(entityId, DocumentStatus.Draft);
        }
    }
}
