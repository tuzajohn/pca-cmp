using PCA.Web.Models;

namespace PCA.Web.Services;

public interface ILogService
{
    Task InfoAsync(string message, string? action = null, string? entityType = null, int? entityId = null,
        string? userId = null, string? userEmail = null, string? ipAddress = null);

    Task AuditAsync(string action, string message, string? entityType = null, int? entityId = null,
        string? userId = null, string? userEmail = null, string? ipAddress = null);

    Task ErrorAsync(string message, string? details = null, string? userId = null, string? userEmail = null,
        string? ipAddress = null);

    Task WriteAsync(AppLog log);

    Task<(List<AppLog> Logs, int Total)> QueryAsync(
        string? source = null, string? level = null, string? category = null,
        string? search = null, DateTime? from = null, DateTime? to = null,
        int page = 1, int pageSize = 50);
}
