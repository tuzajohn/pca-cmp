using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PCA.Modules.Invoicing.Models;
using PCA.Modules.Invoicing.Services;
using PCA.Web.Services;
using System.Text.Json;

namespace PCA.Web.Controllers;

[Authorize]
public class CrbController : Controller
{
    private readonly HcmReportService  _hcmSvc;
    private readonly CrbReportService  _ippsSvc;
    private readonly HcmMappingService _mappings;
    private readonly CrbProgressStore  _progress;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CrbController> _logger;
    private readonly string _storageRoot;

    private static readonly JsonSerializerOptions _jsonOpts = new()
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public CrbController(
        HcmReportService hcmSvc,
        CrbReportService ippsSvc,
        HcmMappingService mappings,
        CrbProgressStore progress,
        IServiceScopeFactory scopeFactory,
        ILogger<CrbController> logger,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _hcmSvc       = hcmSvc;
        _ippsSvc      = ippsSvc;
        _mappings     = mappings;
        _progress     = progress;
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _storageRoot  = config["InvoiceStoragePath"]
            ?? Path.Combine(env.ContentRootPath, "uploads", "documents");
    }

    // ── Pages ─────────────────────────────────────────────────────────────────

    [HttpGet] public IActionResult Index()   => View();
    [HttpGet] public IActionResult HcmRun()  => View();
    [HttpGet] public IActionResult IppsRun() => View();

