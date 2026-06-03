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

    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public ICollection<DocumentPermission> Permissions { get; set; } = new List<DocumentPermission>();

    public DocumentVersion? CurrentVersion => Versions.FirstOrDefault(v => v.IsCurrentVersion);
}
