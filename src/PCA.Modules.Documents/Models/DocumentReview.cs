using PCA.Modules.Identity.Models;
using PCA.Shared;

namespace PCA.Modules.Documents.Models;

public enum ReviewStatus { Pending, Approved, Rejected }

public class DocumentReview : BaseEntity
{
    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public string ReviewedById { get; set; } = string.Empty;
    public ApplicationUser? ReviewedBy { get; set; }

    public DateTime ReviewedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
}
