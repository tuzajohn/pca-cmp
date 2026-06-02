using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PCA.Modules.Approvals.Models;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;
using PCA.Web.Models;
using PCA.Web.Services;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IApprovalService _approvalService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IThemeService _themeService;

    public AdminController(IApprovalService approvalService, UserManager<ApplicationUser> userManager, IThemeService themeService)
    {
        _approvalService = approvalService;
        _userManager = userManager;
        _themeService = themeService;
    }

    public async Task<IActionResult> Index()
    {
        var templates = await _approvalService.GetTemplatesAsync();
        return View(templates);
    }

    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.ToListAsync();
        return View(users);
    }

    public async Task<IActionResult> EditTemplate(int id)
    {
        var template = (await _approvalService.GetTemplatesAsync()).FirstOrDefault(t => t.Id == id);
        if (template == null) return NotFound();
        ViewBag.Users = await _userManager.Users.ToListAsync();
        return View(template);
    }

    public async Task<IActionResult> CreateTemplate()
    {
        ViewBag.Users = await _userManager.Users.ToListAsync();
        return View(new ApprovalTemplate());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(string name, ChangeType changeType,
        List<string> approverIds, List<string> roleNames)
    {
        var steps = new List<ApprovalTemplateStep>();
        for (int i = 0; i < approverIds.Count; i++)
        {
            steps.Add(new ApprovalTemplateStep
            {
                Order = i + 1,
                ApproverId = approverIds[i],
                RoleName = roleNames.Count > i ? roleNames[i] : string.Empty
            });
        }

        var template = new ApprovalTemplate
        {
            Name = name,
            ChangeType = changeType,
            Steps = steps
        };
        await _approvalService.CreateTemplateAsync(template);
        TempData["Success"] = "Template created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // Theme
    public async Task<IActionResult> Theme()
    {
        var theme = await _themeService.GetThemeAsync();
        return View(theme);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Theme(ThemeSettings settings)
    {
        if (!ModelState.IsValid) return View(settings);
        await _themeService.SaveThemeAsync(settings);
        TempData["Success"] = "Theme updated successfully.";
        return RedirectToAction(nameof(Theme));
    }
}
