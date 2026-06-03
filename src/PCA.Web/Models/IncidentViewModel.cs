using System.ComponentModel.DataAnnotations;
using PCA.Shared.Enums;

namespace PCA.Web.Models;

public class IncidentCreateViewModel
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public IncidentCategory Category { get; set; }

    [Required]
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.S3Medium;

    [Required]
    public Priority Priority { get; set; } = Priority.Medium;

    public string? AffectedSystems { get; set; }
    public string? ImpactDescription { get; set; }

    [Required]
    public DateTime DetectedAt { get; set; } = DateTime.Now;
}

public class IncidentEditViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public IncidentCategory Category { get; set; }

    [Required]
    public IncidentSeverity Severity { get; set; }

    [Required]
    public Priority Priority { get; set; }

    public string? AffectedSystems { get; set; }
    public string? ImpactDescription { get; set; }

    [Required]
    public DateTime DetectedAt { get; set; }

    public int? LinkedChangeRequestId { get; set; }
}

public class IncidentResolveViewModel
{
    public int Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    [Required]
    public string RootCause { get; set; } = string.Empty;

    [Required]
    public string ResolutionSummary { get; set; } = string.Empty;
}
