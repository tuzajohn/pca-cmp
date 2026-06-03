using PCA.Modules.Identity.Models;
using PCA.Shared;

namespace PCA.Modules.Documents.Models;

public class DocumentFolder : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public DocumentFolder? Parent { get; set; }
    public ICollection<DocumentFolder> Children { get; set; } = new List<DocumentFolder>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<FolderPermission> Permissions { get; set; } = new List<FolderPermission>();
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }
}
