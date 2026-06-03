using PCA.Modules.Identity.Models;
using PCA.Shared;

namespace PCA.Modules.Documents.Models;

public class DocumentVersion : BaseEntity
{
    public int DocumentId { get; set; }
    public Document? Document { get; set; }
    public int VersionNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? ChangeNotes { get; set; }
    public bool IsCurrentVersion { get; set; }
    public string UploadedById { get; set; } = string.Empty;
    public ApplicationUser? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
