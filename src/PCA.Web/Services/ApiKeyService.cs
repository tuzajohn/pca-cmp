using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PCA.Web.Data;
using PCA.Web.Models;

namespace PCA.Web.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly ApplicationDbContext _db;

    public ApiKeyService(ApplicationDbContext db) => _db = db;

    public async Task<(string RawKey, ApiKey Record)> CreateAsync(string appName, string createdById)
    {
        var raw    = GenerateKey();
        var hash   = Hash(raw);
        var prefix = raw[..8];

        var record = new ApiKey
        {
            AppName     = appName,
            KeyHash     = hash,
            KeyPrefix   = prefix,
            IsActive    = true,
            CreatedById = createdById,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        _db.ApiKeys.Add(record);
        await _db.SaveChangesAsync();
        return (raw, record);
    }

    public Task<List<ApiKey>> GetAllAsync()
        => _db.ApiKeys.OrderByDescending(k => k.CreatedAt).ToListAsync();

    public async Task RevokeAsync(int id)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null) return;
        key.IsActive  = false;
        key.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<ApiKey?> ValidateAsync(string rawKey)
    {
        var hash = Hash(rawKey);
        var key  = await _db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive);
        if (key == null) return null;

        key.LastUsedAt = DateTime.UtcNow;
        key.UpdatedAt  = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return key;
    }

    private static string GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
