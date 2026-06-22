using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Approvals.Models;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Identity.Models;
using PCA.Modules.Incidents.Models;
using PCA.Modules.Incidents.Services;
using PCA.Shared.Enums;
using PCA.Web.Models;
using PCA.Web.Services;

namespace PCA.Web.Controllers;

[Authorize(Policy = "Module:Incidents")]
public class IncidentsController : Controller
{
    private readonly IIncidentService _incidentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAttachmentService _attachmentService;
    private readonly IApprovalService _approvalService;
    private readonly IEmailService _emailService;
    private readonly ILogger<IncidentsController> _logger;

    public IncidentsController(IIncidentService incidentService, UserManager<ApplicationUser> userManager,
        IAttachmentService attachmentService, IApprovalService approvalService,
        IEmailService emailService, ILogger<IncidentsController> logger)
    {
        _incidentService = incidentService;
        _userManager = userManager;
        _attachmentService = attachmentService;
        _approvalService = approvalService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? status, string? severity, string? category, int page = 1, int pageSize = 20)
    {
        var user = await _userManager.GetUserAsync(User);
        var userId = User.IsInRole("Admin") ? null : user!.Id;
        var result = await _incidentService.GetPagedAsync(userId, status, severity, category, page, pageSize);

        ViewBag.StatusFilter = status;
        ViewBag.SeverityFilter = severity;
        ViewBag.CategoryFilter = category;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("_IncidentList", result);

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Data(
        int page = 1, int pageSize = 20,
        string? sortCol = null, string? sortDir = "desc",
        string? status = null, string? severity = null, string? category = null)
    {
        var user = await _userManager.GetUserAsync(User);
        var userId = User.IsInRole("Admin") ? null : user!.Id;
        var result = await _incidentService.GetPagedAsync(userId, status, severity, category, page, pageSize, sortCol, sortDir);

        return Json(new {
            items = result.Collection.Select(i => new {
                id         = i.Id,
                serial     = i.SerialNumber,
                title      = i.Title,
                category   = i.Category.ToString(),
                severity   = i.Severity.ToString(),
                priority   = i.Priority.ToString(),
                status     = i.Status.ToString(),
                assignedTo = i.AssignedTo?.FullName ?? "",
                detected   = i.DetectedAt.ToString("dd MMM yyyy")
            }),
            totalCount  = result.TotalCount,
            currentPage = result.CurrentPage,
            totalPages = result.TotalPages
        });
    }

    public IActionResult Create()
    {
        return View(new IncidentCreateViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IncidentCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _userManager.GetUserAsync(User);
        var incident = new Incident
        {
            Title = vm.Title,
            Description = vm.Description,
            Category = vm.Category,
            Severity = vm.Severity,
            Priority = vm.Priority,
            AffectedSystems = vm.AffectedSystems,
            ImpactDescription = vm.ImpactDescription,
            DetectedAt = vm.DetectedAt,
            ReportedById = user!.Id
        };

        await _incidentService.CreateAsync(incident);

        var autoTemplates = await _approvalService.GetAutoTriggerTemplatesAsync(AutoTriggerOn.OnSubmit, "Incident");
        if (autoTemplates.Any())
        {
            await _approvalService.InitiateApprovalFlowAsync("Incident", incident.Id, null, user!.Id);

            // Notify first approver
            try
            {
                var firstStep = await _approvalService.GetNextPendingStepAsync("Incident", incident.Id);
                if (firstStep?.Approver != null && !string.IsNullOrEmpty(firstStep.Approver.Email))
                {
                    var entityLabel = $"Incident {incident.SerialNumber} - {incident.Title}";
                    var viewLink = Url.Action(nameof(Details), "Incidents", new { id = incident.Id }, Request.Scheme);

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
                _logger.LogError(ex, "Failed to send approval notification for Incident {Id}", incident.Id);
            }
        }

        TempData["Success"] = $"Incident {incident.SerialNumber} created.";
        return RedirectToAction(nameof(Details), new { id = incident.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var incident = await _incidentService.GetByIdAsync(id);
        if (incident == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        ViewBag.CurrentUserId = user?.Id;
        ViewBag.IsAdmin = User.IsInRole("Admin");
        ViewBag.AllUsers = await _userManager.Users.ToListAsync();
        ViewBag.Attachments = await _attachmentService.GetForEntityAsync("Incident", id);
        ViewBag.ApprovalSteps = await _approvalService.GetStepsForEntityAsync("Incident", id);
        ViewBag.ActiveFlow = await _approvalService.GetActiveFlowAsync("Incident", id);
        return View(incident);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var incident = await _incidentService.GetByIdAsync(id);
        if (incident == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (incident.ReportedById != user!.Id && !User.IsInRole("Admin")) return Forbid();
        if (incident.Status == IncidentStatus.Closed)
        {
            TempData["Error"] = "Closed incidents cannot be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new IncidentEditViewModel
        {
            Id = incident.Id,
            Title = incident.Title,
            Description = incident.Description,
            Category = incident.Category,
            Severity = incident.Severity,
            Priority = incident.Priority,
            AffectedSystems = incident.AffectedSystems,
            ImpactDescription = incident.ImpactDescription,
            DetectedAt = incident.DetectedAt,
            LinkedChangeRequestId = incident.LinkedChangeRequestId
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(IncidentEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var incident = await _incidentService.GetByIdAsync(vm.Id);
        if (incident == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (incident.ReportedById != user!.Id && !User.IsInRole("Admin")) return Forbid();

        incident.Title = vm.Title;
        incident.Description = vm.Description;
        incident.Category = vm.Category;
        incident.Severity = vm.Severity;
        incident.Priority = vm.Priority;
        incident.AffectedSystems = vm.AffectedSystems;
        incident.ImpactDescription = vm.ImpactDescription;
        incident.DetectedAt = vm.DetectedAt;
        incident.LinkedChangeRequestId = vm.LinkedChangeRequestId;

        await _incidentService.UpdateAsync(incident);
        TempData["Success"] = "Incident updated.";
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, string? assigneeId)
    {
        var user = await _userManager.GetUserAsync(User);
        await _incidentService.AssignAsync(id, assigneeId, user!.Id);
        TempData["Success"] = "Incident assignment updated.";
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
        await _incidentService.AddUpdateAsync(id, user!.Id, content);
        TempData["Success"] = "Comment added.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string newStatus, string? comment)
    {
        if (!Enum.TryParse<IncidentStatus>(newStatus, out var status))
        {
            TempData["Error"] = "Invalid status.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var user = await _userManager.GetUserAsync(User);
        await _incidentService.UpdateStatusAsync(id, status, user!.Id, comment);
        TempData["Success"] = $"Status updated to {status}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Resolve(int id)
    {
        var incident = await _incidentService.GetByIdAsync(id);
        if (incident == null) return NotFound();
        if (incident.Status == IncidentStatus.Closed || incident.Status == IncidentStatus.Resolved)
        {
            TempData["Error"] = "Incident is already resolved or closed.";
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(new IncidentResolveViewModel { Id = id, SerialNumber = incident.SerialNumber, Title = incident.Title });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(IncidentResolveViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _userManager.GetUserAsync(User);
        await _incidentService.ResolveAsync(vm.Id, user!.Id, vm.RootCause, vm.ResolutionSummary);
        TempData["Success"] = "Incident marked as resolved.";
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Close(int id, string? comment)
    {
        var user = await _userManager.GetUserAsync(User);
        await _incidentService.CloseAsync(id, user!.Id, comment);
        TempData["Success"] = "Incident closed.";
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
            await _attachmentService.UploadAsync("Incident", id, file, user!.Id);
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
