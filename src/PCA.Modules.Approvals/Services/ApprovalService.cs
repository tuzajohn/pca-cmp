using Microsoft.EntityFrameworkCore;
using PCA.Modules.Approvals.Models;
using PCA.Shared.Enums;



namespace PCA.Modules.Approvals.Services;

public interface IApplicationDbContextForApprovals
{
    DbSet<ApprovalTemplate> ApprovalTemplates { get; }
    DbSet<ApprovalTemplateStep> ApprovalTemplateSteps { get; }
    DbSet<ApprovalFlow> ApprovalFlows { get; }
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

    public Task<ApprovalTemplate?> GetTemplateByIdAsync(int id) =>
        _db.ApprovalTemplates
            .Include(t => t.Steps).ThenInclude(s => s.Approver)
            .FirstOrDefaultAsync(t => t.Id == id);

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

    public async Task InitiateApprovalFlowAsync(string entityType, int entityId, string? entitySubType, string? initiatedById = null)
    {
        // Cancel any in-progress flow for this entity
        var existing = await _db.ApprovalFlows
            .Where(f => f.EntityType == entityType && f.EntityId == entityId && f.Status == FlowStatus.InProgress)
            .ToListAsync();
        foreach (var f in existing)
        {
            f.Status   = FlowStatus.Cancelled;
            f.ClosedAt = DateTime.UtcNow;
        }

        var template = await GetTemplateForEntityAsync(entityType, entitySubType);
        if (template == null) { await _db.SaveChangesAsync(); return; }

        var flow = new ApprovalFlow
        {
            EntityType      = entityType,
            EntityId        = entityId,
            TemplateId      = template.Id,
            Status          = FlowStatus.InProgress,
            CurrentStepOrder = 1,
            InitiatedById   = initiatedById ?? string.Empty,
            InitiatedAt     = DateTime.UtcNow,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow
        };
        _db.ApprovalFlows.Add(flow);
        await _db.SaveChangesAsync(); // get flow.Id

        foreach (var ts in template.Steps.OrderBy(s => s.Order))
        {
            _db.ApprovalSteps.Add(new ApprovalStep
            {
                FlowId     = flow.Id,
                EntityType = entityType,
                EntityId   = entityId,
                Order      = ts.Order,
                RoleName   = ts.RoleName,
                ApproverId = ts.ApproverId,
                Status     = ApprovalStatus.Pending,
                CreatedAt  = DateTime.UtcNow,
                UpdatedAt  = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<ApprovalFlow?> GetActiveFlowAsync(string entityType, int entityId)
    {
        return await _db.ApprovalFlows
            .Include(f => f.InitiatedBy)
            .Include(f => f.ReturnedBy)
            .Include(f => f.Steps).ThenInclude(s => s.Approver)
            .Where(f => f.EntityType == entityType && f.EntityId == entityId)
            .OrderByDescending(f => f.InitiatedAt)
            .FirstOrDefaultAsync();
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

    public async Task<ApprovalStep?> GetNextPendingStepAsync(string entityType, int entityId)
    {
        return await _db.ApprovalSteps
            .Include(s => s.Approver)
            .Where(s => s.EntityType == entityType && 
                        s.EntityId == entityId && 
                        s.Status == ApprovalStatus.Pending)
            .OrderBy(s => s.Order)
            .FirstOrDefaultAsync();
    }

    public async Task<ApprovalOutcome> ApproveStepAsync(int stepId, string approverId, string? comment)
    {
        var step = await _db.ApprovalSteps.FindAsync(stepId);
        if (step == null || step.ApproverId != approverId || step.Status != ApprovalStatus.Pending)
            return ApprovalOutcome.StillPending;

        step.Status    = ApprovalStatus.Approved;
        step.Comment   = comment;
        step.ActedAt   = DateTime.UtcNow;
        step.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var outcome = await EvaluateOutcomeAsync(step.EntityType, step.EntityId);

        var flow = await _db.ApprovalFlows.FirstOrDefaultAsync(
            f => f.EntityType == step.EntityType && f.EntityId == step.EntityId && f.Status == FlowStatus.InProgress);
        if (flow != null)
        {
            if (outcome == ApprovalOutcome.AllApproved)
            {
                flow.Status   = FlowStatus.Approved;
                flow.ClosedAt = DateTime.UtcNow;
            }
            else
            {
                flow.CurrentStepOrder = step.Order + 1;
            }
            flow.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return outcome;
    }

    public async Task<ApprovalOutcome> RejectStepAsync(int stepId, string approverId, string comment)
    {
        var step = await _db.ApprovalSteps.FindAsync(stepId);
        if (step == null || step.ApproverId != approverId || step.Status != ApprovalStatus.Pending)
            return ApprovalOutcome.StillPending;

        step.Status    = ApprovalStatus.Rejected;
        step.Comment   = comment;
        step.ActedAt   = DateTime.UtcNow;
        step.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var flow = await _db.ApprovalFlows.FirstOrDefaultAsync(
            f => f.EntityType == step.EntityType && f.EntityId == step.EntityId && f.Status == FlowStatus.InProgress);
        if (flow != null)
        {
            flow.Status    = FlowStatus.Rejected;
            flow.ClosedAt  = DateTime.UtcNow;
            flow.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return await EvaluateOutcomeAsync(step.EntityType, step.EntityId);
    }

    public async Task<ApprovalOutcome> ReturnStepAsync(int stepId, string approverId, string comment)
    {
        var step = await _db.ApprovalSteps.FindAsync(stepId);
        if (step == null || step.ApproverId != approverId || step.Status != ApprovalStatus.Pending)
            return ApprovalOutcome.StillPending;

        step.Status    = ApprovalStatus.ReturnedForEdit;
        step.Comment   = comment;
        step.ActedAt   = DateTime.UtcNow;
        step.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var flow = await _db.ApprovalFlows.FirstOrDefaultAsync(
            f => f.EntityType == step.EntityType && f.EntityId == step.EntityId && f.Status == FlowStatus.InProgress);
        if (flow != null)
        {
            flow.Status        = FlowStatus.ReturnedForEdit;
            flow.ReturnComment = comment;
            flow.ReturnedById  = approverId;
            flow.ClosedAt      = DateTime.UtcNow;
            flow.UpdatedAt     = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return ApprovalOutcome.ReturnedForEdit;
    }

    private async Task<ApprovalOutcome> EvaluateOutcomeAsync(string entityType, int entityId)
    {
        var steps = await _db.ApprovalSteps
            .Where(s => s.EntityType == entityType && s.EntityId == entityId)
            .ToListAsync();

        if (!steps.Any()) return ApprovalOutcome.StillPending;
        if (steps.Any(s => s.Status == ApprovalStatus.ReturnedForEdit)) return ApprovalOutcome.ReturnedForEdit;
        if (steps.Any(s => s.Status == ApprovalStatus.Rejected)) return ApprovalOutcome.AnyRejected;

        var template = await _db.ApprovalTemplates
            .FirstOrDefaultAsync(t => t.EntityType == entityType);
        var mode = template?.ApprovalMode ?? ApprovalMode.AllMustApprove;

        if (mode == ApprovalMode.AnyCanApprove && steps.Any(s => s.Status == ApprovalStatus.Approved))
            return ApprovalOutcome.AllApproved;

        if (steps.All(s => s.Status == ApprovalStatus.Approved)) return ApprovalOutcome.AllApproved;
        return ApprovalOutcome.StillPending;
    }
}
