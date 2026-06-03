using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.Approvals.Models;

public class ApprovalTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntitySubType { get; set; }
    public ChangeType ChangeType { get; set; }
    public ICollection<ApprovalTemplateStep> Steps { get; set; } = new List<ApprovalTemplateStep>();
}
