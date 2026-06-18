using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.AccessManagement.Models;

public class AccessRequest : BaseEntity
{
    public string SerialNumber { get; set; } = string.Empty;

    public string RequestedById { get; set; } = string.Empty;
    public ApplicationUser? RequestedBy { get; set; }

    // The person gaining access (may differ from requester)
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;

    public string SystemName { get; set; } = string.Empty;
    public AccessType AccessType { get; set; }
    public string AccessDetails { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;

    public DateTime RequestedByDate { get; set; }
    public DateTime? AccessExpiryDate { get; set; }

    // Drives approval template selection: Privileged vs Standard
    public bool IsPrivileged { get; set; }

    public AccessRequestStatus Status { get; set; } = AccessRequestStatus.Draft;

    public DateTime? ProvisionedAt { get; set; }
    public string? ProvisionedById { get; set; }
    public ApplicationUser? ProvisionedBy { get; set; }

    public ICollection<AccessRequestComment> Comments { get; set; } = new List<AccessRequestComment>();
}
