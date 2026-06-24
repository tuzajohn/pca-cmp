using Microsoft.EntityFrameworkCore;
using PCA.Modules.Invoicing.Models;

namespace PCA.Modules.Invoicing.Services;

public class HcmMappingService
{
    private readonly IApplicationDbContextForInvoicing _db;

    // key = "SOURCECOL|rawvalue" (upper-cased), value = full mapping entry
    private Dictionary<string, HcmMapping> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public HcmMappingService(IApplicationDbContextForInvoicing db) => _db = db;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _lock.WaitAsync();
        try { if (!_loaded) await RefreshCacheAsync(); }
        finally { _lock.Release(); }
    }

    public async Task RefreshCacheAsync()
    {
        var all  = await _db.HcmMappings.AsNoTracking().ToListAsync();
        var dict = new Dictionary<string, HcmMapping>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in all)
        {
            // Primary raw value
            dict[CacheKey(m.SourceColumn, m.RawValue)] = m;

            // Aliases — comma-separated alternate raw strings that map to the same entry
            if (!string.IsNullOrWhiteSpace(m.Aliases))
            {
                foreach (var alias in m.Aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    dict[CacheKey(m.SourceColumn, alias)] = m;
            }
        }

        _cache  = dict;
        _loaded = true;
    }

    public string? GetClassification(string rawValue, string sourceColumn)
        => _cache.TryGetValue(CacheKey(sourceColumn, rawValue), out var m) ? m.Classification : null;

    /// <summary>
    /// Returns the canonical group name for a raw value.
    /// Falls back to the raw value itself when no CanonicalName is set.
    /// Used to collapse aliases when aggregating stat/allow.
    /// </summary>
    public string GetCanonical(string rawValue, string sourceColumn)
    {
        if (_cache.TryGetValue(CacheKey(sourceColumn, rawValue), out var m) &&
            !string.IsNullOrWhiteSpace(m.CanonicalName))
            return m.CanonicalName;
        return rawValue;
    }

    public async Task<List<(string RawValue, string SourceColumn)>> FindUnknownsAsync(
        IEnumerable<string> costItems,
        IEnumerable<string> vendorNames)
    {
        await EnsureLoadedAsync();
        var unknowns = new List<(string, string)>();

        foreach (var v in costItems.Distinct(StringComparer.OrdinalIgnoreCase))
            if (!string.IsNullOrWhiteSpace(v) &&
                GetClassification(v, HcmMapping.SourceColumns.CostItem) == null)
                unknowns.Add((v, HcmMapping.SourceColumns.CostItem));

        foreach (var v in vendorNames.Distinct(StringComparer.OrdinalIgnoreCase))
            if (!string.IsNullOrWhiteSpace(v) &&
                GetClassification(v, HcmMapping.SourceColumns.VendorName) == null)
                unknowns.Add((v, HcmMapping.SourceColumns.VendorName));

        return unknowns;
    }

    public async Task SaveMappingsAsync(List<HcmMapping> mappings)
    {
        foreach (var m in mappings)
        {
            var existing = await _db.HcmMappings.FirstOrDefaultAsync(
                x => x.RawValue == m.RawValue && x.SourceColumn == m.SourceColumn);
            if (existing != null)
            {
                existing.Classification = m.Classification;
                existing.CanonicalName  = m.CanonicalName;
                existing.UpdatedAt      = DateTime.UtcNow;
            }
            else
            {
                m.CreatedAt = DateTime.UtcNow;
                m.UpdatedAt = DateTime.UtcNow;
                _db.HcmMappings.Add(m);
            }
        }
        await _db.SaveChangesAsync();
        await RefreshCacheAsync();
    }

    public async Task<List<HcmMapping>> GetAllAsync()
        => await _db.HcmMappings.AsNoTracking()
            .OrderBy(m => m.SourceColumn).ThenBy(m => m.RawValue)
            .ToListAsync();

    private static string CacheKey(string sourceColumn, string rawValue)
        => $"{sourceColumn.ToUpperInvariant()}|{rawValue}";
}
