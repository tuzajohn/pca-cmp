using PCA.Modules.Approvals.Models;
using PCA.Modules.ChangeManagement.Models;
using PCA.Modules.Documents.Models;
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
}
