using Microsoft.AspNetCore.Http;
using PCA.Web.Models;

namespace PCA.Web.Services;

public interface IAttachmentService
{
    Task<List<Attachment>> GetForEntityAsync(string entityType, int entityId);
    Task<Attachment> UploadAsync(string entityType, int entityId, IFormFile file, string uploadedById);
    Task<(string filePath, string contentType, string fileName)?> GetFileAsync(int attachmentId);
    Task DeleteAsync(int attachmentId, string requestingUserId, bool isAdmin);
}
