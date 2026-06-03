using PCA.Modules.Approvals.Models;
using PCA.Shared.Enums;

namespace PCA.Modules.Approvals.Services;

public interface IApprovalService
{
    Task<List<ApprovalTemplate>> GetTemplatesAsync();
    Task<ApprovalTemplate?> GetTemplateForEntityAsync(string entityType, string? entitySubType);
    Task<ApprovalTemplate> CreateTemplateAsync(ApprovalTemplate template);
    Task<ApprovalTemplate> UpdateTemplateAsync(ApprovalTemplate template);

    Task InitiateApprovalFlowAsync(string entityType, int entityId, string? entitySubType);
    Task<List<ApprovalStep>> GetStepsForEntityAsync(string entityType, int entityId);
    Task<List<ApprovalStep>> GetPendingStepsForApproverAsync(string approverId);
    Task<ApprovalOutcome> ApproveStepAsync(int stepId, string approverId, string? comment);
    Task<ApprovalOutcome> RejectStepAsync(int stepId, string approverId, string comment);
}
