using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Invoicing.Models;
using PCA.Modules.Invoicing.Services;
using System.Text.Json;

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
