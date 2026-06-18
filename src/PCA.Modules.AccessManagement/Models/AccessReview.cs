using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.AccessManagement.Models;

public class AccessReview : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public AccessReviewCycle Cycle { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }  // 1–4; semi-annual uses 1=H1, 3=H2

    public DateTime ReviewPeriodStart { get; set; }
    public DateTime ReviewPeriodEnd { get; set; }
    public DateTime DueDate { get; set; }

    public AccessReviewStatus Status { get; set; } = AccessReviewStatus.Scheduled;

    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }

    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }

    public ICollection<AccessReviewEntry> Entries { get; set; } = new List<AccessReviewEntry>();
}
