using Microsoft.AspNetCore.Http;
using PCA.Modules.Documents.Models;
using PCA.Shared.Enums;

namespace PCA.Modules.Documents.Services;

public interface IDocumentService
{
    // Folders
    Task<List<DocumentFolder>> GetFolderTreeAsync();
    Task<DocumentFolder?> GetFolderByIdAsync(int id);
    Task<DocumentFolder> CreateFolderAsync(DocumentFolder folder);
    Task<DocumentFolder> UpdateFolderAsync(DocumentFolder folder);
    Task<bool> DeleteFolderAsync(int id);

    // Documents
    Task<List<Document>> GetAllAsync(int? folderId = null);
    Task<List<Document>> SearchAsync(string query);
    Task<Document?> GetByIdAsync(int id);
    Task<Document> CreateAsync(Document document, IFormFile file, string changeNotes);
    Task<Document> UpdateMetadataAsync(Document document);
    Task<bool> RetireAsync(int id, string userId);

    // Versions
    Task<DocumentVersion> UploadVersionAsync(int documentId, IFormFile file, string changeNotes, string userId);
    Task<bool> SetCurrentVersionAsync(int versionId, string userId);
    Task<bool> DeleteVersionAsync(int versionId, string userId);
    Task<(Stream stream, string contentType, string fileName)?> DownloadAsync(int versionId, string userId);

    // Review schedule
    Task MarkReviewedAsync(int documentId, string reviewedById, string? notes = null);
    Task<List<DocumentReview>> GetReviewHistoryAsync(int documentId);
    Task<List<Document>> GetDocumentsDueForReviewAlertAsync(int daysAhead, int alertFlag);
    Task SetReviewAlertFlagAsync(int documentId, int flag);
    Task UpdateStatusAsync(int documentId, DocumentStatus status);

    // Permissions
    Task<AccessLevel?> GetEffectiveAccessAsync(int? documentId, int? folderId, string userId, IList<string> userRoles);
    Task<bool> HasAccessAsync(int? documentId, int? folderId, string userId, IList<string> userRoles, AccessLevel minimum);
    Task<List<FolderPermission>> GetFolderPermissionsAsync(int folderId);
    Task<List<DocumentPermission>> GetDocumentPermissionsAsync(int documentId);
    Task UpsertFolderPermissionAsync(int folderId, PermissionSubjectType subjectType, string subjectId, AccessLevel level);
    Task RemoveFolderPermissionAsync(int permissionId);
    Task UpsertDocumentPermissionAsync(int documentId, PermissionSubjectType subjectType, string subjectId, AccessLevel level);
    Task RemoveDocumentPermissionAsync(int permissionId);
}
