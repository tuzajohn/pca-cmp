using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.AccessManagement.Models;
using PCA.Modules.AccessManagement.Services;
using PCA.Modules.Approvals.Models;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;
using PCA.Web.Models;
using PCA.Web.Services;

namespace PCA.Web.Controllers;

[Authorize(Policy = "Module:AccessManagement")]
public class AccessRequestsController : Controller
{
    private readonly IAccessManagementService _svc;
    private readonly IApprovalService _approvalService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAttachmentService _attachmentService;

    public AccessRequestsController(IAccessManagementService svc, IApprovalService approvalService,
        UserManager<ApplicationUser> userManager, IAttachmentService attachmentService)
    {
        _svc = svc;
        _approvalService = approvalService;
        _userManager = userManager;
        _attachmentService = attachmentService;
    }

    public async Task<IActionResult> Index(string? status, string? system, DateTime? from, DateTime? to, int page = 1, int pageSize = 25)
    {
        var user = await _userManager.GetUserAsync(User);
        var userId = User.IsInRole("Admin") ? null : user!.Id;
        var result = await _svc.GetAccessRequestsPagedAsync(userId, status, system, from, to, page, pageSize);

        ViewBag.StatusFilter = status;
        ViewBag.SystemFilter = system;
        ViewBag.FromFilter = from?.ToString("yyyy-MM-dd");
        ViewBag.ToFilter = to?.ToString("yyyy-MM-dd");

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_AccessRequestList", result);

        return View(result);
    }

    public IActionResult Create() => View(new AccessRequestCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AccessRequestCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _userManager.GetUserAsync(User);
        var req = new AccessRequest
        {
            RequestedById = user!.Id,
            EmployeeName = vm.EmployeeName,
            EmployeeId = vm.EmployeeId,
            Department = vm.Department,
            JobTitle = vm.JobTitle,
            SystemName = vm.SystemName,
            AccessType = vm.AccessType,
            AccessDetails = vm.AccessDetails,
            Justification = vm.Justification,
            RequestedByDate = vm.RequestedByDate,
            AccessExpiryDate = vm.AccessExpiryDate,
            IsPrivileged = vm.IsPrivileged
        };

        await _svc.CreateAccessRequestAsync(req);
        TempData["Success"] = "Access request created.";
        return RedirectToAction(nameof(Details), new { id = req.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var req = await _svc.GetAccessRequestByIdAsync(id);
        if (req == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        ViewBag.ApprovalSteps = await _approvalService.GetStepsForEntityAsync("AccessRequest", id);
        ViewBag.ActiveFlow = await _approvalService.GetActiveFlowAsync("AccessRequest", id);
        ViewBag.CurrentUserId = user?.Id;
        ViewBag.Attachments = await _attachmentService.GetForEntityAsync("AccessRequest", id);
        return View(req);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var req = await _svc.GetAccessRequestByIdAsync(id);
        if (req == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        if (req.RequestedById != user!.Id && !User.IsInRole("Admin")) return Forbid();
        if (req.Status != AccessRequestStatus.Draft)
        {
            TempData["Error"] = "Only draft access requests can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var vm = new AccessRequestEditViewModel
        {
            Id = req.Id,
            EmployeeName = req.EmployeeName,
            EmployeeId = req.EmployeeId,
            Department = req.Department,
            JobTitle = req.JobTitle,
            SystemName = req.SystemName,
            AccessType = req.AccessType,
            AccessDetails = req.AccessDetails,
            Justification = req.Justification,
            RequestedByDate = req.RequestedByDate,
            AccessExpiryDate = req.AccessExpiryDate,
            IsPrivileged = req.IsPrivileged
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AccessRequestEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var req = await _svc.GetAccessRequestByIdAsync(vm.Id);
        if (req == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        if (req.RequestedById != user!.Id && !User.IsInRole("Admin")) return Forbid();

        req.EmployeeName = vm.EmployeeName;
        req.EmployeeId = vm.EmployeeId;
        req.Department = vm.Department;
        req.JobTitle = vm.JobTitle;
        req.SystemName = vm.SystemName;
        req.AccessType = vm.AccessType;
        req.AccessDetails = vm.AccessDetails;
        req.Justification = vm.Justification;
        req.RequestedByDate = vm.RequestedByDate;
        req.AccessExpiryDate = vm.AccessExpiryDate;
        req.IsPrivileged = vm.IsPrivileged;

        await _svc.UpdateAccessRequestAsync(req);
        TempData["Success"] = "Access request updated.";
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var req = await _svc.GetAccessRequestByIdAsync(id);
        if (req == null) return NotFound();

        var success = await _svc.SubmitAccessRequestAsync(id, user!.Id);
        if (success)
        {
            await _approvalService.InitiateApprovalFlowAsync("AccessRequest", id,
                req.IsPrivileged ? "Privileged" : "Standard", user.Id);
            TempData["Success"] = "Access request submitted for approval.";
        }
        else
        {
            TempData["Error"] = "Unable to submit access request.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarkProvisioned(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var success = await _svc.MarkProvisionedAsync(id, user!.Id);
        TempData[success ? "Success" : "Error"] = success
            ? "Access request marked as provisioned."
            : "Unable to mark as provisioned. Ensure request is Approved.";
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
        await _svc.AddAccessRequestCommentAsync(id, user!.Id, content);
        TempData["Success"] = "Comment added.";
        return RedirectToAction(nameof(Details), new { id });
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
            await _attachmentService.UploadAsync("AccessRequest", id, file, user!.Id);
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
