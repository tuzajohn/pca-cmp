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

    // ── Module 1: HCM upload — JS fetch endpoint ──────────────────────────────

    [HttpPost]
    public async Task<IActionResult> HcmUpload(
        IFormFile? hcmFile, IFormFile? stanbicFile, CancellationToken ct)
    {
        if (hcmFile == null || stanbicFile == null)
            return Json(new { status = "error", message = "Both files are required." });

        // Check for unknown mappings before running
        List<(string RawValue, string SourceColumn)> unknowns;
        try { unknowns = await _hcmSvc.CheckUnknownsAsync(hcmFile, stanbicFile); }
        catch (Exception ex)
        { return Json(new { status = "error", message = $"Could not read files: {ex.Message}" }); }

        if (unknowns.Count > 0)
        {
            // Save files for resume
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

    // ── Module 1: save mappings + resume — JS fetch endpoint ──────────────────

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

        // Resume from saved pending files
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

    // ── Module 2: standalone IPPS — JS fetch endpoint ─────────────────────────

    [HttpPost]
    public async Task<IActionResult> IppsGenerate(IFormFile? ippsFile, CancellationToken ct)
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

    // ── Helpers ───────────────────────────────────────────────────────────────

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
        status  = "complete",
        hcmFile = new { url = Url.Action("DownloadResult", new { path = result.HcmFilePath,  name = result.HcmFileName  }), name = result.HcmFileName  },
        ippsFile= new { url = Url.Action("DownloadResult", new { path = result.IppsFilePath, name = result.IppsFileName }), name = result.IppsFileName },
        stats   = new
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

// ── Request model for SaveMappings JSON body ──────────────────────────────────

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
    public string ContentType        => "application/octet-stream";
    public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{_name}\"";
    public IHeaderDictionary Headers => new HeaderDictionary();
    public long   Length             => _stream.Length;
    public string Name               => "file";
    public string FileName           => _name;
    public void   CopyTo(Stream target)                             => _stream.CopyTo(target);
    public Task   CopyToAsync(Stream target, CancellationToken ct)  => _stream.CopyToAsync(target, ct);
    public Stream OpenReadStream() { _stream.Position = 0; return _stream; }
}


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

    // ── Landing ───────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Index() => View();

    // ── Module 1: HCM + IPPS combined run ────────────────────────────────────

    [HttpGet]
    public IActionResult HcmRun() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HcmUpload(
        IFormFile? hcmFile, IFormFile? stanbicFile, CancellationToken ct)
    {
        if (hcmFile == null || stanbicFile == null)
        {
            ModelState.AddModelError(string.Empty, "Both files are required.");
            return View("HcmRun");
        }

        // Pre-flight: check for unknown mapping values before running
        List<(string RawValue, string SourceColumn)> unknowns;
        try
        {
            unknowns = await _hcmSvc.CheckUnknownsAsync(hcmFile, stanbicFile);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Could not read files: {ex.Message}");
            return View("HcmRun");
        }

        if (unknowns.Count > 0)
        {
            // Save uploaded files to a pending run folder
            var runId   = Guid.NewGuid().ToString("N")[..12];
            var pending = PendingFolder(runId);
            Directory.CreateDirectory(pending);

            await SaveToFile(hcmFile,     Path.Combine(pending, "hcm.xlsx"));
            await SaveToFile(stanbicFile, Path.Combine(pending, "stanbic.xlsx"));

            ViewBag.RunId    = runId;
            ViewBag.Unknowns = unknowns;
            return View("ClassifyMappings");
        }

        return await ExecuteHcmRun(hcmFile, stanbicFile, ct);
    }

    // ── Classification form submission ────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMappings(
        string runId,
        List<string> rawValues,
        List<string> sourceColumns,
        List<string> classifications,
        CancellationToken ct)
    {
        if (rawValues.Count != classifications.Count)
        {
            TempData["Error"] = "Classification data was incomplete.";
            return RedirectToAction(nameof(HcmRun));
        }

        var mappings = rawValues.Select((rv, i) => new HcmMapping
        {
            RawValue       = rv,
            SourceColumn   = sourceColumns[i],
            Classification = classifications[i]
        }).ToList();

        try { await _mappings.SaveMappingsAsync(mappings); }
        catch (Exception ex)
        {
            TempData["Error"] = $"Could not save mappings: {ex.Message}";
            return RedirectToAction(nameof(HcmRun));
        }

        // Resume run with saved pending files
        var pending     = PendingFolder(runId);
        var hcmPath     = Path.Combine(pending, "hcm.xlsx");
        var stanbicPath = Path.Combine(pending, "stanbic.xlsx");

        if (!System.IO.File.Exists(hcmPath) || !System.IO.File.Exists(stanbicPath))
        {
            TempData["Error"] = "Pending run files not found. Please re-upload.";
            return RedirectToAction(nameof(HcmRun));
        }

        HcmRunResult result;
        try
        {
            using var hcmStream     = System.IO.File.OpenRead(hcmPath);
            using var stanbicStream = System.IO.File.OpenRead(stanbicPath);
            var hcmFormFile     = new PhysicalFormFile(hcmStream, "hcm.xlsx");
            var stanbicFormFile = new PhysicalFormFile(stanbicStream, "stanbic.xlsx");

            result = await _hcmSvc.RunAsync(hcmFormFile, stanbicFormFile, _storageRoot, ct);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Report generation failed: {ex.Message}";
            return RedirectToAction(nameof(HcmRun));
        }
        finally
        {
            // Clean up pending files
            try { Directory.Delete(pending, recursive: true); } catch { /* non-fatal */ }
        }

        return ShowRunResults(result);
    }

    // ── Download completed run files ──────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> DownloadResult(string path, string name)
    {
        // Validate path stays within storage root to prevent traversal
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(Path.GetFullPath(_storageRoot), StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        if (!System.IO.File.Exists(full)) return NotFound();
        var bytes = await System.IO.File.ReadAllBytesAsync(full);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            name);
    }

    // ── Module 2: Standalone IPPS run ─────────────────────────────────────────

    [HttpGet]
    public IActionResult IppsRun() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> IppsGenerate(IFormFile? ippsFile, CancellationToken ct)
    {
        if (ippsFile == null || ippsFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Please select an IPPS file.");
            return View("IppsRun");
        }

        List<string> numbers;
        try { numbers = CrbReportService.ParseIppsFile(ippsFile); }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Could not parse file: {ex.Message}");
            return View("IppsRun");
        }

        if (numbers.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "No valid IPPS numbers found.");
            return View("IppsRun");
        }

        CrbReportResult result;
        try { result = await _ippsSvc.GenerateAsync(numbers, _storageRoot, ct); }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Report generation failed: {ex.Message}");
            return View("IppsRun");
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(result.FilePath, ct);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.FileName);
    }

    // ── Mapping table admin ───────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Mappings()
    {
        var mappings = await _mappings.GetAllAsync();
        return View(mappings);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<IActionResult> ExecuteHcmRun(
        IFormFile hcmFile, IFormFile stanbicFile, CancellationToken ct)
    {
        HcmRunResult result;
        try { result = await _hcmSvc.RunAsync(hcmFile, stanbicFile, _storageRoot, ct); }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Report generation failed: {ex.Message}");
            return View("HcmRun");
        }
        return ShowRunResults(result);
    }

    private IActionResult ShowRunResults(HcmRunResult result)
    {
        ViewBag.Result = result;
        return View("HcmResults");
    }

    private string PendingFolder(string runId)
        => Path.Combine(_storageRoot, "crb", "pending", runId);

    private static async Task SaveToFile(IFormFile file, string path)
    {
        using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs);
    }
}

// Minimal IFormFile wrapper around a FileStream (for resume-from-disk)
internal sealed class PhysicalFormFile : Microsoft.AspNetCore.Http.IFormFile
{
    private readonly Stream _stream;
    private readonly string _name;

    public PhysicalFormFile(Stream stream, string name) { _stream = stream; _name = name; }

    public string ContentType        => "application/octet-stream";
    public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{_name}\"";
    public Microsoft.AspNetCore.Http.IHeaderDictionary Headers => new Microsoft.AspNetCore.Http.HeaderDictionary();
    public long Length               => _stream.Length;
    public string Name               => "file";
    public string FileName           => _name;
    public void CopyTo(Stream target) => _stream.CopyTo(target);
    public Task CopyToAsync(Stream target, CancellationToken ct = default) => _stream.CopyToAsync(target, ct);
    public Stream OpenReadStream()   { _stream.Position = 0; return _stream; }
}
