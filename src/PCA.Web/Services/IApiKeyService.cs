using PCA.Web.Models;

namespace PCA.Web.Services;

public interface IApiKeyService
{
    /// <summary>Returns (rawKey, record) — rawKey is shown once and never stored.</summary>
    Task<(string RawKey, ApiKey Record)> CreateAsync(string appName, string createdById);
    Task<List<ApiKey>> GetAllAsync();
    Task RevokeAsync(int id);
    Task<ApiKey?> ValidateAsync(string rawKey);
}
