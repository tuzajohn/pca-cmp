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

[Authorize(Policy = "Module:ChangeManagement")]
public class ChangeRequestsController : Controller
{
    private readonly IChangeRequestService _crService;
    private readonly IApprovalService _approvalService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAttachmentService _attachmentService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ChangeRequestsController> _logger;

    public ChangeRequestsController(IChangeRequestService crService, IApprovalService approvalService,
        UserManager<ApplicationUser> userManager, IAttachmentService attachmentService, 
        IEmailService emailService, ILogger<ChangeRequestsController> logger)
    {
        _crService = crService;
        _approvalService = approvalService;
        _userManager = userManager;
        _attachmentService = attachmentService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? status, int page = 1, int pageSize = 25)
    {
        var user = await _userManager.GetUserAsync(User);
        var userId = User.IsInRole("Admin") ? null : user!.Id;
        var result = await _crService.GetPagedAsync(userId, status, page, pageSize);

        ViewBag.StatusFilter = status;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_ChangeRequestList", result);

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Data(
        int page = 1, int pageSize = 25,
        string? sortCol = null, string? sortDir = "desc",
        string? status = null)
    {
        var user = await _userManager.GetUserAsync(User);
        var userId = User.IsInRole("Admin") ? null : user!.Id;
        var result = await _crService.GetPagedAsync(userId, status, page, pageSize, sortCol, sortDir);
        int totalPages = result.PageSize > 0 ? (int)Math.Ceiling((double)result.TotalCount / result.PageSize) : 1;

        return Json(new {
            items = result.Collection.Select(cr => new {
                id            = cr.Id,
                serial        = string.IsNullOrEmpty(cr.SerialNumber) ? $"#{cr.Id}" : cr.SerialNumber,
                title         = cr.Title,
                requestedBy   = cr.RequestedBy?.FullName ?? "",
                type          = cr.Type.ToString(),
                status        = cr.Status.ToString(),
                priority      = cr.Priority.ToString(),
                targetDate    = cr.TargetDate?.ToString("dd MMM yyyy") ?? "",
                targetOverdue = cr.TargetDate.HasValue && cr.TargetDate.Value < DateTime.Today
                               && cr.Status is not ChangeStatus.Closed and not ChangeStatus.Implemented,
                createdAt     = cr.CreatedAt.ToString("dd MMM yyyy")
            }),
            totalCount  = result.TotalCount,
            currentPage = result.CurrentPage,
            totalPages
        });
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
        {
            await _approvalService.InitiateApprovalFlowAsync("ChangeRequest", cr.Id, cr.Type.ToString(), user!.Id);

            // Notify first approver
            try
            {
                var firstStep = await _approvalService.GetNextPendingStepAsync("ChangeRequest", cr.Id);
                if (firstStep?.Approver != null && !string.IsNullOrEmpty(firstStep.Approver.Email))
                {
                    var entityLabel = $"Change Request {cr.SerialNumber} - {cr.Title}";
                    var viewLink = Url.Action(nameof(Details), "ChangeRequests", new { id = cr.Id }, Request.Scheme);

                    await _emailService.SendApprovalRequestAsync(
                        firstStep.Approver.Email,
                        firstStep.Approver.FullName,
                        entityLabel,
                        firstStep.RoleName ?? "Approver",
                        viewLink ?? ""
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send approval notification for ChangeRequest {Id}", cr.Id);
            }
        }

        TempData["Success"] = "Change request created successfully.";
        return RedirectToAction(nameof(Details), new { id = cr.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var cr = await _crService.GetByIdAsync(id);
        if (cr == null) return NotFound();
        ViewBag.ApprovalSteps = await _approvalService.GetStepsForEntityAsync("ChangeRequest", id);
        ViewBag.ActiveFlow    = await _approvalService.GetActiveFlowAsync("ChangeRequest", id);
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

            // Notify first approver
            try
            {
                var firstStep = await _approvalService.GetNextPendingStepAsync("ChangeRequest", cr.Id);
                if (firstStep?.Approver != null && !string.IsNullOrEmpty(firstStep.Approver.Email))
                {
                    var entityLabel = $"Change Request {cr.SerialNumber} - {cr.Title}";
                    var viewLink = Url.Action(nameof(Details), "ChangeRequests", new { id = cr.Id }, Request.Scheme);

                    await _emailService.SendApprovalRequestAsync(
                        firstStep.Approver.Email,
                        firstStep.Approver.FullName,
                        entityLabel,
                        firstStep.RoleName ?? "Approver",
                        viewLink ?? ""
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send approval notification for ChangeRequest {Id}", cr.Id);
            }

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
