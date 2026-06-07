using PCA.Shared;

namespace PCA.Web.Models;

public class ApiKey : BaseEntity
{
    public string AppName   { get; set; } = string.Empty;
    public string KeyHash   { get; set; } = string.Empty;  // SHA-256 hex of the raw key
    public string KeyPrefix { get; set; } = string.Empty;  // first 8 chars for display
    public bool   IsActive  { get; set; } = true;
    public DateTime? LastUsedAt { get; set; }
    public string CreatedById { get; set; } = string.Empty;
}
