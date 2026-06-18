using PCA.Modules.Identity.Models;
using PCA.Shared;

namespace PCA.Modules.AccessManagement.Models;

public class AccessRequestComment : BaseEntity
{
    public int AccessRequestId { get; set; }
    public AccessRequest? AccessRequest { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }
    public string Content { get; set; } = string.Empty;
}
