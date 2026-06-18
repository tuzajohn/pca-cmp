using PCA.Modules.Identity.Models;
using PCA.Shared;

namespace PCA.Modules.AccessManagement.Models;

public class ServerRoomAccessComment : BaseEntity
{
    public int ServerRoomAccessRequestId { get; set; }
    public ServerRoomAccessRequest? ServerRoomAccessRequest { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }
    public string Content { get; set; } = string.Empty;
}
