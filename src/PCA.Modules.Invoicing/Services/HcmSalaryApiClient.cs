using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PCA.Modules.Invoicing.Services;

public class HcmApiSettings
{
    public string Url { get; set; } = "https://api.pcuganda.com/pdms/prod/validate_hcm_employee";
    public string Token { get; set; } = string.Empty;   // optional: sent as Bearer only when set
    public int MaxConcurrency { get; set; } = 8;
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Reads gross_salary from the HCM validate_hcm_employee API
/// (multipart POST: nin, empNumber, transactionType; Bearer token when configured).
/// </summary>
public class HcmSalaryApiClient
{
    private const int MaxAttempts     = 3;
    private const int MaxLoggedErrors = 10;

    private readonly HttpClient _http;
    private readonly HcmApiSettings _settings;
    private readonly ILogger<HcmSalaryApiClient> _logger;

    public HcmSalaryApiClient(HttpClient http, HcmApiSettings settings, ILogger<HcmSalaryApiClient> logger)
    {
        _http     = http;
        _settings = settings;
        _logger   = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.Url);

    /// <summary>
    /// Looks up each 15-digit employee number. Employees the API could not answer for
    /// (HTTP error, timeout, non-success status, no gross_salary) are left out of the
    /// result so the caller can fall back to another source.
    /// </summary>
    public async Task<Dictionary<string, decimal>> GetGrossSalariesAsync(
        IReadOnlyCollection<string> employeeNumbers,
        Action<int, int>? progress = null,
        CancellationToken ct = default)
    {
        var result = new ConcurrentDictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        int done = 0, errors = 0;

        void LogError(string empNo, string reason)
        {
            if (Interlocked.Increment(ref errors) <= MaxLoggedErrors)
                _logger.LogWarning("HCM API: no salary for {EmpNo} — {Reason}", empNo, reason);
        }

        await Parallel.ForEachAsync(employeeNumbers,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _settings.MaxConcurrency), CancellationToken = ct },
            async (empNo, token) =>
            {
                var salary = await GetGrossSalaryAsync(empNo, LogError, token);
                if (salary.HasValue) result[empNo] = salary.Value;
                progress?.Invoke(Interlocked.Increment(ref done), employeeNumbers.Count);
            });

        if (errors > MaxLoggedErrors)
            _logger.LogWarning("HCM API: {Errors} employees had no salary from the API ({Logged} logged above)",
                errors, MaxLoggedErrors);
        return new Dictionary<string, decimal>(result, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<decimal?> GetGrossSalaryAsync(
        string empNo, Action<string, string> logError, CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, _settings.Url)
                {
                    Content = new MultipartFormDataContent
                    {
                        { FormField(""), "nin" },
                        { FormField(empNo), "empNumber" },
                        { FormField("0"), "transactionType" }
                    }
                };
                if (!string.IsNullOrWhiteSpace(_settings.Token))
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);

                using var resp = await _http.SendAsync(req, ct);
                if (IsTransient(resp.StatusCode) && attempt < MaxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
                    continue;
                }
                if (!resp.IsSuccessStatusCode)
                {
                    logError(empNo, $"HTTP {(int)resp.StatusCode}");
                    return null;
                }

                var salary = ParseGrossSalary(await resp.Content.ReadAsStringAsync(ct));
                if (salary == null) logError(empNo, "response had no successful gross_salary");
                return salary;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= MaxAttempts)
                {
                    logError(empNo, ex is TaskCanceledException ? "timed out" : ex.Message);
                    return null;
                }
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
        }
    }

    // Plain form field, sent the way Postman does (no per-part Content-Type)
    private static StringContent FormField(string value)
    {
        var content = new StringContent(value);
        content.Headers.ContentType = null;
        return content;
    }

    private static bool IsTransient(HttpStatusCode code)
        => code is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)code >= 500;

    // { "status": "success", "data": { "gross_salary": "700000.00", ... } }
    private static decimal? ParseGrossSalary(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("status", out var status) ||
                !string.Equals(status.GetString(), "success", StringComparison.OrdinalIgnoreCase) ||
                !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("gross_salary", out var gross))
                return null;

            var raw = gross.ValueKind == JsonValueKind.Number ? gross.GetRawText() : gross.GetString();
            return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        catch (JsonException) { return null; }
    }
}
