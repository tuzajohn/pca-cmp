using System.ComponentModel.DataAnnotations;
using PCA.Shared.Enums;

namespace PCA.Web.Models;

public class ChangeRequestCreateViewModel
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000), Display(Name = "Description of Change")]
    public string Description { get; set; } = string.Empty;

    [Required, Display(Name = "Category")]
    public ChangeType Type { get; set; }

    [Required, Display(Name = "Risk Assessment")]
    public Priority Priority { get; set; }

    [Display(Name = "Target / Proposed Implementation Window")]
    public string? ProposedImplementationWindow { get; set; }

    [Display(Name = "Target Date")]
    public DateTime? TargetDate { get; set; }

    [MaxLength(1000), Display(Name = "Systems / Processes Affected")]
    public string? SystemsAffected { get; set; }

    [MaxLength(2000), Display(Name = "Risk Details")]
    public string? RiskDescription { get; set; }

    [MaxLength(1000), Display(Name = "Impact on Users / PDMS")]
    public string? ImpactOnUsers { get; set; }

    [MaxLength(4000), Display(Name = "Rollback Plan")]
    public string? RollbackPlan { get; set; }

    [MaxLength(1000), Display(Name = "Rollback Trigger / Window")]
    public string? RollbackTrigger { get; set; }

    [MaxLength(4000), Display(Name = "Testing / Verification Steps")]
    public string? TestingSteps { get; set; }

    [Display(Name = "Staging Environment Tested?")]
    public StagingTestedStatus? StagingTested { get; set; }
}

public class ChangeRequestEditViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000), Display(Name = "Description of Change")]
    public string Description { get; set; } = string.Empty;

    [Required, Display(Name = "Category")]
    public ChangeType Type { get; set; }

    [Required, Display(Name = "Risk Assessment")]
    public Priority Priority { get; set; }

    [Display(Name = "Target Date")]
    public DateTime? TargetDate { get; set; }

    [Display(Name = "Target / Proposed Implementation Window")]
    public string? ProposedImplementationWindow { get; set; }

    [MaxLength(1000), Display(Name = "Systems / Processes Affected")]
    public string? SystemsAffected { get; set; }

    [MaxLength(2000), Display(Name = "Risk Details")]
    public string? RiskDescription { get; set; }

    [MaxLength(1000), Display(Name = "Impact on Users / PDMS")]
    public string? ImpactOnUsers { get; set; }

    [MaxLength(4000), Display(Name = "Rollback Plan")]
    public string? RollbackPlan { get; set; }

    [MaxLength(1000), Display(Name = "Rollback Trigger / Window")]
    public string? RollbackTrigger { get; set; }

    [MaxLength(4000), Display(Name = "Testing / Verification Steps")]
    public string? TestingSteps { get; set; }

    [Display(Name = "Staging Environment Tested?")]
    public StagingTestedStatus? StagingTested { get; set; }

    [MaxLength(4000), Display(Name = "Implementation Notes")]
    public string? ImplementationNotes { get; set; }
}

public class PirViewModel
{
    public int ChangeRequestId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    [Required, Display(Name = "Implementation Outcome")]
    public ImplementationOutcome Outcome { get; set; }

    [Display(Name = "Actual Implementation Date")]
    public DateTime? ActualDate { get; set; }

    [Display(Name = "Was Rollback Executed?")]
    public bool RollbackExecuted { get; set; }

    [Display(Name = "Issues Encountered")]
    public string? IssuesEncountered { get; set; }

    [Display(Name = "Lessons Learned")]
    public string? LessonsLearned { get; set; }

    [Display(Name = "Closure Notes")]
    public string? ClosureNotes { get; set; }
}
