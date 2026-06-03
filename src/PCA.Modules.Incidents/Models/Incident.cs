using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.Incidents.Models;

public class Incident : BaseEntity
{
    public string SerialNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IncidentCategory Category { get; set; }
    public IncidentSeverity Severity { get; set; }
    public Priority Priority { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public string? AffectedSystems { get; set; }
    public string? ImpactDescription { get; set; }
    public string? RootCause { get; set; }
    public string? ResolutionSummary { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? LinkedChangeRequestId { get; set; }

    public string ReportedById { get; set; } = string.Empty;
    public ApplicationUser? ReportedBy { get; set; }

    public string? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    public ICollection<IncidentUpdate> Updates { get; set; } = new List<IncidentUpdate>();
    public ICollection<IncidentDocument> LinkedDocuments { get; set; } = new List<IncidentDocument>();
}
