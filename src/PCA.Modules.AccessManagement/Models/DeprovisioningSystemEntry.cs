using PCA.Modules.Identity.Models;
using PCA.Shared;

namespace PCA.Modules.AccessManagement.Models;

public class DeprovisioningSystemEntry : BaseEntity
{
    public int DeprovisioningEventId { get; set; }
    public DeprovisioningEvent? DeprovisioningEvent { get; set; }

    public string SystemName { get; set; } = string.Empty;
    public string AccessDescription { get; set; } = string.Empty;

    public bool IsDeactivated { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public string? DeactivatedById { get; set; }
    public ApplicationUser? DeactivatedBy { get; set; }
}
