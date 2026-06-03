using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Approvals.Services;
using PCA.Modules.ChangeManagement.Services;
using PCA.Modules.Documents.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IChangeRequestService _crService;
    private readonly IApprovalService _approvalService;
    private readonly IDocumentService _docService;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(IChangeRequestService crService, IApprovalService approvalService,
        IDocumentService docService, UserManager<ApplicationUser> userManager)
    {
        _crService = crService;
        _approvalService = approvalService;
        _docService = docService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");

        var allDocs = await _docService.GetAllAsync();
        var folders = await _docService.GetFolderTreeAsync();

        List<PCA.Modules.Documents.Models.Document> visibleDocs;
        if (isAdmin)
        {
            visibleDocs = allDocs;
        }
        else
        {
            var roles = (await _userManager.GetRolesAsync(user!)).ToList();
            visibleDocs = new List<PCA.Modules.Documents.Models.Document>();
            foreach (var doc in allDocs)
            {
                var access = await _docService.GetEffectiveAccessAsync(doc.Id, doc.FolderId, user!.Id, roles);
                if (access.HasValue) visibleDocs.Add(doc);
            }
        }

        var vm = new DashboardViewModel
        {
            RecentChangeRequests = await _crService.GetRecentAsync(8),
            StatusCounts = await _crService.GetStatusCountsAsync(),
            TotalDocuments = visibleDocs.Count,
            ActiveDocuments = visibleDocs.Count(d => d.Status == DocumentStatus.Active),
            TotalFolders = folders.Count,
            RecentDocuments = visibleDocs.OrderByDescending(d => d.UpdatedAt).Take(5).ToList()
        };

        if (user != null)
            vm.PendingApprovals = await _approvalService.GetPendingStepsForApproverAsync(user.Id);

        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
