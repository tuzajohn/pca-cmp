using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.Documents.Models;

public class DocumentPermission : BaseEntity
{
    public int DocumentId { get; set; }
    public Document? Document { get; set; }
    public PermissionSubjectType SubjectType { get; set; }
    public string SubjectId { get; set; } = string.Empty;
    public AccessLevel AccessLevel { get; set; }
}
