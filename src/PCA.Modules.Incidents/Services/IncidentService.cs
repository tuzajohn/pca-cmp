using Microsoft.EntityFrameworkCore;
using PCA.Modules.Incidents.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.Incidents.Services;

public class IncidentService : IIncidentService
{
    private readonly IApplicationDbContextForIncidents _db;

    public IncidentService(IApplicationDbContextForIncidents db)
    {
        _db = db;
    }

    public async Task<List<Incident>> GetAllAsync()
    {
        return await _db.Incidents
            .Include(i => i.ReportedBy)
            .Include(i => i.AssignedTo)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Incident>> GetByUserAsync(string userId)
    {
        return await _db.Incidents
            .Include(i => i.ReportedBy)
            .Include(i => i.AssignedTo)
            .Where(i => i.ReportedById == userId || i.AssignedToId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<PagedResult<Incident>> GetPagedAsync(
        string? userId, string? status, string? severity, string? category, int page, int pageSize, string? sortCol = null, string? sortDir = null)
    {
        var query = _db.Incidents
            .Include(i => i.ReportedBy)
            .Include(i => i.AssignedTo)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(i => i.ReportedById == userId || i.AssignedToId == userId);
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<IncidentStatus>(status, out var s))
            query = query.Where(i => i.Status == s);
        if (!string.IsNullOrEmpty(severity) && Enum.TryParse<IncidentSeverity>(severity, out var sv))
            query = query.Where(i => i.Severity == sv);
        if (!string.IsNullOrEmpty(category) && Enum.TryParse<IncidentCategory>(category, out var cat))
            query = query.Where(i => i.Category == cat);

        bool asc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        query = sortCol switch {
            "serial"   => asc ? query.OrderBy(i => i.SerialNumber)  : query.OrderByDescending(i => i.SerialNumber),
            "title"    => asc ? query.OrderBy(i => i.Title)         : query.OrderByDescending(i => i.Title),
            "severity" => asc ? query.OrderBy(i => i.Severity)      : query.OrderByDescending(i => i.Severity),
            "priority" => asc ? query.OrderBy(i => i.Priority)      : query.OrderByDescending(i => i.Priority),
            "status"   => asc ? query.OrderBy(i => i.Status)        : query.OrderByDescending(i => i.Status),
            "detected" => asc ? query.OrderBy(i => i.DetectedAt)    : query.OrderByDescending(i => i.DetectedAt),
            _          => query.OrderByDescending(i => i.CreatedAt)
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<Incident> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<Incident?> GetByIdAsync(int id)
    {
        return await _db.Incidents
            .Include(i => i.ReportedBy)
            .Include(i => i.AssignedTo)
            .Include(i => i.Updates.OrderBy(u => u.CreatedAt))
                .ThenInclude(u => u.Author)
            .Include(i => i.LinkedDocuments)
                .ThenInclude(d => d.LinkedBy)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<List<Incident>> GetRecentAsync(int count = 8)
    {
        return await _db.Incidents
            .Include(i => i.ReportedBy)
            .Include(i => i.AssignedTo)
            .OrderByDescending(i => i.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<Dictionary<IncidentStatus, int>> GetStatusCountsAsync()
    {
        return await _db.Incidents
            .GroupBy(i => i.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<IncidentSeverity, int>> GetOpenBySeverityAsync()
    {
        return await _db.Incidents
            .Where(i => i.Status == IncidentStatus.Open || i.Status == IncidentStatus.InProgress)
            .GroupBy(i => i.Severity)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Incident> CreateAsync(Incident incident)
    {
        incident.SerialNumber = await GenerateSerialNumberAsync();
        incident.Status = IncidentStatus.Open;
        incident.CreatedAt = DateTime.UtcNow;
        incident.UpdatedAt = DateTime.UtcNow;
        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return incident;
    }

    public async Task<Incident> UpdateAsync(Incident incident)
    {
        incident.UpdatedAt = DateTime.UtcNow;
        _db.Incidents.Update(incident);
        await _db.SaveChangesAsync();
        return incident;
    }

    public async Task AssignAsync(int id, string? assigneeId, string actorId)
    {
        var incident = await _db.Incidents.FindAsync(id);
        if (incident == null) return;

        incident.AssignedToId = assigneeId;
        incident.UpdatedAt = DateTime.UtcNow;

        _db.IncidentUpdates.Add(new IncidentUpdate
        {
            IncidentId = id,
            AuthorId = actorId,
            Content = assigneeId == null ? "Incident unassigned." : "Incident assigned.",
            UpdateType = IncidentUpdateType.Assignment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int id, IncidentStatus newStatus, string actorId, string? comment = null)
    {
        var incident = await _db.Incidents.FindAsync(id);
        if (incident == null) return;

        var old = incident.Status;
        incident.Status = newStatus;
        incident.UpdatedAt = DateTime.UtcNow;

        if (newStatus == IncidentStatus.Resolved) incident.ResolvedAt = DateTime.UtcNow;
        if (newStatus == IncidentStatus.Closed) incident.ClosedAt = DateTime.UtcNow;

        _db.IncidentUpdates.Add(new IncidentUpdate
        {
            IncidentId = id,
            AuthorId = actorId,
            Content = comment ?? $"Status changed from {old} to {newStatus}.",
            UpdateType = IncidentUpdateType.StatusChange,
            OldStatus = old,
            NewStatus = newStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task ResolveAsync(int id, string actorId, string rootCause, string resolutionSummary)
    {
        var incident = await _db.Incidents.FindAsync(id);
        if (incident == null) return;

        var old = incident.Status;
        incident.Status = IncidentStatus.Resolved;
        incident.RootCause = rootCause;
        incident.ResolutionSummary = resolutionSummary;
        incident.ResolvedAt = DateTime.UtcNow;
        incident.UpdatedAt = DateTime.UtcNow;

        _db.IncidentUpdates.Add(new IncidentUpdate
        {
            IncidentId = id,
            AuthorId = actorId,
            Content = resolutionSummary,
            UpdateType = IncidentUpdateType.Resolution,
            OldStatus = old,
            NewStatus = IncidentStatus.Resolved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task CloseAsync(int id, string actorId, string? comment = null)
    {
        await UpdateStatusAsync(id, IncidentStatus.Closed, actorId, comment ?? "Incident closed.");
    }

    public async Task AddUpdateAsync(int id, string authorId, string content)
    {
        var incident = await _db.Incidents.FindAsync(id);
        if (incident == null) return;

        _db.IncidentUpdates.Add(new IncidentUpdate
        {
            IncidentId = id,
            AuthorId = authorId,
            Content = content,
            UpdateType = IncidentUpdateType.Comment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        incident.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task<string> GenerateSerialNumberAsync()
    {
        var now = DateTime.UtcNow;
        var seq = await _db.IncidentSequences
            .FirstOrDefaultAsync(s => s.Year == now.Year && s.Month == now.Month);

        if (seq == null)
        {
            seq = new IncidentSequence { Year = now.Year, Month = now.Month, LastSequence = 0 };
            _db.IncidentSequences.Add(seq);
        }

        seq.LastSequence++;
        await _db.SaveChangesAsync();
        return $"PCA-INC-{now:yyyyMM}-{seq.LastSequence:D4}";
    }
}
