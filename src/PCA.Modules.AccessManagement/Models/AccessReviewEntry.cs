using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.AccessManagement.Models;

public class AccessReviewEntry : BaseEntity
{
    public int AccessReviewId { get; set; }
    public AccessReview? AccessReview { get; set; }

    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string CurrentAccessLevel { get; set; } = string.Empty;

    public AccessReviewEntryOutcome Outcome { get; set; } = AccessReviewEntryOutcome.Pending;
    public string? ReviewerNotes { get; set; }

    public string? ReviewedById { get; set; }
    public ApplicationUser? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public int? LinkedDeprovisioningEventId { get; set; }
}
