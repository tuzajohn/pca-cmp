using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Invoicing.Models;
using PCA.Modules.Invoicing.Services;

namespace PCA.Web.Controllers;

[Authorize]
public class CrbController : Controller
{
    private readonly HcmReportService  _hcmSvc;
    private readonly CrbReportService  _ippsSvc;
    private readonly HcmMappingService _mappings;
    private readonly string _storageRoot;

    public CrbController(
        HcmReportService hcmSvc,
        CrbReportService ippsSvc,
        HcmMappingService mappings,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _hcmSvc      = hcmSvc;
        _ippsSvc     = ippsSvc;
        _mappings    = mappings;
        _storageRoot = config["InvoiceStoragePath"]
            ?? Path.Combine(env.ContentRootPath, "uploads", "documents");
    }

    // ── Pages ─────────────────────────────────────────────────────────────────

    [HttpGet] public IActionResult Index()   => View();
    [HttpGet] public IActionResult HcmRun()  => View();
    [HttpGet] public IActionResult IppsRun() => View();

    [HttpGet]
    public async Task<IActionResult> Mappings()
        => View(await _mappings.GetAllAsync());

    // ── Module 1: HCM upload — JSON endpoint ─────────────────────────────────

    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue)]
    public async Task<IActionResult> HcmUpload(
        [FromForm] IFormFile? hcmFile, [FromForm] IFormFile? stanbicFile, CancellationToken ct)
    {
        if (hcmFile == null || stanbicFile == null)
            return Json(new { status = "error", message = "Both files are required." });

        List<(string RawValue, string SourceColumn)> unknowns;
        try { unknowns = await _hcmSvc.CheckUnknownsAsync(hcmFile, stanbicFile); }
        catch (Exception ex)
        { return Json(new { status = "error", message = $"Could not read files: {ex.Message}" }); }

        if (unknowns.Count > 0)
        {
            var runId   = Guid.NewGuid().ToString("N")[..12];
            var pending = PendingFolder(runId);
            Directory.CreateDirectory(pending);
            await SaveToFile(hcmFile,     Path.Combine(pending, "hcm.xlsx"));
            await SaveToFile(stanbicFile, Path.Combine(pending, "stanbic.xlsx"));

            return Json(new
            {
                status   = "classify",
                runId,
                unknowns = unknowns.Select(u => new { u.RawValue, u.SourceColumn }).ToList()
            });
        }

        return await RunAndRespond(hcmFile, stanbicFile, ct);
    }

    // ── Module 1: save mappings + resume — JSON endpoint ──────────────────────

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
                Classification = m.Classification
            }).ToList());
        }
        catch (Exception ex)
        { return Json(new { status = "error", message = $"Could not save mappings: {ex.Message}" }); }

        var pending     = PendingFolder(req.RunId);
        var hcmPath     = Path.Combine(pending, "hcm.xlsx");
        var stanbicPath = Path.Combine(pending, "stanbic.xlsx");

        if (!System.IO.File.Exists(hcmPath) || !System.IO.File.Exists(stanbicPath))
            return Json(new { status = "error", message = "Pending run files not found. Please re-upload." });

        try
        {
            using var hcmStream     = System.IO.File.OpenRead(hcmPath);
            using var stanbicStream = System.IO.File.OpenRead(stanbicPath);
            var result = await _hcmSvc.RunAsync(
                new PhysicalFormFile(hcmStream, "hcm.xlsx"),
                new PhysicalFormFile(stanbicStream, "stanbic.xlsx"),
                _storageRoot, ct);

            try { Directory.Delete(pending, recursive: true); } catch { /* non-fatal */ }
            return Json(BuildCompleteResponse(result));
        }
        catch (Exception ex)
        { return Json(new { status = "error", message = ex.Message }); }
    }

    // ── Module 2: standalone IPPS — JSON endpoint ─────────────────────────────

    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue)]
    public async Task<IActionResult> IppsGenerate([FromForm] IFormFile? ippsFile, CancellationToken ct)
    {
        if (ippsFile == null || ippsFile.Length == 0)
            return Json(new { status = "error", message = "Please select an IPPS file." });

        List<string> numbers;
        try { numbers = CrbReportService.ParseIppsFile(ippsFile); }
        catch (Exception ex)
        { return Json(new { status = "error", message = $"Could not parse file: {ex.Message}" }); }

        if (numbers.Count == 0)
            return Json(new { status = "error", message = "No valid IPPS numbers found." });

        CrbReportResult result;
        try { result = await _ippsSvc.GenerateAsync(numbers, _storageRoot, ct); }
        catch (Exception ex)
        { return Json(new { status = "error", message = ex.Message }); }

        return Json(new
        {
            status = "complete",
            file   = new { url = Url.Action("DownloadResult", new { path = result.FilePath, name = result.FileName }), name = result.FileName },
            stats  = new
            {
                result.TotalSubmitted, result.Matched, result.Unmatched,
                result.WithStat, result.WithAllow, result.WithDed,
                result.WithStanbic, result.ZeroAfford
            }
        });
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

    private async Task<IActionResult> RunAndRespond(
        IFormFile hcmFile, IFormFile stanbicFile, CancellationToken ct)
    {
        try
        {
            var result = await _hcmSvc.RunAsync(hcmFile, stanbicFile, _storageRoot, ct);
            return Json(BuildCompleteResponse(result));
        }
        catch (Exception ex)
        { return Json(new { status = "error", message = ex.Message }); }
    }

    private object BuildCompleteResponse(HcmRunResult result) => new
    {
        status   = "complete",
        hcmFile  = new { url = Url.Action("DownloadResult", new { path = result.HcmFilePath,  name = result.HcmFileName  }), name = result.HcmFileName  },
        ippsFile = new { url = Url.Action("DownloadResult", new { path = result.IppsFilePath, name = result.IppsFileName }), name = result.IppsFileName },
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

// ── Request model for SaveMappings ────────────────────────────────────────────

public class SaveMappingsRequest
{
    public string RunId { get; set; } = string.Empty;
    public List<MappingEntry> Mappings { get; set; } = new();
}

public class MappingEntry
{
    public string RawValue       { get; set; } = string.Empty;
    public string SourceColumn   { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
}

// ── IFormFile wrapper for resuming from disk ──────────────────────────────────

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
