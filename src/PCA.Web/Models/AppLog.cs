using PCA.Shared;

namespace PCA.Web.Models;

public class AppLog : BaseEntity
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Info";        // Info, Warning, Error, Critical
    public string Category { get; set; } = "App";      // App, Audit, External
    public string Source { get; set; } = "PCA Portal"; // app name
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }               // stack trace or extra JSON
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? Action { get; set; }                // e.g. "CR.Created", "User.Login"
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? IpAddress { get; set; }
}
