using PCA.Modules.Approvals.Models;
using PCA.Shared.Enums;

namespace PCA.Modules.Approvals.Services;

public interface IApprovalService
{
    Task<List<ApprovalTemplate>> GetTemplatesAsync();
    Task<ApprovalTemplate?> GetTemplateForEntityAsync(string entityType, string? entitySubType);
    Task<ApprovalTemplate> CreateTemplateAsync(ApprovalTemplate template);
    Task<ApprovalTemplate> UpdateTemplateAsync(ApprovalTemplate template);

    Task InitiateApprovalFlowAsync(string entityType, int entityId, string? entitySubType, string? initiatedById = null);
    Task<List<ApprovalTemplate>> GetAutoTriggerTemplatesAsync(AutoTriggerOn trigger, string entityType);
    Task<ApprovalFlow?> GetActiveFlowAsync(string entityType, int entityId);
    Task<List<ApprovalStep>> GetStepsForEntityAsync(string entityType, int entityId);
    Task<List<ApprovalStep>> GetPendingStepsForApproverAsync(string approverId);
    Task<ApprovalStep?> GetNextPendingStepAsync(string entityType, int entityId);
    Task<ApprovalOutcome> ApproveStepAsync(int stepId, string approverId, string? comment);
    Task<ApprovalOutcome> RejectStepAsync(int stepId, string approverId, string comment);
    Task<ApprovalOutcome> ReturnStepAsync(int stepId, string approverId, string comment);
}
