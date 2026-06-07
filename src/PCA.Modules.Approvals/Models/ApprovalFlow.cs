using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.Approvals.Models;

public enum FlowStatus
{
    InProgress,
    Approved,
    Rejected,
    ReturnedForEdit,
    Cancelled
}

public class ApprovalFlow : BaseEntity
{
    public string EntityType     { get; set; } = string.Empty;
    public int    EntityId       { get; set; }
    public int    TemplateId     { get; set; }
    public ApprovalTemplate? Template { get; set; }

    public FlowStatus Status          { get; set; } = FlowStatus.InProgress;
    public int        CurrentStepOrder { get; set; } = 1;

    public string  InitiatedById { get; set; } = string.Empty;
    public ApplicationUser? InitiatedBy { get; set; }
    public DateTime InitiatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt    { get; set; }

    // Populated when Status == ReturnedForEdit
    public string? ReturnComment  { get; set; }
    public string? ReturnedById   { get; set; }
    public ApplicationUser? ReturnedBy { get; set; }

    public ICollection<ApprovalStep> Steps { get; set; } = new List<ApprovalStep>();
}
