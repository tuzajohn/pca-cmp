using PCA.Shared.Enums;

namespace PCA.Web.Helpers;

public static class IncidentViewHelpers
{
    public static string StatusClass(IncidentStatus s)
    {
        return s switch
        {
            IncidentStatus.Open => "status-submitted",
            IncidentStatus.InProgress => "status-under-review",
            IncidentStatus.OnHold => "status-draft",
            IncidentStatus.Resolved => "status-approved",
            IncidentStatus.Closed => "status-closed",
            _ => string.Empty
        };
    }

    public static string SeverityBadgeClass(IncidentSeverity s)
    {
        return s switch
        {
            IncidentSeverity.S1Critical => "bg-danger",
            IncidentSeverity.S2High => "bg-warning text-dark",
            IncidentSeverity.S3Medium => "bg-info text-dark",
            IncidentSeverity.S4Low => "bg-secondary",
            _ => "bg-secondary"
        };
    }

    public static string SeverityLabel(IncidentSeverity s)
    {
        return s switch
        {
            IncidentSeverity.S1Critical => "S1 Critical",
            IncidentSeverity.S2High => "S2 High",
            IncidentSeverity.S3Medium => "S3 Medium",
            IncidentSeverity.S4Low => "S4 Low",
            _ => s.ToString()
        };
    }

    public static string UpdateDotClass(IncidentUpdateType t)
    {
        return t switch
        {
            IncidentUpdateType.Resolution => "dot-success",
            IncidentUpdateType.StatusChange => "dot-primary",
            IncidentUpdateType.Assignment => "dot-warning",
            _ => "dot-muted"
        };
    }
}
