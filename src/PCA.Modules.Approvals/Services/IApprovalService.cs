using PCA.Modules.Approvals.Models;
using PCA.Shared.Enums;

namespace PCA.Modules.Approvals.Services;

public interface IApprovalService
{
    Task<List<ApprovalTemplate>> GetTemplatesAsync();
    Task<ApprovalTemplate?> GetTemplateByTypeAsync(ChangeType changeType);
    Task<ApprovalTemplate> CreateTemplateAsync(ApprovalTemplate template);
    Task<ApprovalTemplate> UpdateTemplateAsync(ApprovalTemplate template);

    Task InitiateApprovalFlowAsync(int changeRequestId, ChangeType changeType);
    Task<List<ApprovalStep>> GetStepsForRequestAsync(int changeRequestId);
    Task<List<ApprovalStep>> GetPendingStepsForApproverAsync(string approverId);
    Task<bool> ApproveStepAsync(int stepId, string approverId, string? comment);
    Task<bool> RejectStepAsync(int stepId, string approverId, string comment);

    // Returns the new status after processing
    Task<ChangeStatus?> ProcessApprovalResultAsync(int changeRequestId);
}
