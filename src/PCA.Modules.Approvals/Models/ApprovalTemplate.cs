using PCA.Shared;

namespace PCA.Modules.Approvals.Models;

public class ApprovalTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntitySubType { get; set; }
    public ICollection<ApprovalTemplateStep> Steps { get; set; } = new List<ApprovalTemplateStep>();
}
