using Microsoft.EntityFrameworkCore;
using PCA.Web.Data;
using PCA.Web.Models;

namespace PCA.Web.Services;

public class LogService : ILogService
{
    private readonly ApplicationDbContext _db;

    public LogService(ApplicationDbContext db) => _db = db;

    public Task InfoAsync(string message, string? action = null, string? entityType = null, int? entityId = null,
        string? userId = null, string? userEmail = null, string? ipAddress = null)
        => WriteAsync(new AppLog
        {
            Level = "Info", Category = "App", Message = message, Action = action,
            EntityType = entityType, EntityId = entityId,
            UserId = userId, UserEmail = userEmail, IpAddress = ipAddress
        });

    public Task AuditAsync(string action, string message, string? entityType = null, int? entityId = null,
        string? userId = null, string? userEmail = null, string? ipAddress = null)
        => WriteAsync(new AppLog
        {
            Level = "Info", Category = "Audit", Action = action, Message = message,
            EntityType = entityType, EntityId = entityId,
            UserId = userId, UserEmail = userEmail, IpAddress = ipAddress
        });

    public Task ErrorAsync(string message, string? details = null, string? userId = null,
        string? userEmail = null, string? ipAddress = null)
        => WriteAsync(new AppLog
        {
            Level = "Error", Category = "App", Message = message, Details = details,
            UserId = userId, UserEmail = userEmail, IpAddress = ipAddress
        });

    public async Task WriteAsync(AppLog log)
    {
        log.Timestamp = DateTime.UtcNow;
        log.CreatedAt = DateTime.UtcNow;
        log.UpdatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(log.Source)) log.Source = "PCA Portal";
        _db.AppLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public async Task<(List<AppLog> Logs, int Total)> QueryAsync(
        string? source = null, string? level = null, string? category = null,
        string? search = null, DateTime? from = null, DateTime? to = null,
        int page = 1, int pageSize = 50)
    {
        var q = _db.AppLogs.AsQueryable();

        if (!string.IsNullOrEmpty(source))   q = q.Where(l => l.Source == source);
        if (!string.IsNullOrEmpty(level))    q = q.Where(l => l.Level == level);
        if (!string.IsNullOrEmpty(category)) q = q.Where(l => l.Category == category);
        if (from.HasValue) q = q.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue)   q = q.Where(l => l.Timestamp <= to.Value);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(l => l.Message.Contains(search) || (l.Action != null && l.Action.Contains(search))
                           || (l.UserEmail != null && l.UserEmail.Contains(search)));

        var total = await q.CountAsync();
        var logs  = await q.OrderByDescending(l => l.Timestamp)
                           .Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .ToListAsync();

        return (logs, total);
    }
}
