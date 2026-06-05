using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Approvals.Models;
using PCA.Modules.Approvals.Services;
using PCA.Modules.ChangeManagement.Models;
using PCA.Modules.ChangeManagement.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;
using PCA.Web.Models;
using PCA.Web.Services;

namespace PCA.Web.Controllers;

[Authorize]
public class ChangeRequestsController : Controller
{
    private readonly IChangeRequestService _crService;
    private readonly IApprovalService _approvalService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAttachmentService _attachmentService;

    public ChangeRequestsController(IChangeRequestService crService, IApprovalService approvalService,
        UserManager<ApplicationUser> userManager, IAttachmentService attachmentService)
    {
        _crService = crService;
        _approvalService = approvalService;
        _userManager = userManager;
        _attachmentService = attachmentService;
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

        // Auto-trigger approval if a matching template is configured for OnSubmit
        var autoTemplates = await _approvalService.GetAutoTriggerTemplatesAsync(AutoTriggerOn.OnSubmit, "ChangeRequest");
        var matchingTemplate = autoTemplates.FirstOrDefault(t => t.EntitySubType == null || t.EntitySubType == cr.Type.ToString());
        if (matchingTemplate != null)
            await _approvalService.InitiateApprovalFlowAsync("ChangeRequest", cr.Id, cr.Type.ToString(), user!.Id);

        TempData["Success"] = "Change request created successfully.";
        return RedirectToAction(nameof(Details), new { id = cr.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var cr = await _crService.GetByIdAsync(id);
        if (cr == null) return NotFound();
        ViewBag.ApprovalSteps = await _approvalService.GetStepsForEntityAsync("ChangeRequest", id);
        var user = await _userManager.GetUserAsync(User);
        ViewBag.CurrentUserId = user?.Id;
        ViewBag.Attachments = await _attachmentService.GetForEntityAsync("ChangeRequest", id);
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
            await _approvalService.InitiateApprovalFlowAsync("ChangeRequest", id, cr.Type.ToString(), user!.Id);
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

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Pir(int id)
    {
        var cr = await _crService.GetByIdAsync(id);
        if (cr == null) return NotFound();
        if (cr.Status != ChangeStatus.Implemented)
        {
            TempData["Error"] = "Post-implementation review is only available for implemented changes.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var vm = new PirViewModel
        {
            ChangeRequestId = cr.Id,
            SerialNumber = cr.SerialNumber,
            Title = cr.Title,
            ActualDate = DateTime.Today
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Pir(PirViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _userManager.GetUserAsync(User);
        var success = await _crService.SubmitPirAsync(
            vm.ChangeRequestId, user!.Id,
            vm.Outcome, vm.ActualDate,
            vm.IssuesEncountered, vm.LessonsLearned,
            vm.RollbackExecuted, vm.ClosureNotes);

        if (success)
            TempData["Success"] = "Post-implementation review submitted. Change request closed.";
        else
            TempData["Error"] = "Unable to submit PIR. Ensure the CR is in Implemented status.";

        return RedirectToAction(nameof(Details), new { id = vm.ChangeRequestId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(int id, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a file.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var user = await _userManager.GetUserAsync(User);
        try
        {
            await _attachmentService.UploadAsync("ChangeRequest", id, file, user!.Id);
            TempData["Success"] = "Attachment uploaded.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId)
    {
        var user = await _userManager.GetUserAsync(User);
        try
        {
            await _attachmentService.DeleteAsync(attachmentId, user!.Id, User.IsInRole("Admin"));
            TempData["Success"] = "Attachment deleted.";
        }
        catch (UnauthorizedAccessException)
        {
            TempData["Error"] = "You cannot delete this attachment.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId)
    {
        var result = await _attachmentService.GetFileAsync(attachmentId);
        if (result == null) return NotFound();
        var (filePath, contentType, fileName) = result.Value;
        return PhysicalFile(filePath, contentType, fileName);
    }
}
