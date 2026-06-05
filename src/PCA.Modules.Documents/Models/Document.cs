using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.Documents.Models;

public class Document : BaseEntity
{
    public string SerialNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public int? FolderId { get; set; }
    public DocumentFolder? Folder { get; set; }

    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }

    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }

    // Review schedule
    public int? ReviewPeriodDays { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public string? LastReviewedById { get; set; }
    public ApplicationUser? LastReviewedBy { get; set; }
    // Bitmask: 1=7-day alert sent, 2=3-day alert sent, 4=1-day alert sent, 8=overdue alert sent
    public int ReviewAlertFlags { get; set; }

    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public ICollection<DocumentPermission> Permissions { get; set; } = new List<DocumentPermission>();

    public DocumentVersion? CurrentVersion => Versions.FirstOrDefault(v => v.IsCurrentVersion);

    public ReviewDueState GetReviewDueState()
    {
        if (NextReviewDate == null) return ReviewDueState.NotScheduled;
        var days = (NextReviewDate.Value.Date - DateTime.UtcNow.Date).TotalDays;
        if (days < 0) return ReviewDueState.Overdue;
        if (days <= 7) return ReviewDueState.DueSoon;
        return ReviewDueState.OnTrack;
    }
}

public enum ReviewDueState { NotScheduled, OnTrack, DueSoon, Overdue }
