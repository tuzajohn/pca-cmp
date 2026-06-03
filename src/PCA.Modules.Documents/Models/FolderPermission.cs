using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.Documents.Models;

public class FolderPermission : BaseEntity
{
    public int FolderId { get; set; }
    public DocumentFolder? Folder { get; set; }
    public PermissionSubjectType SubjectType { get; set; }
    public string SubjectId { get; set; } = string.Empty;
    public AccessLevel AccessLevel { get; set; }
}
