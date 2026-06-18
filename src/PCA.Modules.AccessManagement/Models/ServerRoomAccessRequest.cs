using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.AccessManagement.Models;

public class ServerRoomAccessRequest : BaseEntity
{
    public string SerialNumber { get; set; } = string.Empty;

    public string RequestedById { get; set; } = string.Empty;
    public ApplicationUser? RequestedBy { get; set; }

    public string VisitorName { get; set; } = string.Empty;
    public string VisitorTitle { get; set; } = string.Empty;
    public string VisitorCompany { get; set; } = string.Empty;
    public bool IsExternal { get; set; }

    public string Purpose { get; set; } = string.Empty;
    public DateTime PlannedEntryDateTime { get; set; }
    public DateTime PlannedExitDateTime { get; set; }

    public DateTime? ActualEntryDateTime { get; set; }
    public DateTime? ActualExitDateTime { get; set; }

    public ServerRoomAccessStatus Status { get; set; } = ServerRoomAccessStatus.Draft;

    public string? WrittenRequestReference { get; set; }
    public string? EscortedBy { get; set; }

    public ICollection<ServerRoomAccessComment> Comments { get; set; } = new List<ServerRoomAccessComment>();
}