    [HttpGet]
    public async Task<IActionResult> Mappings()
        => View(await _mappings.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> UpdateMapping([FromBody] UpdateMappingRequest req)
    {
        if (req.Id <= 0)
            return Json(new { ok = false, message = "Invalid id." });
        try
        {
            await _mappings.UpdateMappingAsync(req.Id, req.Classification, req.CanonicalName, req.Aliases);
            return Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, message = ex.Message });
        }
    }

    // ── SSE stream endpoint ───────────────────────────────────────────────────

    [HttpGet]
    public async Task RunStream(string runId, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"]      = "no-cache";
        Response.Headers["X-Accel-Buffering"]  = "no";

        var reader = _progress.GetReader(runId);
        if (reader == null)
        {
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { t = "error", msg = "Run not found." }, _jsonOpts)}\n\n");
            return;
        }

        try
        {
            await foreach (var evt in reader.ReadAllAsync(ct))
            {
                var json = JsonSerializer.Serialize(evt, _jsonOpts);
                await Response.WriteAsync($"data: {json}\n\n");
                await Response.Body.FlushAsync(ct);
                if (evt.T is "done" or "error") break;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _progress.Remove(runId);
        }
    }

    // ── Module 1: HCM upload ──────────────────────────────────────────────────

    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue)]
    public async Task<IActionResult> HcmUpload([FromForm] HcmUploadForm form, CancellationToken ct)
    {
        var hcmFile     = form.HcmFile;
        var stanbicFile = form.StanbicFile;
        if (hcmFile == null || stanbicFile == null)
            return Json(new { status = "error", message = "Both files are required." });

        // Pre-flight: check for unknown mappings (quick, synchronous)
        List<(string RawValue, string SourceColumn)> unknowns;
        try { unknowns = await _hcmSvc.CheckUnknownsAsync(hcmFile, stanbicFile); }
        catch (Exception ex)
        { return Json(new { status = "error", message = $"Could not read files: {ex.Message}" }); }

        // Always save to pending so the background task can read the files
        var runId   = Guid.NewGuid().ToString("N")[..12];
        var pending = PendingFolder(runId);
        Directory.CreateDirectory(pending);
        await SaveToFile(hcmFile,     Path.Combine(pending, "hcm.xlsx"));
        await SaveToFile(stanbicFile, Path.Combine(pending, "stanbic.xlsx"));

        if (unknowns.Count > 0)
            return Json(new
            {
                status   = "classify",
                runId,
                unknowns = unknowns.Select(u => new { u.RawValue, u.SourceColumn }).ToList()
            });

        // No unknowns — kick off background run and return stream runId
        StartHcmBackground(runId, pending);
        return Json(new { status = "running", runId });
    }

    // ── Module 1: save mappings + resume ─────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> SaveMappings(
        [FromBody] SaveMappingsRequest req, CancellationToken ct)
    {
        if (req.Mappings == null || req.Mappings.Count == 0)
            return Json(new { status = "error", message = "No mappings provided." });

        try
        {
            await _mappings.SaveMappingsAsync(req.Mappings.Select(m => new HcmMapping
            {
                RawValue       = m.RawValue,
                SourceColumn   = m.SourceColumn,
                Classification = m.Classification,
                CanonicalName  = string.IsNullOrWhiteSpace(m.CanonicalName) ? null : m.CanonicalName.Trim()
            }).ToList());
        }
        catch (Exception ex)
        { return Json(new { status = "error", message = $"Could not save mappings: {ex.Message}" }); }

        var pending     = PendingFolder(req.RunId);
        var hcmPath     = Path.Combine(pending, "hcm.xlsx");
        var stanbicPath = Path.Combine(pending, "stanbic.xlsx");

        if (!System.IO.File.Exists(hcmPath) || !System.IO.File.Exists(stanbicPath))
            return Json(new { status = "error", message = "Pending run files not found. Please re-upload." });

        StartHcmBackground(req.RunId, pending);
        return Json(new { status = "running", runId = req.RunId });
    }

    // ── Module 2: standalone IPPS ─────────────────────────────────────────────

    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue)]
    public async Task<IActionResult> IppsGenerate([FromForm] IppsUploadForm form, CancellationToken ct)
    {
        var ippsFile = form.IppsFile;
        if (ippsFile == null || ippsFile.Length == 0)
            return Json(new { status = "error", message = "Please select an IPPS file." });

        List<string> numbers;
        try { numbers = CrbReportService.ParseIppsFile(ippsFile); }
        catch (Exception ex)
        { return Json(new { status = "error", message = $"Could not parse file: {ex.Message}" }); }

        if (numbers.Count == 0)
            return Json(new { status = "error", message = "No valid IPPS numbers found." });

        // Buffer the file so the background task can read it after this request ends
        var runId    = _progress.CreateRun();
        var fileData = new MemoryStream();
        await ippsFile.CopyToAsync(fileData);
        fileData.Position = 0;

        var storageRoot = _storageRoot;
        _ = Task.Run(async () =>
        {
            using var scope  = _scopeFactory.CreateScope();
            var ippsSvc = scope.ServiceProvider.GetRequiredService<CrbReportService>();
            try
            {
                var result = await ippsSvc.GenerateAsync(
                    numbers, storageRoot,
                    msg => _progress.Report(runId, msg));

                var response = new
                {
                    status = "complete",
                    file   = new
                    {
                        url  = Url.Action("DownloadResult", "Crb", new { path = result.FilePath, name = result.FileName }),
                        name = result.FileName
                    },
                    stats = new
                    {
                        result.TotalSubmitted, result.Matched, result.Unmatched,
                        result.WithStat, result.WithAllow, result.WithDed,
                        result.WithStanbic, result.ZeroAfford
                    }
                };
                _progress.Complete(runId, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CRB IPPS background run {RunId} failed", runId);
                _progress.Fail(runId, ex.Message);
            }
        });

        return Json(new { status = "running", runId });
    }

    // ── Download ──────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> DownloadResult(string path, string name)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(Path.GetFullPath(_storageRoot), StringComparison.OrdinalIgnoreCase))
            return BadRequest();
        if (!System.IO.File.Exists(full)) return NotFound();
        var bytes = await System.IO.File.ReadAllBytesAsync(full);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void StartHcmBackground(string runId, string pendingFolder)
    {
        _progress.CreateRunWithId(runId);
        var storageRoot = _storageRoot;

        // Capture URL builder while HTTP context is still alive
        var urlBuilder = (string path, string name) =>
            Url.Action("DownloadResult", "Crb", new { path, name }) ?? "#";

        _ = Task.Run(async () =>
        {
            using var scope  = _scopeFactory.CreateScope();
            var hcmSvc = scope.ServiceProvider.GetRequiredService<HcmReportService>();
            try
            {
                using var hcmStream     = System.IO.File.OpenRead(Path.Combine(pendingFolder, "hcm.xlsx"));
                using var stanbicStream = System.IO.File.OpenRead(Path.Combine(pendingFolder, "stanbic.xlsx"));

                var result = await hcmSvc.RunAsync(
                    new PhysicalFormFile(hcmStream, "hcm.xlsx"),
                    new PhysicalFormFile(stanbicStream, "stanbic.xlsx"),
                    storageRoot,
                    msg => _progress.Report(runId, msg));

                try { Directory.Delete(pendingFolder, recursive: true); } catch { /* non-fatal */ }

                var response = BuildCompleteResponse(result, urlBuilder);
                _progress.Complete(runId, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CRB HCM background run {RunId} failed", runId);
                _progress.Fail(runId, ex.Message);
            }
        });
    }

    private static object BuildCompleteResponse(HcmRunResult result, Func<string, string, string> urlBuilder) => new
    {
        status   = "complete",
        hcmFile  = new { url = urlBuilder(result.HcmFilePath,  result.HcmFileName),  name = result.HcmFileName  },
        ippsFile = new { url = urlBuilder(result.IppsFilePath, result.IppsFileName), name = result.IppsFileName },
        stats    = new
        {
            result.TotalStanbicSubmitted, result.MatchedToHcm, result.PassedToIpps,
            result.WithStat, result.WithAllow, result.WithDed, result.WithStanbic, result.ZeroAfford
        }
    };

    private string PendingFolder(string runId)
        => Path.Combine(_storageRoot, "crb", "pending", runId);

    private static async Task SaveToFile(IFormFile file, string path)
    {
        using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs);
    }
}

