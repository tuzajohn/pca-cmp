using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PCA.Modules.Approvals.Models;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Identity.Models;
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
    private readonly IEmailService _emailService;

    public AdminController(IApprovalService approvalService,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IThemeService themeService,
        IEmailService emailService)
    {
        _approvalService = approvalService;
        _userManager = userManager;
        _roleManager = roleManager;
        _themeService = themeService;
        _emailService = emailService;
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
    public async Task<IActionResult> EditTemplate(int id, string name, string entityType, string? entitySubType,
        string approvalMode, string autoTriggerOn, string? conditionField, string? conditionValue,
        List<string> approverIds, List<string> roleNames)
    {
        var template = (await _approvalService.GetTemplatesAsync()).FirstOrDefault(t => t.Id == id);
        if (template == null) return NotFound();

        template.Name = name;
        template.EntityType = entityType;
        template.EntitySubType = string.IsNullOrWhiteSpace(entitySubType) ? null : entitySubType;
        template.ApprovalMode = Enum.TryParse<ApprovalMode>(approvalMode, out var mode) ? mode : ApprovalMode.AllMustApprove;
        template.AutoTriggerOn = Enum.TryParse<AutoTriggerOn>(autoTriggerOn, out var trigger) ? trigger : AutoTriggerOn.None;
        template.ConditionField = string.IsNullOrWhiteSpace(conditionField) ? null : conditionField;
        template.ConditionValue = string.IsNullOrWhiteSpace(conditionValue) ? null : conditionValue;
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
    public async Task<IActionResult> CreateTemplate(string name, string entityType, string? entitySubType,
        string approvalMode, string autoTriggerOn, string? conditionField, string? conditionValue,
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
            EntityType = entityType,
            EntitySubType = string.IsNullOrWhiteSpace(entitySubType) ? null : entitySubType,
            ApprovalMode = Enum.TryParse<ApprovalMode>(approvalMode, out var mode) ? mode : ApprovalMode.AllMustApprove,
            AutoTriggerOn = Enum.TryParse<AutoTriggerOn>(autoTriggerOn, out var trigger) ? trigger : AutoTriggerOn.None,
            ConditionField = string.IsNullOrWhiteSpace(conditionField) ? null : conditionField,
            ConditionValue = string.IsNullOrWhiteSpace(conditionValue) ? null : conditionValue,
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
            var status = !u.EmailConfirmed ? UserAccountStatus.Pending
                : u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow ? UserAccountStatus.Blocked
                : UserAccountStatus.Active;

            list.Add(new UserListItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                Department = u.Department,
                Roles = (await _userManager.GetRolesAsync(u)).ToList(),
                Status = status
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
            Department = vm.Department ?? string.Empty,
            EmailConfirmed = false,
            LockoutEnabled = true
        };

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            return View(vm);
        }

        if (vm.SelectedRoles.Any())
            await _userManager.AddToRolesAsync(user, vm.SelectedRoles);

        var inviteLink = await GenerateSetPasswordLinkAsync(user);
        try
        {
            await _emailService.SendInviteAsync(user.Email!, user.FullName, inviteLink);
            TempData["Success"] = $"User {vm.Email} created. An activation email has been sent.";
        }
        catch
        {
            TempData["InviteLink"] = inviteLink;
            TempData["InviteUser"] = vm.Email;
            TempData["Success"] = $"User {vm.Email} created. Email delivery failed — copy the invite link below.";
        }
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
        user.Department = vm.Department ?? string.Empty;
        await _userManager.UpdateAsync(user);

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (vm.SelectedRoles.Any())
            await _userManager.AddToRolesAsync(user, vm.SelectedRoles);

        TempData["Success"] = $"User {user.Email} updated.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BlockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(user);
        TempData["Success"] = $"{user.FullName} has been blocked.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        user.LockoutEnd = null;
        await _userManager.UpdateAsync(user);
        TempData["Success"] = $"{user.FullName} has been unblocked.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUserLink(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var link = await GenerateSetPasswordLinkAsync(user);
        var isInvite = !user.EmailConfirmed;
        try
        {
            if (isInvite)
                await _emailService.SendInviteAsync(user.Email!, user.FullName, link);
            else
                await _emailService.SendPasswordResetAsync(user.Email!, user.FullName, link);

            TempData["Success"] = isInvite
                ? $"Invite email resent to {user.Email}."
                : $"Password reset email sent to {user.Email}.";
        }
        catch
        {
            TempData["InviteLink"] = link;
            TempData["InviteUser"] = user.Email;
            TempData["Success"] = $"Email delivery failed — copy the link below to share manually.";
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == id)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Users));
        }
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        await _userManager.DeleteAsync(user);
        TempData["Success"] = $"User {user.Email} deleted.";
        return RedirectToAction(nameof(Users));
    }

    private async Task<string> GenerateSetPasswordLinkAsync(ApplicationUser user)
    {
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return Url.Action("SetPassword", "Account",
            new { userId = user.Id, token },
            Request.Scheme)!;
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
