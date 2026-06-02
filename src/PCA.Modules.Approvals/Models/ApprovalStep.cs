using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.Approvals.Models;

public class ApprovalStep : BaseEntity
{
    public int ChangeRequestId { get; set; }
    public int Order { get; set; }
    public string ApproverId { get; set; } = string.Empty;
    public ApplicationUser? Approver { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? Comment { get; set; }
    public DateTime? ActedAt { get; set; }
}
