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
public class ServerRoomAccessController : Controller
{
    private readonly IAccessManagementService _svc;
    private readonly IApprovalService _approvalService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAttachmentService _attachmentService;

    public ServerRoomAccessController(IAccessManagementService svc, IApprovalService approvalService,
        UserManager<ApplicationUser> userManager, IAttachmentService attachmentService)
    {
        _svc = svc;
        _approvalService = approvalService;
        _userManager = userManager;
        _attachmentService = attachmentService;
    }

    public async Task<IActionResult> Index(string? status, int page = 1, int pageSize = 25)
    {
        var result = await _svc.GetServerRoomRequestsPagedAsync(status, page, pageSize);
        ViewBag.StatusFilter = status;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_ServerRoomList", result);

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Data(
        int draw, int start, int length,
        string? status,
        [FromQuery(Name = "order[0][column]")] int orderCol = 4,
        [FromQuery(Name = "order[0][dir]")] string orderDir = "desc")
    {
        string[] cols = { "serial", "visitor", "purpose", "entry", "status" };
        var sortCol = orderCol < cols.Length ? cols[orderCol] : null;
        int page = length > 0 ? (start / length) + 1 : 1;

        var result = await _svc.GetServerRoomRequestsPagedAsync(status, page, length, sortCol, orderDir);

        return Json(new {
            draw,
            recordsTotal    = result.TotalCount,
            recordsFiltered = result.TotalCount,
            data = result.Items.Select(r => new {
                id             = r.Id,
                serial         = r.SerialNumber,
                visitorName    = r.VisitorName,
                visitorCompany = r.VisitorCompany ?? "",
                isExternal     = r.IsExternal,
                purpose        = r.Purpose,
                plannedEntry   = r.PlannedEntryDateTime.ToString("dd MMM yyyy HH:mm"),
                plannedExit    = r.PlannedExitDateTime?.ToString("dd MMM yyyy HH:mm") ?? "",
                status         = r.Status.ToString()
            })
        });
    }

    public IActionResult Create() => View(new ServerRoomCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServerRoomCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _userManager.GetUserAsync(User);
        var req = new ServerRoomAccessRequest
        {
            RequestedById = user!.Id,
            VisitorName = vm.VisitorName,
            VisitorTitle = vm.VisitorTitle,
            VisitorCompany = vm.VisitorCompany,
            IsExternal = vm.IsExternal,
            Purpose = vm.Purpose,
            PlannedEntryDateTime = vm.PlannedEntryDateTime,
            PlannedExitDateTime = vm.PlannedExitDateTime,
            WrittenRequestReference = vm.WrittenRequestReference,
            EscortedBy = vm.EscortedBy
        };

        await _svc.CreateServerRoomRequestAsync(req);
        TempData["Success"] = "Server room access request created.";
        return RedirectToAction(nameof(Details), new { id = req.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var req = await _svc.GetServerRoomRequestByIdAsync(id);
        if (req == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        ViewBag.ApprovalSteps = await _approvalService.GetStepsForEntityAsync("ServerRoomAccessRequest", id);
        ViewBag.ActiveFlow = await _approvalService.GetActiveFlowAsync("ServerRoomAccessRequest", id);
        ViewBag.CurrentUserId = user?.Id;
        ViewBag.Attachments = await _attachmentService.GetForEntityAsync("ServerRoomAccessRequest", id);
        return View(req);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var req = await _svc.GetServerRoomRequestByIdAsync(id);
        if (req == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        if (req.RequestedById != user!.Id && !User.IsInRole("Admin")) return Forbid();
        if (req.Status != ServerRoomAccessStatus.Draft)
        {
            TempData["Error"] = "Only draft requests can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var vm = new ServerRoomEditViewModel
        {
            Id = req.Id,
            VisitorName = req.VisitorName,
            VisitorTitle = req.VisitorTitle,
            VisitorCompany = req.VisitorCompany,
            IsExternal = req.IsExternal,
            Purpose = req.Purpose,
            PlannedEntryDateTime = req.PlannedEntryDateTime,
            PlannedExitDateTime = req.PlannedExitDateTime,
            WrittenRequestReference = req.WrittenRequestReference,
            EscortedBy = req.EscortedBy
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ServerRoomEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var req = await _svc.GetServerRoomRequestByIdAsync(vm.Id);
        if (req == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        if (req.RequestedById != user!.Id && !User.IsInRole("Admin")) return Forbid();

        req.VisitorName = vm.VisitorName;
        req.VisitorTitle = vm.VisitorTitle;
        req.VisitorCompany = vm.VisitorCompany;
        req.IsExternal = vm.IsExternal;
        req.Purpose = vm.Purpose;
        req.PlannedEntryDateTime = vm.PlannedEntryDateTime;
        req.PlannedExitDateTime = vm.PlannedExitDateTime;
        req.WrittenRequestReference = vm.WrittenRequestReference;
        req.EscortedBy = vm.EscortedBy;

        await _svc.UpdateServerRoomRequestAsync(req);
        TempData["Success"] = "Request updated.";
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var success = await _svc.SubmitServerRoomRequestAsync(id, user!.Id);
        if (success)
        {
            await _approvalService.InitiateApprovalFlowAsync("ServerRoomAccessRequest", id, null, user.Id);
            TempData["Success"] = "Request submitted for approval.";
        }
        else
        {
            TempData["Error"] = "Unable to submit request.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RecordEntry(int id, DateTime? actualEntry, DateTime? actualExit)
    {
        await _svc.RecordActualEntryExitAsync(id, actualEntry, actualExit);
        TempData["Success"] = "Entry/exit times recorded.";
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
        await _svc.AddServerRoomCommentAsync(id, user!.Id, content);
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
            await _attachmentService.UploadAsync("ServerRoomAccessRequest", id, file, user!.Id);
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
