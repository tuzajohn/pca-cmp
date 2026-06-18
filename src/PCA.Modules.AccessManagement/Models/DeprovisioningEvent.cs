using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.AccessManagement.Models;

public class DeprovisioningEvent : BaseEntity
{
    public string SerialNumber { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;

    public DeprovisioningTrigger Trigger { get; set; }
    public string TriggerDetails { get; set; } = string.Empty;

    // SLA: must complete within 24 hours of HR notification
    public DateTime HrNotificationReceivedAt { get; set; }
    public DateTime SlaDeadline { get; set; }
    public DateTime? SlaWarningEmailSentAt { get; set; }

    public DeprovisioningStatus Status { get; set; } = DeprovisioningStatus.Notified;

    public string NotifiedById { get; set; } = string.Empty;
    public ApplicationUser? NotifiedBy { get; set; }

    public DateTime? CompletedAt { get; set; }
    public string? CompletedById { get; set; }
    public ApplicationUser? CompletedBy { get; set; }

    public string? Notes { get; set; }

    public ICollection<DeprovisioningSystemEntry> SystemEntries { get; set; } = new List<DeprovisioningSystemEntry>();
}
