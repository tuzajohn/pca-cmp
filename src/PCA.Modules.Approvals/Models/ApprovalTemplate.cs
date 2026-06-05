using PCA.Shared;

namespace PCA.Modules.Approvals.Models;

public enum ApprovalMode { AllMustApprove, AnyCanApprove }
public enum AutoTriggerOn
{
    None,           // manual — user clicks Submit for Approval
    OnSubmit,       // triggered when entity is first submitted / created
    OnStatusChange, // triggered when status changes (generic)
    OnNewVersion,   // document: triggered when a new file version is uploaded
    OnReview,       // document: triggered when Mark as Reviewed is submitted
    OnRetire,       // document: triggered before retiring
    OnPublish,      // document: triggered when moving from Draft → Active
}

public class ApprovalTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntitySubType { get; set; }
    public ApprovalMode ApprovalMode { get; set; } = ApprovalMode.AllMustApprove;
    public AutoTriggerOn AutoTriggerOn { get; set; } = AutoTriggerOn.None;
    public string? ConditionField { get; set; }
    public string? ConditionValue { get; set; }
    public ICollection<ApprovalTemplateStep> Steps { get; set; } = new List<ApprovalTemplateStep>();
}
