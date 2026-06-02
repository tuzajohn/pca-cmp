using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Approvals.Services;
using PCA.Modules.ChangeManagement.Services;
using PCA.Modules.Identity.Models;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IChangeRequestService _crService;
    private readonly IApprovalService _approvalService;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(IChangeRequestService crService, IApprovalService approvalService, UserManager<ApplicationUser> userManager)
    {
        _crService = crService;
        _approvalService = approvalService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var vm = new DashboardViewModel
        {
            RecentChangeRequests = await _crService.GetRecentAsync(10),
            StatusCounts = await _crService.GetStatusCountsAsync()
        };

        if (user != null)
        {
            vm.PendingApprovals = await _approvalService.GetPendingStepsForApproverAsync(user.Id);
        }

        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}
