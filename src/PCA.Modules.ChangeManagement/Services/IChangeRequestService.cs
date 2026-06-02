using PCA.Modules.ChangeManagement.Models;
using PCA.Shared.Enums;

namespace PCA.Modules.ChangeManagement.Services;

public interface IChangeRequestService
{
    Task<List<ChangeRequest>> GetAllAsync();
    Task<List<ChangeRequest>> GetByUserAsync(string userId);
    Task<ChangeRequest?> GetByIdAsync(int id);
    Task<ChangeRequest> CreateAsync(ChangeRequest changeRequest);
    Task<ChangeRequest> UpdateAsync(ChangeRequest changeRequest);
    Task<bool> SubmitAsync(int id, string userId);
    Task<bool> UpdateStatusAsync(int id, ChangeStatus status, string userId);
    Task<List<ChangeRequest>> GetRecentAsync(int count = 10);
    Task<Dictionary<ChangeStatus, int>> GetStatusCountsAsync();
    Task AddCommentAsync(int changeRequestId, string authorId, string content);
    Task<bool> SubmitPirAsync(int id, string userId, ImplementationOutcome outcome,
        DateTime? actualDate, string? issues, string? lessons, bool rollbackExecuted, string? notes);
}