// ── Form models for file uploads ──────────────────────────────────────────────

public class HcmUploadForm
{
    public IFormFile? HcmFile     { get; set; }
    public IFormFile? StanbicFile { get; set; }
}

public class IppsUploadForm
{
    public IFormFile? IppsFile { get; set; }
}

// ── Request model for SaveMappings ────────────────────────────────────────────

public class SaveMappingsRequest
{
    public string RunId { get; set; } = string.Empty;
    public List<MappingEntry> Mappings { get; set; } = new();
}

public class MappingEntry
{
    public string  RawValue       { get; set; } = string.Empty;
    public string  SourceColumn   { get; set; } = string.Empty;
    public string  Classification { get; set; } = string.Empty;
    public string? CanonicalName  { get; set; }
}

// ── Request model for UpdateMapping ──────────────────────────────────────────

public class UpdateMappingRequest
{
    public int     Id             { get; set; }
    public string  Classification { get; set; } = string.Empty;
    public string? CanonicalName  { get; set; }
    public string? Aliases        { get; set; }
}

// ── IFormFile wrapper for reading from disk/memory in background tasks ────────

internal sealed class PhysicalFormFile : IFormFile
{
    private readonly Stream _stream;
    private readonly string _name;

    public PhysicalFormFile(Stream stream, string name) { _stream = stream; _name = name; }

    public string           ContentType        => "application/octet-stream";
    public string           ContentDisposition => $"form-data; name=\"file\"; filename=\"{_name}\"";
    public IHeaderDictionary Headers           => new HeaderDictionary();
    public long             Length             => _stream.Length;
    public string           Name               => "file";
    public string           FileName           => _name;
    public void             CopyTo(Stream target)                             => _stream.CopyTo(target);
    public Task             CopyToAsync(Stream target, CancellationToken ct)  => _stream.CopyToAsync(target, ct);
    public Stream           OpenReadStream()   { _stream.Position = 0; return _stream; }
}
