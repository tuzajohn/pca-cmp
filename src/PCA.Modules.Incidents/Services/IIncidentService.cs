using PCA.Modules.Incidents.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.Incidents.Services;

public interface IIncidentService
{
    Task<List<Incident>> GetAllAsync();
    Task<List<Incident>> GetByUserAsync(string userId);
    Task<PagedResult<Incident>> GetPagedAsync(string? userId, string? status, string? severity, string? category, int page, int pageSize);
    Task<Incident?> GetByIdAsync(int id);
    Task<List<Incident>> GetRecentAsync(int count = 8);
    Task<Dictionary<IncidentStatus, int>> GetStatusCountsAsync();
    Task<Dictionary<IncidentSeverity, int>> GetOpenBySeverityAsync();

    Task<Incident> CreateAsync(Incident incident);
    Task<Incident> UpdateAsync(Incident incident);
    Task AssignAsync(int id, string? assigneeId, string actorId);
    Task UpdateStatusAsync(int id, IncidentStatus newStatus, string actorId, string? comment = null);
    Task ResolveAsync(int id, string actorId, string rootCause, string resolutionSummary);
    Task CloseAsync(int id, string actorId, string? comment = null);
    Task AddUpdateAsync(int id, string authorId, string content);
}
