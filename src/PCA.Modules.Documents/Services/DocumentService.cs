using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PCA.Modules.Documents.Models;
using PCA.Shared.Enums;

namespace PCA.Modules.Documents.Services;

public class DocumentService : IDocumentService
{
    private readonly IApplicationDbContextForDocuments _db;
    private readonly string _storageRoot;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt",
        ".txt", ".csv", ".png", ".jpg", ".jpeg", ".gif", ".zip"
    };

    public DocumentService(IApplicationDbContextForDocuments db, string storageRoot)
    {
        _db = db;
        _storageRoot = storageRoot;
    }

    // ── Folders ──────────────────────────────────────────────────────────────

    public async Task<List<DocumentFolder>> GetFolderTreeAsync()
    {
        return await _db.DocumentFolders
            .Include(f => f.Children)
            .Include(f => f.Permissions)
            .Include(f => f.CreatedBy)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<DocumentFolder?> GetFolderByIdAsync(int id)
    {
        return await _db.DocumentFolders
            .Include(f => f.Children)
            .Include(f => f.Parent)
            .Include(f => f.Permissions)
            .Include(f => f.CreatedBy)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<DocumentFolder> CreateFolderAsync(DocumentFolder folder)
    {
        folder.CreatedAt = DateTime.UtcNow;
        folder.UpdatedAt = DateTime.UtcNow;
        _db.DocumentFolders.Add(folder);
        await _db.SaveChangesAsync();
        return folder;
    }

    public async Task<DocumentFolder> UpdateFolderAsync(DocumentFolder folder)
    {
        folder.UpdatedAt = DateTime.UtcNow;
        _db.DocumentFolders.Update(folder);
        await _db.SaveChangesAsync();
        return folder;
    }

    public async Task<bool> DeleteFolderAsync(int id)
    {
        var folder = await _db.DocumentFolders
            .Include(f => f.Children)
            .Include(f => f.Documents)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (folder == null) return false;
        if (folder.Children.Any() || folder.Documents.Any()) return false;

        _db.DocumentFolders.Remove(folder);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Documents ─────────────────────────────────────────────────────────────

    public async Task<List<Document>> GetAllAsync(int? folderId = null)
    {
        var q = _db.Documents
            .Include(d => d.Versions)
            .Include(d => d.Owner)
            .Include(d => d.Folder)
            .AsQueryable();

        if (folderId.HasValue)
            q = q.Where(d => d.FolderId == folderId);

        return await q.OrderByDescending(d => d.UpdatedAt).ToListAsync();
    }

    public async Task<List<Document>> SearchAsync(string query)
    {
        var lower = query.ToLower();
        return await _db.Documents
            .Include(d => d.Versions)
            .Include(d => d.Owner)
            .Include(d => d.Folder)
            .Where(d => d.Title.ToLower().Contains(lower)
                     || d.SerialNumber.ToLower().Contains(lower)
                     || (d.Tags != null && d.Tags.ToLower().Contains(lower))
                     || (d.Description != null && d.Description.ToLower().Contains(lower)))
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Document?> GetByIdAsync(int id)
    {
        return await _db.Documents
            .Include(d => d.Versions.OrderByDescending(v => v.VersionNumber))
                .ThenInclude(v => v.UploadedBy)
            .Include(d => d.Owner)
            .Include(d => d.CreatedBy)
            .Include(d => d.Folder)
            .Include(d => d.Permissions)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Document> CreateAsync(Document document, IFormFile file, string changeNotes)
    {
        document.SerialNumber = await GenerateSerialNumberAsync();
        document.CreatedAt = DateTime.UtcNow;
        document.UpdatedAt = DateTime.UtcNow;
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        await UploadVersionAsync(document.Id, file, changeNotes, document.CreatedById);
        return document;
    }

    public async Task<Document> UpdateMetadataAsync(Document document)
    {
        document.UpdatedAt = DateTime.UtcNow;
        _db.Documents.Update(document);
        await _db.SaveChangesAsync();
        return document;
    }

    public async Task<bool> RetireAsync(int id, string userId)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null) return false;
        doc.Status = DocumentStatus.Retired;
        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Versions ─────────────────────────────────────────────────────────────

    public async Task<DocumentVersion> UploadVersionAsync(int documentId, IFormFile file, string changeNotes, string userId)
    {
        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"File type '{ext}' is not permitted.");

        var existing = await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .ToListAsync();

        foreach (var v in existing.Where(v => v.IsCurrentVersion))
            v.IsCurrentVersion = false;

        var versionNumber = existing.Count == 0 ? 1 : existing.Max(v => v.VersionNumber) + 1;

        var dir = Path.Combine(_storageRoot, documentId.ToString());
        Directory.CreateDirectory(dir);
        var storedName = $"v{versionNumber}_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(dir, storedName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var version = new DocumentVersion
        {
            DocumentId = documentId,
            VersionNumber = versionNumber,
            FileName = storedName,
            OriginalFileName = file.FileName,
            FilePath = filePath,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            ChangeNotes = changeNotes,
            IsCurrentVersion = true,
            UploadedById = userId,
            UploadedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.DocumentVersions.Add(version);

        var doc = await _db.Documents.FindAsync(documentId);
        if (doc != null)
        {
            if (doc.Status == DocumentStatus.Draft)
                doc.Status = DocumentStatus.Active;
            doc.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return version;
    }

    public async Task<bool> SetCurrentVersionAsync(int versionId, string userId)
    {
        var version = await _db.DocumentVersions.FindAsync(versionId);
        if (version == null) return false;

        var siblings = await _db.DocumentVersions
            .Where(v => v.DocumentId == version.DocumentId)
            .ToListAsync();

        foreach (var v in siblings)
            v.IsCurrentVersion = v.Id == versionId;

        var doc = await _db.Documents.FindAsync(version.DocumentId);
        if (doc != null) doc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteVersionAsync(int versionId, string userId)
    {
        var version = await _db.DocumentVersions.FindAsync(versionId);
        if (version == null) return false;

        var count = await _db.DocumentVersions.CountAsync(v => v.DocumentId == version.DocumentId);
        if (count <= 1) return false;

        if (version.IsCurrentVersion)
        {
            var next = await _db.DocumentVersions
                .Where(v => v.DocumentId == version.DocumentId && v.Id != versionId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();
            if (next != null) next.IsCurrentVersion = true;
        }

        if (File.Exists(version.FilePath))
            File.Delete(version.FilePath);

        _db.DocumentVersions.Remove(version);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(Stream stream, string contentType, string fileName)?> DownloadAsync(int versionId, string userId)
    {
        var version = await _db.DocumentVersions.FindAsync(versionId);
        if (version == null || !File.Exists(version.FilePath)) return null;

        var stream = new FileStream(version.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, version.ContentType, version.OriginalFileName);
    }

    // ── Permissions ───────────────────────────────────────────────────────────

    public async Task<AccessLevel?> GetEffectiveAccessAsync(int? documentId, int? folderId, string userId, IList<string> userRoles)
    {
        // 1. Document-level explicit — user
        if (documentId.HasValue)
        {
            var docPerms = await _db.DocumentPermissions
                .Where(p => p.DocumentId == documentId)
                .ToListAsync();

            var userPerm = docPerms.FirstOrDefault(p =>
                p.SubjectType == PermissionSubjectType.User && p.SubjectId == userId);
            if (userPerm != null) return userPerm.AccessLevel;

            var rolePerm = docPerms
                .Where(p => p.SubjectType == PermissionSubjectType.Role && userRoles.Contains(p.SubjectId))
                .OrderByDescending(p => p.AccessLevel)
                .FirstOrDefault();
            if (rolePerm != null) return rolePerm.AccessLevel;
        }

        // 2. Walk up the folder tree
        var currentFolderId = folderId;
        if (documentId.HasValue && !currentFolderId.HasValue)
        {
            var doc = await _db.Documents.FindAsync(documentId.Value);
            currentFolderId = doc?.FolderId;
        }

        while (currentFolderId.HasValue)
        {
            var folder = await _db.DocumentFolders
                .Include(f => f.Permissions)
                .FirstOrDefaultAsync(f => f.Id == currentFolderId);
            if (folder == null) break;

            var userPerm = folder.Permissions.FirstOrDefault(p =>
                p.SubjectType == PermissionSubjectType.User && p.SubjectId == userId);
            if (userPerm != null) return userPerm.AccessLevel;

            var rolePerm = folder.Permissions
                .Where(p => p.SubjectType == PermissionSubjectType.Role && userRoles.Contains(p.SubjectId))
                .OrderByDescending(p => p.AccessLevel)
                .FirstOrDefault();
            if (rolePerm != null) return rolePerm.AccessLevel;

            currentFolderId = folder.ParentId;
        }

        return null;
    }

    public async Task<bool> HasAccessAsync(int? documentId, int? folderId, string userId, IList<string> userRoles, AccessLevel minimum)
    {
        var level = await GetEffectiveAccessAsync(documentId, folderId, userId, userRoles);
        return level.HasValue && level.Value >= minimum;
    }

    public async Task<List<FolderPermission>> GetFolderPermissionsAsync(int folderId)
    {
        return await _db.FolderPermissions
            .Where(p => p.FolderId == folderId)
            .ToListAsync();
    }

    public async Task<List<DocumentPermission>> GetDocumentPermissionsAsync(int documentId)
    {
        return await _db.DocumentPermissions
            .Where(p => p.DocumentId == documentId)
            .ToListAsync();
    }

    public async Task UpsertFolderPermissionAsync(int folderId, PermissionSubjectType subjectType, string subjectId, AccessLevel level)
    {
        var existing = await _db.FolderPermissions.FirstOrDefaultAsync(p =>
            p.FolderId == folderId && p.SubjectType == subjectType && p.SubjectId == subjectId);

        if (existing != null)
        {
            existing.AccessLevel = level;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.FolderPermissions.Add(new FolderPermission
            {
                FolderId = folderId,
                SubjectType = subjectType,
                SubjectId = subjectId,
                AccessLevel = level,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task RemoveFolderPermissionAsync(int permissionId)
    {
        var p = await _db.FolderPermissions.FindAsync(permissionId);
        if (p != null)
        {
            _db.FolderPermissions.Remove(p);
            await _db.SaveChangesAsync();
        }
    }

    public async Task UpsertDocumentPermissionAsync(int documentId, PermissionSubjectType subjectType, string subjectId, AccessLevel level)
    {
        var existing = await _db.DocumentPermissions.FirstOrDefaultAsync(p =>
            p.DocumentId == documentId && p.SubjectType == subjectType && p.SubjectId == subjectId);

        if (existing != null)
        {
            existing.AccessLevel = level;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.DocumentPermissions.Add(new DocumentPermission
            {
                DocumentId = documentId,
                SubjectType = subjectType,
                SubjectId = subjectId,
                AccessLevel = level,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task RemoveDocumentPermissionAsync(int permissionId)
    {
        var p = await _db.DocumentPermissions.FindAsync(permissionId);
        if (p != null)
        {
            _db.DocumentPermissions.Remove(p);
            await _db.SaveChangesAsync();
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private async Task<string> GenerateSerialNumberAsync()
    {
        var now = DateTime.UtcNow;
        var seq = await _db.DocumentSequences
            .FirstOrDefaultAsync(s => s.Year == now.Year && s.Month == now.Month);

        if (seq == null)
        {
            seq = new DocumentSequence { Year = now.Year, Month = now.Month, LastSequence = 1 };
            _db.DocumentSequences.Add(seq);
        }
        else
        {
            seq.LastSequence++;
            _db.DocumentSequences.Update(seq);
        }

        await _db.SaveChangesAsync();
        return $"PCA-DOC-{now.Year}{now.Month:D2}-{seq.LastSequence:D4}";
    }
}
