using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PCA.Web.Data;
using PCA.Web.Models;

namespace PCA.Web.Services;

public class AttachmentService : IAttachmentService
{
    private readonly ApplicationDbContext _db;
    private readonly string _storageRoot;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".png", ".jpg", ".jpeg", ".gif", ".zip", ".log"
    };

    public AttachmentService(ApplicationDbContext db, string storageRoot)
    {
        _db = db;
        _storageRoot = storageRoot;
    }

    public async Task<List<Attachment>> GetForEntityAsync(string entityType, int entityId)
        => await _db.Attachments
            .Include(a => a.UploadedBy)
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

    public async Task<Attachment> UploadAsync(string entityType, int entityId, IFormFile file, string uploadedById)
    {
        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"File type '{ext}' is not permitted.");

        var dir = Path.Combine(_storageRoot, entityType.ToLower(), entityId.ToString());
        Directory.CreateDirectory(dir);

        var storedName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(dir, storedName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var attachment = new Attachment
        {
            EntityType = entityType,
            EntityId = entityId,
            OriginalFileName = file.FileName,
            StoredFileName = storedName,
            FilePath = filePath,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            UploadedById = uploadedById,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync();
        return attachment;
    }

    public async Task<(string filePath, string contentType, string fileName)?> GetFileAsync(int attachmentId)
    {
        var a = await _db.Attachments.FindAsync(attachmentId);
        if (a == null || !File.Exists(a.FilePath)) return null;
        return (a.FilePath, a.ContentType, a.OriginalFileName);
    }

    public async Task DeleteAsync(int attachmentId, string requestingUserId, bool isAdmin)
    {
        var a = await _db.Attachments.FindAsync(attachmentId);
        if (a == null) return;
        if (!isAdmin && a.UploadedById != requestingUserId)
            throw new UnauthorizedAccessException();

        if (File.Exists(a.FilePath))
            File.Delete(a.FilePath);

        _db.Attachments.Remove(a);
        await _db.SaveChangesAsync();
    }
}
