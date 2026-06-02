using PCA.Modules.Identity.Models;
using PCA.Shared;

namespace PCA.Modules.ChangeManagement.Models;

public class ChangeRequestComment : BaseEntity
{
    public int ChangeRequestId { get; set; }
    public ChangeRequest? ChangeRequest { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }
    public string Content { get; set; } = string.Empty;
}
