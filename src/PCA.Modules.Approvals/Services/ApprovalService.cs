using Microsoft.EntityFrameworkCore;
using PCA.Modules.Approvals.Models;
using PCA.Modules.ChangeManagement.Services;
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
    private readonly IChangeRequestService _crService;

    public ApprovalService(IApplicationDbContextForApprovals db, IChangeRequestService crService)
    {
        _db = db;
        _crService = crService;
    }

    public async Task<List<ApprovalTemplate>> GetTemplatesAsync()
    {
        return await _db.ApprovalTemplates
            .Include(t => t.Steps)
                .ThenInclude(s => s.Approver)
            .ToListAsync();
    }

    public async Task<ApprovalTemplate?> GetTemplateByTypeAsync(ChangeType changeType)
    {
        return await _db.ApprovalTemplates
            .Include(t => t.Steps)
                .ThenInclude(s => s.Approver)
            .FirstOrDefaultAsync(t => t.ChangeType == changeType);
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

    public async Task InitiateApprovalFlowAsync(int changeRequestId, ChangeType changeType)
    {
        // Remove existing steps if any
        var existing = await _db.ApprovalSteps
            .Where(s => s.ChangeRequestId == changeRequestId)
            .ToListAsync();
        foreach (var s in existing) _db.ApprovalSteps.Remove(s);

        var template = await GetTemplateByTypeAsync(changeType);
        if (template == null) return;

        foreach (var templateStep in template.Steps.OrderBy(s => s.Order))
        {
            var step = new ApprovalStep
            {
                ChangeRequestId = changeRequestId,
                Order = templateStep.Order,
                ApproverId = templateStep.ApproverId,
                Status = ApprovalStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ApprovalSteps.Add(step);
        }

        await _db.SaveChangesAsync();

        // Set CR to UnderReview
        await _crService.UpdateStatusAsync(changeRequestId, ChangeStatus.UnderReview, string.Empty);
    }

    public async Task<List<ApprovalStep>> GetStepsForRequestAsync(int changeRequestId)
    {
        return await _db.ApprovalSteps
            .Include(s => s.Approver)
            .Where(s => s.ChangeRequestId == changeRequestId)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<List<ApprovalStep>> GetPendingStepsForApproverAsync(string approverId)
    {
        return await _db.ApprovalSteps
            .Where(s => s.ApproverId == approverId && s.Status == ApprovalStatus.Pending)
            .OrderBy(s => s.ChangeRequestId)
            .ThenBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<bool> ApproveStepAsync(int stepId, string approverId, string? comment)
    {
        var step = await _db.ApprovalSteps.FindAsync(stepId);
        if (step == null || step.ApproverId != approverId) return false;
        if (step.Status != ApprovalStatus.Pending) return false;

        step.Status = ApprovalStatus.Approved;
        step.Comment = comment;
        step.ActedAt = DateTime.UtcNow;
        step.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await ProcessApprovalResultAsync(step.ChangeRequestId);
        return true;
    }

    public async Task<bool> RejectStepAsync(int stepId, string approverId, string comment)
    {
        var step = await _db.ApprovalSteps.FindAsync(stepId);
        if (step == null || step.ApproverId != approverId) return false;
        if (step.Status != ApprovalStatus.Pending) return false;

        step.Status = ApprovalStatus.Rejected;
        step.Comment = comment;
        step.ActedAt = DateTime.UtcNow;
        step.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await ProcessApprovalResultAsync(step.ChangeRequestId);
        return true;
    }

    public async Task<ChangeStatus?> ProcessApprovalResultAsync(int changeRequestId)
    {
        var steps = await _db.ApprovalSteps
            .Where(s => s.ChangeRequestId == changeRequestId)
            .ToListAsync();

        if (!steps.Any()) return null;

        if (steps.Any(s => s.Status == ApprovalStatus.Rejected))
        {
            await _crService.UpdateStatusAsync(changeRequestId, ChangeStatus.Rejected, string.Empty);
            return ChangeStatus.Rejected;
        }

        if (steps.All(s => s.Status == ApprovalStatus.Approved))
        {
            await _crService.UpdateStatusAsync(changeRequestId, ChangeStatus.Approved, string.Empty);
            return ChangeStatus.Approved;
        }

        return ChangeStatus.UnderReview;
    }
}
