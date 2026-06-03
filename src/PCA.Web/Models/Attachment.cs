using PCA.Modules.Identity.Models;
using PCA.Shared;

namespace PCA.Web.Models;

public class Attachment : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string UploadedById { get; set; } = string.Empty;
    public ApplicationUser? UploadedBy { get; set; }
}
