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
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IThemeService _themeService;

    public AdminController(IApprovalService approvalService,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IThemeService themeService)
    {
        _approvalService = approvalService;
        _userManager = userManager;
        _roleManager = roleManager;
        _themeService = themeService;
    }

    // ── Approval Templates ──────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var templates = await _approvalService.GetTemplatesAsync();
        return View(templates);
    }

    public async Task<IActionResult> EditTemplate(int id)
    {
        var template = (await _approvalService.GetTemplatesAsync()).FirstOrDefault(t => t.Id == id);
        if (template == null) return NotFound();
        ViewBag.Users = await _userManager.Users.ToListAsync();
        return View(template);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTemplate(int id, string name, ChangeType changeType,
        List<string> approverIds, List<string> roleNames)
    {
        var template = (await _approvalService.GetTemplatesAsync()).FirstOrDefault(t => t.Id == id);
        if (template == null) return NotFound();

        template.Name = name;
        template.ChangeType = changeType;
        template.Steps = approverIds.Select((aid, i) => new ApprovalTemplateStep
        {
            TemplateId = id,
            Order = i + 1,
            ApproverId = aid,
            RoleName = roleNames.Count > i ? roleNames[i] : string.Empty
        }).ToList();

        await _approvalService.UpdateTemplateAsync(template);
        TempData["Success"] = "Approval template updated.";
        return RedirectToAction(nameof(Index));
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
        var steps = approverIds.Select((id, i) => new ApprovalTemplateStep
        {
            Order = i + 1,
            ApproverId = id,
            RoleName = roleNames.Count > i ? roleNames[i] : string.Empty
        }).ToList();

        await _approvalService.CreateTemplateAsync(new ApprovalTemplate
        {
            Name = name,
            ChangeType = changeType,
            Steps = steps
        });
        TempData["Success"] = "Approval template created.";
        return RedirectToAction(nameof(Index));
    }

    // ── Users ───────────────────────────────────────────────────────────────

    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.ToListAsync();
        var list = new List<UserListItemViewModel>();
        foreach (var u in users)
        {
            list.Add(new UserListItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                Department = u.Department,
                Roles = (await _userManager.GetRolesAsync(u)).ToList()
            });
        }
        return View(list);
    }

    public async Task<IActionResult> CreateUser()
    {
        ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        return View(new CreateUserViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            return View(vm);
        }

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            FullName = vm.FullName,
            Department = vm.Department,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, vm.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            return View(vm);
        }

        if (vm.SelectedRoles.Any())
            await _userManager.AddToRolesAsync(user, vm.SelectedRoles);

        TempData["Success"] = $"User {vm.Email} created successfully.";
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> EditUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var vm = new EditUserViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Department = user.Department,
            SelectedRoles = (await _userManager.GetRolesAsync(user)).ToList(),
            AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(EditUserViewModel vm)
    {
        var user = await _userManager.FindByIdAsync(vm.Id);
        if (user == null) return NotFound();

        user.FullName = vm.FullName;
        user.Department = vm.Department;
        await _userManager.UpdateAsync(user);

        // Sync roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (vm.SelectedRoles.Any())
            await _userManager.AddToRolesAsync(user, vm.SelectedRoles);

        // Optional password reset
        if (!string.IsNullOrWhiteSpace(vm.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var pwResult = await _userManager.ResetPasswordAsync(user, token, vm.NewPassword);
            if (!pwResult.Succeeded)
            {
                foreach (var e in pwResult.Errors) ModelState.AddModelError(string.Empty, e.Description);
                vm.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
                vm.Email = user.Email ?? string.Empty;
                return View(vm);
            }
        }

        TempData["Success"] = $"User {user.Email} updated.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        await _userManager.DeleteAsync(user);
        TempData["Success"] = "User deleted.";
        return RedirectToAction(nameof(Users));
    }

    // ── Theme ───────────────────────────────────────────────────────────────

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
