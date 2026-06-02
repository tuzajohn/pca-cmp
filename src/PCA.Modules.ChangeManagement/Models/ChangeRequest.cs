using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.ChangeManagement.Models;

public class ChangeRequest : BaseEntity
{
    public string SerialNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ChangeType Type { get; set; }
    public Priority Priority { get; set; }
    public ChangeStatus Status { get; set; } = ChangeStatus.Draft;
    public string RequestedById { get; set; } = string.Empty;
    public ApplicationUser? RequestedBy { get; set; }
    public DateTime? TargetDate { get; set; }
    public string? ImplementationNotes { get; set; }

    // Extended CR form fields
    public string? SystemsAffected { get; set; }
    public string? RiskDescription { get; set; }
    public string? ImpactOnUsers { get; set; }
    public string? ProposedImplementationWindow { get; set; }
    public string? RollbackPlan { get; set; }
    public string? RollbackTrigger { get; set; }
    public string? TestingSteps { get; set; }
    public StagingTestedStatus? StagingTested { get; set; }

    public ICollection<ChangeRequestComment> Comments { get; set; } = new List<ChangeRequestComment>();
}
