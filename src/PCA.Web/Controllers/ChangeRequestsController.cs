using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Approvals.Services;
using PCA.Modules.ChangeManagement.Models;
using PCA.Modules.ChangeManagement.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize]
public class ChangeRequestsController : Controller
{
    private readonly IChangeRequestService _crService;
    private readonly IApprovalService _approvalService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChangeRequestsController(IChangeRequestService crService, IApprovalService approvalService, UserManager<ApplicationUser> userManager)
    {
        _crService = crService;
        _approvalService = approvalService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        List<ChangeRequest> crs;
        if (User.IsInRole("Admin"))
            crs = await _crService.GetAllAsync();
        else
            crs = await _crService.GetByUserAsync(user!.Id);
        return View(crs);
    }

    public IActionResult Create()
    {
        return View(new ChangeRequestCreateViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChangeRequestCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _userManager.GetUserAsync(User);
        var cr = new ChangeRequest
        {
            Title = vm.Title,
            Description = vm.Description,
            Type = vm.Type,
            Priority = vm.Priority,
            TargetDate = vm.TargetDate,
            ProposedImplementationWindow = vm.ProposedImplementationWindow,
            SystemsAffected = vm.SystemsAffected,
            RiskDescription = vm.RiskDescription,
            ImpactOnUsers = vm.ImpactOnUsers,
            RollbackPlan = vm.RollbackPlan,
            RollbackTrigger = vm.RollbackTrigger,
            TestingSteps = vm.TestingSteps,
            StagingTested = vm.StagingTested,
            RequestedById = user!.Id,
            SerialNumber = string.Empty
        };

        await _crService.CreateAsync(cr);
        TempData["Success"] = "Change request created successfully.";
        return RedirectToAction(nameof(Details), new { id = cr.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var cr = await _crService.GetByIdAsync(id);
        if (cr == null) return NotFound();
        ViewBag.ApprovalSteps = await _approvalService.GetStepsForRequestAsync(id);
        var user = await _userManager.GetUserAsync(User);
        ViewBag.CurrentUserId = user?.Id;
        return View(cr);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var cr = await _crService.GetByIdAsync(id);
        if (cr == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        if (cr.RequestedById != user!.Id && !User.IsInRole("Admin")) return Forbid();
        if (cr.Status != ChangeStatus.Draft) 
        {
            TempData["Error"] = "Only draft change requests can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new ChangeRequestEditViewModel
        {
            Id = cr.Id,
            Title = cr.Title,
            Description = cr.Description,
            Type = cr.Type,
            Priority = cr.Priority,
            TargetDate = cr.TargetDate,
            ProposedImplementationWindow = cr.ProposedImplementationWindow,
            SystemsAffected = cr.SystemsAffected,
            RiskDescription = cr.RiskDescription,
            ImpactOnUsers = cr.ImpactOnUsers,
            RollbackPlan = cr.RollbackPlan,
            RollbackTrigger = cr.RollbackTrigger,
            TestingSteps = cr.TestingSteps,
            StagingTested = cr.StagingTested,
            ImplementationNotes = cr.ImplementationNotes
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ChangeRequestEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var cr = await _crService.GetByIdAsync(vm.Id);
        if (cr == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        if (cr.RequestedById != user!.Id && !User.IsInRole("Admin")) return Forbid();

        cr.Title = vm.Title;
        cr.Description = vm.Description;
        cr.Type = vm.Type;
        cr.Priority = vm.Priority;
        cr.TargetDate = vm.TargetDate;
        cr.ProposedImplementationWindow = vm.ProposedImplementationWindow;
        cr.SystemsAffected = vm.SystemsAffected;
        cr.RiskDescription = vm.RiskDescription;
        cr.ImpactOnUsers = vm.ImpactOnUsers;
        cr.RollbackPlan = vm.RollbackPlan;
        cr.RollbackTrigger = vm.RollbackTrigger;
        cr.TestingSteps = vm.TestingSteps;
        cr.StagingTested = vm.StagingTested;
        cr.ImplementationNotes = vm.ImplementationNotes;

        await _crService.UpdateAsync(cr);
        TempData["Success"] = "Change request updated successfully.";
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var cr = await _crService.GetByIdAsync(id);
        if (cr == null) return NotFound();

        var success = await _crService.SubmitAsync(id, user!.Id);
        if (success)
        {
            await _approvalService.InitiateApprovalFlowAsync(id, cr.Type);
            TempData["Success"] = "Change request submitted for approval.";
        }
        else
        {
            TempData["Error"] = "Unable to submit change request.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Comment cannot be empty.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var user = await _userManager.GetUserAsync(User);
        await _crService.AddCommentAsync(id, user!.Id, content);
        TempData["Success"] = "Comment added.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarkImplemented(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        await _crService.UpdateStatusAsync(id, ChangeStatus.Implemented, user!.Id);
        TempData["Success"] = "Change request marked as implemented.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Close(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        await _crService.UpdateStatusAsync(id, ChangeStatus.Closed, user!.Id);
        TempData["Success"] = "Change request closed.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
