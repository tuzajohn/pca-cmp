using Microsoft.EntityFrameworkCore;
using PCA.Modules.Approvals.Models;
using PCA.Shared.Enums;



namespace PCA.Modules.Approvals.Services;

public interface IApplicationDbContextForApprovals
{
    DbSet<ApprovalTemplate> ApprovalTemplates { get; }
    DbSet<ApprovalTemplateStep> ApprovalTemplateSteps { get; }
    DbSet<ApprovalStep> ApprovalSteps { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public class ApprovalService : IApprovalService
{
    private readonly IApplicationDbContextForApprovals _db;

    public ApprovalService(IApplicationDbContextForApprovals db)
    {
        _db = db;
    }

    public async Task<List<ApprovalTemplate>> GetTemplatesAsync()
    {
        return await _db.ApprovalTemplates
            .Include(t => t.Steps).ThenInclude(s => s.Approver)
            .ToListAsync();
    }

    public async Task<ApprovalTemplate?> GetTemplateForEntityAsync(string entityType, string? entitySubType)
    {
        return await _db.ApprovalTemplates
            .Include(t => t.Steps).ThenInclude(s => s.Approver)
            .FirstOrDefaultAsync(t =>
                t.EntityType == entityType &&
                t.EntitySubType == entitySubType);
    }

    public async Task<ApprovalTemplate> CreateTemplateAsync(ApprovalTemplate template)
    {
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;
        _db.ApprovalTemplates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    public async Task<ApprovalTemplate> UpdateTemplateAsync(ApprovalTemplate template)
    {
        var oldSteps = await _db.ApprovalTemplateSteps
            .Where(s => s.TemplateId == template.Id)
            .ToListAsync();
        foreach (var s in oldSteps) _db.ApprovalTemplateSteps.Remove(s);
        template.UpdatedAt = DateTime.UtcNow;
        _db.ApprovalTemplates.Update(template);
        await _db.SaveChangesAsync();
        return template;
    }

    public async Task InitiateApprovalFlowAsync(string entityType, int entityId, string? entitySubType)
    {
        var existing = await _db.ApprovalSteps
            .Where(s => s.EntityType == entityType && s.EntityId == entityId)
            .ToListAsync();
        foreach (var s in existing) _db.ApprovalSteps.Remove(s);

        var template = await GetTemplateForEntityAsync(entityType, entitySubType);
        if (template == null) return;

        foreach (var ts in template.Steps.OrderBy(s => s.Order))
        {
            _db.ApprovalSteps.Add(new ApprovalStep
            {
                EntityType = entityType,
                EntityId = entityId,
                Order = ts.Order,
                ApproverId = ts.ApproverId,
                Status = ApprovalStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<ApprovalTemplate>> GetAutoTriggerTemplatesAsync(AutoTriggerOn trigger, string entityType)
    {
        return await _db.ApprovalTemplates
            .Include(t => t.Steps).ThenInclude(s => s.Approver)
            .Where(t => t.AutoTriggerOn == trigger && t.EntityType == entityType)
            .ToListAsync();
    }

    public async Task<List<ApprovalStep>> GetStepsForEntityAsync(string entityType, int entityId)
    {
        return await _db.ApprovalSteps
            .Include(s => s.Approver)
            .Where(s => s.EntityType == entityType && s.EntityId == entityId)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<List<ApprovalStep>> GetPendingStepsForApproverAsync(string approverId)
    {
        return await _db.ApprovalSteps
            .Where(s => s.ApproverId == approverId && s.Status == ApprovalStatus.Pending)
            .OrderBy(s => s.EntityType).ThenBy(s => s.EntityId).ThenBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<ApprovalOutcome> ApproveStepAsync(int stepId, string approverId, string? comment)
    {
        var step = await _db.ApprovalSteps.FindAsync(stepId);
        if (step == null || step.ApproverId != approverId || step.Status != ApprovalStatus.Pending)
            return ApprovalOutcome.StillPending;

        step.Status = ApprovalStatus.Approved;
        step.Comment = comment;
        step.ActedAt = DateTime.UtcNow;
        step.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await EvaluateOutcomeAsync(step.EntityType, step.EntityId);
    }

    public async Task<ApprovalOutcome> RejectStepAsync(int stepId, string approverId, string comment)
    {
        var step = await _db.ApprovalSteps.FindAsync(stepId);
        if (step == null || step.ApproverId != approverId || step.Status != ApprovalStatus.Pending)
            return ApprovalOutcome.StillPending;

        step.Status = ApprovalStatus.Rejected;
        step.Comment = comment;
        step.ActedAt = DateTime.UtcNow;
        step.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await EvaluateOutcomeAsync(step.EntityType, step.EntityId);
    }

    private async Task<ApprovalOutcome> EvaluateOutcomeAsync(string entityType, int entityId)
    {
        var steps = await _db.ApprovalSteps
            .Where(s => s.EntityType == entityType && s.EntityId == entityId)
            .ToListAsync();

        if (!steps.Any()) return ApprovalOutcome.StillPending;
        if (steps.Any(s => s.Status == ApprovalStatus.Rejected)) return ApprovalOutcome.AnyRejected;

        // Resolve approval mode from template
        var template = await _db.ApprovalTemplates
            .FirstOrDefaultAsync(t => t.EntityType == entityType);
        var mode = template?.ApprovalMode ?? ApprovalMode.AllMustApprove;

        if (mode == ApprovalMode.AnyCanApprove && steps.Any(s => s.Status == ApprovalStatus.Approved))
            return ApprovalOutcome.AllApproved;

        if (steps.All(s => s.Status == ApprovalStatus.Approved)) return ApprovalOutcome.AllApproved;
        return ApprovalOutcome.StillPending;
    }
}
