using PCA.Modules.Identity.Models;
using PCA.Shared;

namespace PCA.Modules.Approvals.Models;

public class ApprovalTemplateStep : BaseEntity
{
    public int TemplateId { get; set; }
    public ApprovalTemplate? Template { get; set; }
    public int Order { get; set; }
    public string ApproverId { get; set; } = string.Empty;
    public ApplicationUser? Approver { get; set; }
    public string RoleName { get; set; } = string.Empty;
}
