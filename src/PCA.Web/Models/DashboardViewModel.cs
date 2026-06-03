using PCA.Modules.Approvals.Models;
using PCA.Modules.ChangeManagement.Models;
using PCA.Modules.Documents.Models;
using PCA.Modules.Incidents.Models;
using PCA.Shared.Enums;

namespace PCA.Web.Models;

public class DashboardViewModel
{
    // Change Management
    public List<ChangeRequest> RecentChangeRequests { get; set; } = new();
    public List<ApprovalStep> PendingApprovals { get; set; } = new();
    public Dictionary<ChangeStatus, int> StatusCounts { get; set; } = new();
    public int TotalChangeRequests => StatusCounts.Values.Sum();

    // Documents
    public int TotalDocuments { get; set; }
    public int ActiveDocuments { get; set; }
    public int TotalFolders { get; set; }
    public List<Document> RecentDocuments { get; set; } = new();

    // Incidents
    public int OpenIncidents { get; set; }
    public Dictionary<IncidentSeverity, int> IncidentsBySeverity { get; set; } = new();
    public List<Incident> RecentIncidents { get; set; } = new();
    public int CriticalIncidents => IncidentsBySeverity.TryGetValue(IncidentSeverity.S1Critical, out var n) ? n : 0;
    public int HighIncidents => IncidentsBySeverity.TryGetValue(IncidentSeverity.S2High, out var n) ? n : 0;
}
