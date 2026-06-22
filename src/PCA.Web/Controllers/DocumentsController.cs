using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using PCA.Modules.Approvals.Models;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Documents.Models;
using PCA.Modules.Documents.Services;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;
using PCA.Web.Models;
using PCA.Web.Services;

namespace PCA.Web.Controllers;

[Authorize(Policy = "Module:Documents")]
public class DocumentsController : Controller
{
    private readonly IDocumentService _docService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApprovalService _approvalService;
    private readonly IApprovalWorkflowRegistry _workflowRegistry;
    private readonly IEmailService _emailService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IDocumentService docService, UserManager<ApplicationUser> userManager,
        IApprovalService approvalService, IApprovalWorkflowRegistry workflowRegistry,
        IEmailService emailService, ILogger<DocumentsController> logger)
    {
        _docService = docService;
        _userManager = userManager;
        _approvalService = approvalService;
        _workflowRegistry = workflowRegistry;
        _emailService = emailService;
        _logger = logger;
    }

    // ── Index ─────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(int? folderId, string? q, string? status, string? dueForReview)
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = (await _userManager.GetRolesAsync(user!)).ToList();
        var isAdmin = User.IsInRole("Admin");

        var folderTree = await _docService.GetFolderTreeAsync();

        List<Document> docs;
        if (!string.IsNullOrWhiteSpace(q))
            docs = await _docService.SearchAsync(q);
        else
            docs = await _docService.GetAllAsync(folderId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DocumentStatus>(status, out var statusEnum))
            docs = docs.Where(d => d.Status == statusEnum).ToList();

        var dueForReviewFilter = dueForReview == "true";
        if (dueForReviewFilter)
        {
            var cutoff = DateTime.UtcNow.AddDays(7);
            docs = docs.Where(d => d.NextReviewDate.HasValue && d.NextReviewDate.Value <= cutoff).ToList();
        }

        // Filter by IAM — admins see all, others need at least Viewer
        if (!isAdmin)
        {
            var accessible = new List<Document>();
            foreach (var doc in docs)
            {
                var access = await _docService.GetEffectiveAccessAsync(doc.Id, doc.FolderId, user!.Id, roles);
                if (access.HasValue) accessible.Add(doc);
            }
            docs = accessible;
        }

        AccessLevel? folderAccess = null;
        if (folderId.HasValue)
            folderAccess = isAdmin ? AccessLevel.Manager
                : await _docService.GetEffectiveAccessAsync(null, folderId, user!.Id, roles);

        var flat = new List<FlatFolderItem>();
        FlattenFolderTree(folderTree.Where(f => f.ParentId == null).OrderBy(f => f.Name), 0, flat);

        var vm = new DocumentIndexViewModel
        {
            FolderTree = folderTree,
            FlatFolderTree = flat,
            Documents = docs,
            ActiveFolderId = folderId,
            SearchQuery = q,
            StatusFilter = status,
            DueForReviewFilter = dueForReviewFilter,
            EffectiveAccess = isAdmin ? AccessLevel.Manager : folderAccess
        };

        return View(vm);
    }

    // ── Index Data ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> IndexData(
        int page = 1, int pageSize = 20,
        string? sortCol = null, string? sortDir = "asc",
        int? folderId = null, string? q = null, string? status = null, bool dueForReview = false)
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = (await _userManager.GetRolesAsync(user!)).ToList();
        var isAdmin = User.IsInRole("Admin");

        List<PCA.Modules.Documents.Models.Document> docs;
        if (!string.IsNullOrWhiteSpace(q))
            docs = await _docService.SearchAsync(q);
        else
            docs = await _docService.GetAllAsync(folderId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DocumentStatus>(status, out var statusEnum))
            docs = docs.Where(d => d.Status == statusEnum).ToList();

        if (dueForReview)
        {
            var cutoff = DateTime.UtcNow.AddDays(7);
            docs = docs.Where(d => d.NextReviewDate.HasValue && d.NextReviewDate.Value <= cutoff).ToList();
        }

        if (!isAdmin)
        {
            var accessible = new List<PCA.Modules.Documents.Models.Document>();
            foreach (var doc in docs)
            {
                var access = await _docService.GetEffectiveAccessAsync(doc.Id, doc.FolderId, user!.Id, roles);
                if (access.HasValue) accessible.Add(doc);
            }
            docs = accessible;
        }

        var sorted = sortCol switch {
            "title"      => sortDir == "asc" ? docs.OrderBy(d => d.Title).ToList() : docs.OrderByDescending(d => d.Title).ToList(),
            "status"     => sortDir == "asc" ? docs.OrderBy(d => d.Status).ToList() : docs.OrderByDescending(d => d.Status).ToList(),
            "reviewDate" => sortDir == "asc" ? docs.OrderBy(d => d.NextReviewDate).ToList() : docs.OrderByDescending(d => d.NextReviewDate).ToList(),
            _            => docs.OrderBy(d => d.Title).ToList()
        };

        var totalCount = sorted.Count;
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        int totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

        return Json(new {
            items = items.Select(d => new {
                id         = d.Id,
                title      = d.Title,
                status     = d.Status.ToString(),
                reviewDate = d.NextReviewDate.HasValue ? d.NextReviewDate.Value.ToString("dd MMM yyyy") : "",
                isDueForReview = d.NextReviewDate.HasValue && d.NextReviewDate.Value <= DateTime.UtcNow.AddDays(7)
            }),
            totalCount,
            currentPage = page,
            totalPages = totalPages
        });
    }

    // ── Details ───────────────────────────────────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        var doc = await _docService.GetByIdAsync(id);
        if (doc == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        var roles = (await _userManager.GetRolesAsync(user!)).ToList();
        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin)
        {
            var access = await _docService.GetEffectiveAccessAsync(doc.Id, doc.FolderId, user!.Id, roles);
            if (!access.HasValue) return Forbid();
            ViewBag.Access = access.Value;
        }
        else
        {
            ViewBag.Access = AccessLevel.Manager;
        }

        ViewBag.IsOwner = doc.OwnerId == user!.Id;
        ViewBag.IsAdmin = isAdmin;
        ViewBag.CurrentUserId = user!.Id;
        ViewBag.FolderPermissions = doc.FolderId.HasValue
            ? await _docService.GetFolderPermissionsAsync(doc.FolderId.Value)
            : new List<FolderPermission>();
        ViewBag.DocumentPermissions = await _docService.GetDocumentPermissionsAsync(doc.Id);
        ViewBag.AllUsers = await _userManager.Users.ToListAsync();
        ViewBag.ApprovalSteps = await _approvalService.GetStepsForEntityAsync("Document", id);
        ViewBag.ActiveFlow    = await _approvalService.GetActiveFlowAsync("Document", id);
        ViewBag.ReviewHistory = await _docService.GetReviewHistoryAsync(id);
        ViewBag.Attachments = await HttpContext.RequestServices
            .GetRequiredService<PCA.Web.Services.IAttachmentService>()
            .GetForEntityAsync("Document", id);

        return View(doc);
    }

    // ── Versions Data ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> VersionsData(int id, int page = 1, int pageSize = 20)
    {
        var doc = await _docService.GetByIdAsync(id);
        if (doc == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        var roles = (await _userManager.GetRolesAsync(user!)).ToList();
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            var access = await _docService.GetEffectiveAccessAsync(doc.Id, doc.FolderId, user!.Id, roles);
            if (!access.HasValue) return Forbid();
        }

        var result = await _docService.GetVersionsPagedAsync(id, page, pageSize);
        var canManage = isAdmin || doc.OwnerId == user!.Id;

        return Json(new {
            items = result.Collection.Select(v => new {
                versionId   = v.Id,
                versionNumber = v.VersionNumber,
                fileName    = v.OriginalFileName,
                fileSize    = PCA.Web.Helpers.DocumentViewHelpers.FormatSize(v.FileSizeBytes),
                fileExt     = System.IO.Path.GetExtension(v.OriginalFileName).TrimStart('.').ToUpper(),
                uploadedBy  = v.UploadedBy?.FullName ?? "—",
                uploadedAt  = v.UploadedAt.ToString("dd MMM yyyy"),
                changeNotes = v.ChangeNotes ?? "",
                isCurrent   = v.IsCurrentVersion,
                canManage   = canManage && !v.IsCurrentVersion
            }),
            totalCount  = result.TotalCount,
            currentPage = result.CurrentPage,
            totalPages = result.TotalPages
        });
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Create(int? folderId)
    {
        await AssertCanContribute(null, folderId);
        ViewBag.Folders = await _docService.GetFolderTreeAsync();
        return View(new DocumentCreateViewModel { FolderId = folderId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DocumentCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Folders = await _docService.GetFolderTreeAsync();
            return View(vm);
        }

        var user = await _userManager.GetUserAsync(User);
        var doc = new Document
        {
            Title = vm.Title,
            Description = vm.Description,
            FolderId = vm.FolderId,
            Tags = vm.Tags,
            OwnerId = user!.Id,
            CreatedById = user.Id,
            Status = DocumentStatus.Draft,
            ReviewPeriodDays = vm.ReviewPeriodDays,
            NextReviewDate = vm.NextReviewDate
        };

        await _docService.CreateAsync(doc, vm.File!, vm.ChangeNotes ?? "Initial version");
        TempData["Success"] = "Document created successfully.";
        return RedirectToAction(nameof(Details), new { id = doc.Id });
    }

    // ── Edit ──────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Edit(int id)
    {
        var doc = await _docService.GetByIdAsync(id);
        if (doc == null) return NotFound();
        await AssertCanManage(doc);

        var users = await _userManager.Users.ToListAsync();
        ViewBag.Users = users;
        ViewBag.Folders = await _docService.GetFolderTreeAsync();

        return View(new DocumentEditViewModel
        {
            Id = doc.Id,
            Title = doc.Title,
            Description = doc.Description,
            FolderId = doc.FolderId,
            Tags = doc.Tags,
            Status = doc.Status,
            OwnerId = doc.OwnerId,
            ReviewPeriodDays = doc.ReviewPeriodDays,
            NextReviewDate = doc.NextReviewDate
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DocumentEditViewModel vm)
    {
        var doc = await _docService.GetByIdAsync(vm.Id);
        if (doc == null) return NotFound();
        await AssertCanManage(doc);

        if (!ModelState.IsValid)
        {
            ViewBag.Users = await _userManager.Users.ToListAsync();
            ViewBag.Folders = await _docService.GetFolderTreeAsync();
            return View(vm);
        }

        doc.Title = vm.Title;
        doc.Description = vm.Description;
        doc.FolderId = vm.FolderId;
        doc.Tags = vm.Tags;
        doc.Status = vm.Status;
        doc.OwnerId = vm.OwnerId;
        doc.ReviewPeriodDays = vm.ReviewPeriodDays;
        doc.NextReviewDate = vm.NextReviewDate;

        await _docService.UpdateMetadataAsync(doc);
        TempData["Success"] = "Document updated.";
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    // ── Upload Version ────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadVersion(DocumentUploadVersionViewModel vm)
    {
        var doc = await _docService.GetByIdAsync(vm.DocumentId);
        if (doc == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        var roles = (await _userManager.GetRolesAsync(user!)).ToList();
        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin)
        {
            var access = await _docService.GetEffectiveAccessAsync(doc.Id, doc.FolderId, user!.Id, roles);
            if (!access.HasValue || access < AccessLevel.Contributor)
            {
                TempData["Error"] = "You do not have permission to upload versions to this document.";
                return RedirectToAction(nameof(Details), new { id = vm.DocumentId });
            }
        }

        if (vm.File == null || vm.File.Length == 0)
        {
            TempData["Error"] = "Please select a file to upload.";
            return RedirectToAction(nameof(Details), new { id = vm.DocumentId });
        }

        try
        {
            await _docService.UploadVersionAsync(vm.DocumentId, vm.File, vm.ChangeNotes ?? string.Empty, user!.Id);
            TempData["Success"] = "New version uploaded.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        // Auto-trigger on new version upload
        await TriggerDocumentApprovalAsync(vm.DocumentId, AutoTriggerOn.OnNewVersion, user!.Id);

        return RedirectToAction(nameof(Details), new { id = vm.DocumentId });
    }

    // ── Download ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> Download(int versionId)
    {
        var user = await _userManager.GetUserAsync(User);
        var result = await _docService.DownloadAsync(versionId, user!.Id);
        if (result == null) return NotFound();

        var (stream, contentType, fileName) = result.Value;
        return File(stream, contentType, fileName);
    }

    // ── Set Current Version ───────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCurrentVersion(int versionId, int documentId)
    {
        var doc = await _docService.GetByIdAsync(documentId);
        if (doc == null) return NotFound();
        await AssertCanManage(doc);

        await _docService.SetCurrentVersionAsync(versionId, (await _userManager.GetUserAsync(User))!.Id);
        TempData["Success"] = "Current version updated.";
        return RedirectToAction(nameof(Details), new { id = documentId });
    }

    // ── Delete Version ────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVersion(int versionId, int documentId)
    {
        var doc = await _docService.GetByIdAsync(documentId);
        if (doc == null) return NotFound();
        await AssertCanManage(doc);

        var ok = await _docService.DeleteVersionAsync(versionId, (await _userManager.GetUserAsync(User))!.Id);
        TempData[ok ? "Success" : "Error"] = ok ? "Version deleted." : "Cannot delete the only version of a document.";
        return RedirectToAction(nameof(Details), new { id = documentId });
    }

    // ── Retire ────────────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Retire(int id)
    {
        var doc = await _docService.GetByIdAsync(id);
        if (doc == null) return NotFound();
        await AssertCanManage(doc);

        var user = await _userManager.GetUserAsync(User);

        // If an OnRetire template exists, initiate approval instead of retiring immediately
        var retireTemplates = await _approvalService.GetAutoTriggerTemplatesAsync(AutoTriggerOn.OnRetire, "Document");
        if (retireTemplates.Any())
        {
            await _approvalService.InitiateApprovalFlowAsync("Document", id, null, user!.Id);
            var workflow = _workflowRegistry.Resolve("Document");
            await workflow.OnFlowInitiatedAsync(id, user!.Id, HttpContext.RequestServices);

            // Notify first approver
            try
            {
                var firstStep = await _approvalService.GetNextPendingStepAsync("Document", id);
                if (firstStep?.Approver != null && !string.IsNullOrEmpty(firstStep.Approver.Email))
                {
                    var entityLabel = $"Document {doc.SerialNumber} - {doc.Title}";
                    var viewLink = Url.Action(nameof(Details), "Documents", new { id }, Request.Scheme);

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
                _logger.LogError(ex, "Failed to send approval notification for Document {Id}", id);
            }

            TempData["Success"] = "Retirement submitted for approval.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _docService.RetireAsync(id, user!.Id);
        TempData["Success"] = "Document retired.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── Folders ───────────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateFolder(int? parentId)
    {
        ViewBag.Folders = await _docService.GetFolderTreeAsync();
        return View(new FolderCreateViewModel { ParentId = parentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateFolder(FolderCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Folders = await _docService.GetFolderTreeAsync();
            return View(vm);
        }
        var user = await _userManager.GetUserAsync(User);
        var folder = new DocumentFolder
        {
            Name = vm.Name,
            Description = vm.Description,
            ParentId = vm.ParentId,
            CreatedById = user!.Id
        };
        await _docService.CreateFolderAsync(folder);
        TempData["Success"] = $"Folder '{folder.Name}' created.";
        return RedirectToAction(nameof(Index), new { folderId = folder.Id });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditFolder(int id)
    {
        var folder = await _docService.GetFolderByIdAsync(id);
        if (folder == null) return NotFound();
        ViewBag.Folders = await _docService.GetFolderTreeAsync();
        return View(new FolderEditViewModel
        {
            Id = folder.Id,
            Name = folder.Name,
            Description = folder.Description,
            ParentId = folder.ParentId
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditFolder(FolderEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Folders = await _docService.GetFolderTreeAsync();
            return View(vm);
        }
        var folder = await _docService.GetFolderByIdAsync(vm.Id);
        if (folder == null) return NotFound();

        folder.Name = vm.Name;
        folder.Description = vm.Description;
        folder.ParentId = vm.ParentId == vm.Id ? folder.ParentId : vm.ParentId;

        await _docService.UpdateFolderAsync(folder);
        TempData["Success"] = "Folder updated.";
        return RedirectToAction(nameof(Index), new { folderId = vm.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFolder(int id)
    {
        var ok = await _docService.DeleteFolderAsync(id);
        TempData[ok ? "Success" : "Error"] = ok ? "Folder deleted." : "Folder must be empty before it can be deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── Permissions ───────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantFolderPermission(PermissionGrantViewModel vm)
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = (await _userManager.GetRolesAsync(user!)).ToList();
        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin)
        {
            var access = await _docService.GetEffectiveAccessAsync(null, vm.FolderId, user!.Id, roles);
            if (access < AccessLevel.Manager)
            {
                TempData["Error"] = "You need Manager access to grant permissions.";
                return RedirectToAction(nameof(Index), new { folderId = vm.FolderId });
            }
        }

        await _docService.UpsertFolderPermissionAsync(vm.FolderId!.Value, vm.SubjectType, vm.SubjectId, vm.AccessLevel);
        TempData["Success"] = "Permission granted.";
        return RedirectToAction(nameof(Index), new { folderId = vm.FolderId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFolderPermission(int permissionId, int folderId)
    {
        await AssertFolderManager(folderId);
        await _docService.RemoveFolderPermissionAsync(permissionId);
        TempData["Success"] = "Permission removed.";
        return RedirectToAction(nameof(Index), new { folderId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantDocumentPermission(PermissionGrantViewModel vm)
    {
        var doc = await _docService.GetByIdAsync(vm.DocumentId!.Value);
        if (doc == null) return NotFound();
        await AssertCanManage(doc);

        await _docService.UpsertDocumentPermissionAsync(vm.DocumentId!.Value, vm.SubjectType, vm.SubjectId, vm.AccessLevel);
        TempData["Success"] = "Permission granted.";
        return RedirectToAction(nameof(Details), new { id = vm.DocumentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDocumentPermission(int permissionId, int documentId)
    {
        var doc = await _docService.GetByIdAsync(documentId);
        if (doc == null) return NotFound();
        await AssertCanManage(doc);

        await _docService.RemoveDocumentPermissionAsync(permissionId);
        TempData["Success"] = "Permission removed.";
        return RedirectToAction(nameof(Details), new { id = documentId });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task AssertCanContribute(int? documentId, int? folderId)
    {
        if (User.IsInRole("Admin")) return;
        var user = await _userManager.GetUserAsync(User);
        var roles = (await _userManager.GetRolesAsync(user!)).ToList();
        var access = await _docService.GetEffectiveAccessAsync(documentId, folderId, user!.Id, roles);
        if (!access.HasValue || access < AccessLevel.Contributor)
            throw new UnauthorizedAccessException();
    }

    private async Task AssertCanManage(Document doc)
    {
        if (User.IsInRole("Admin")) return;
        var user = await _userManager.GetUserAsync(User);
        if (doc.OwnerId == user!.Id) return;
        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var access = await _docService.GetEffectiveAccessAsync(doc.Id, doc.FolderId, user.Id, roles);
        if (!access.HasValue || access < AccessLevel.Manager)
            throw new UnauthorizedAccessException();
    }

    private async Task AssertFolderManager(int folderId)
    {
        if (User.IsInRole("Admin")) return;
        var user = await _userManager.GetUserAsync(User);
        var roles = (await _userManager.GetRolesAsync(user!)).ToList();
        var access = await _docService.GetEffectiveAccessAsync(null, folderId, user!.Id, roles);
        if (!access.HasValue || access < AccessLevel.Manager)
            throw new UnauthorizedAccessException();
    }

    // ── Approval actions ──────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForApproval(int id)
    {
        var doc = await _docService.GetByIdAsync(id);
        if (doc == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        var workflow = _workflowRegistry.Resolve("Document");
        await _approvalService.InitiateApprovalFlowAsync("Document", id, null, user!.Id);
        await workflow.OnFlowInitiatedAsync(id, user!.Id, HttpContext.RequestServices);

        // Notify first approver
        try
        {
            var firstStep = await _approvalService.GetNextPendingStepAsync("Document", id);
            if (firstStep?.Approver != null && !string.IsNullOrEmpty(firstStep.Approver.Email))
            {
                var entityLabel = $"Document {doc.SerialNumber} - {doc.Title}";
                var viewLink = Url.Action(nameof(Details), "Documents", new { id }, Request.Scheme);

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
            _logger.LogError(ex, "Failed to send approval notification for Document {Id}", id);
        }

        TempData["Success"] = "Document submitted for approval.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── Attachment actions ────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(int id, IFormFile file)
    {
        if (file == null || file.Length == 0) { TempData["Error"] = "Please select a file."; return RedirectToAction(nameof(Details), new { id }); }
        var user = await _userManager.GetUserAsync(User);
        try { await HttpContext.RequestServices.GetRequiredService<PCA.Web.Services.IAttachmentService>().UploadAsync("Document", id, file, user!.Id); TempData["Success"] = "Attachment uploaded."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId)
    {
        var user = await _userManager.GetUserAsync(User);
        try { await HttpContext.RequestServices.GetRequiredService<PCA.Web.Services.IAttachmentService>().DeleteAsync(attachmentId, user!.Id, User.IsInRole("Admin")); TempData["Success"] = "Attachment deleted."; }
        catch (UnauthorizedAccessException) { TempData["Error"] = "You cannot delete this attachment."; }
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId)
    {
        var result = await HttpContext.RequestServices.GetRequiredService<PCA.Web.Services.IAttachmentService>().GetFileAsync(attachmentId);
        if (result == null) return NotFound();
        var (filePath, contentType, fileName) = result.Value;
        return PhysicalFile(filePath, contentType, fileName);
    }

    // ── Review actions ────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReviewed(int id, string? notes)
    {
        var user = await _userManager.GetUserAsync(User);
        await _docService.MarkReviewedAsync(id, user!.Id, notes);

        // Auto-trigger approval for the review if a template is configured
        await TriggerDocumentApprovalAsync(id, AutoTriggerOn.OnReview, user!.Id);

        TempData["Success"] = "Review recorded. It will be reflected once approved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task TriggerDocumentApprovalAsync(int documentId, AutoTriggerOn trigger, string userId)
    {
        var templates = await _approvalService.GetAutoTriggerTemplatesAsync(trigger, "Document");
        if (!templates.Any()) return;
        await _approvalService.InitiateApprovalFlowAsync("Document", documentId, null, userId);
        var workflow = _workflowRegistry.Resolve("Document");
        await workflow.OnFlowInitiatedAsync(documentId, userId, HttpContext.RequestServices);

        // Notify first approver
        try
        {
            var firstStep = await _approvalService.GetNextPendingStepAsync("Document", documentId);
            if (firstStep?.Approver != null && !string.IsNullOrEmpty(firstStep.Approver.Email))
            {
                var doc = await _docService.GetByIdAsync(documentId);
                if (doc != null)
                {
                    var entityLabel = $"Document {doc.SerialNumber} - {doc.Title}";
                    var viewLink = Url.Action(nameof(Details), "Documents", new { id = documentId }, Request.Scheme);

                    await _emailService.SendApprovalRequestAsync(
                        firstStep.Approver.Email,
                        firstStep.Approver.FullName,
                        entityLabel,
                        firstStep.RoleName ?? "Approver",
                        viewLink ?? ""
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send approval notification for Document {Id}", documentId);
        }
    }

    private static void FlattenFolderTree(IEnumerable<DocumentFolder> folders, int depth, List<FlatFolderItem> result)
    {
        foreach (var f in folders)
        {
            result.Add(new FlatFolderItem { Folder = f, Depth = depth });
            if (f.Children.Any())
                FlattenFolderTree(f.Children.OrderBy(c => c.Name), depth + 1, result);
        }
    }
}
