using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Invoicing.Services;
using PCA.Modules.Identity.Models;

namespace PCA.Web.Controllers;

[Authorize]
public class CrbController : Controller
{
    private readonly CrbReportService _svc;
    private readonly string _storageRoot;

    public CrbController(CrbReportService svc, IConfiguration config,
        IWebHostEnvironment env)
    {
        _svc         = svc;
        _storageRoot = config["InvoiceStoragePath"]
            ?? Path.Combine(env.ContentRootPath, "uploads", "documents");
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(IFormFile? ippsFile, CancellationToken ct)
    {
        if (ippsFile == null || ippsFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Please select an IPPS file to upload.");
            return View("Index");
        }

        var ext = Path.GetExtension(ippsFile.FileName).ToLowerInvariant();
        if (ext is not (".txt" or ".csv" or ".xlsx" or ".xls"))
        {
            ModelState.AddModelError(string.Empty,
                "Unsupported file type. Upload a .txt, .csv, or .xlsx file.");
            return View("Index");
        }

        List<string> numbers;
        try
        {
            numbers = CrbReportService.ParseIppsFile(ippsFile);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Could not parse file: {ex.Message}");
            return View("Index");
        }

        if (numbers.Count == 0)
        {
            ModelState.AddModelError(string.Empty,
                "No valid IPPS numbers found in the uploaded file.");
            return View("Index");
        }

        CrbReportResult result;
        try
        {
            result = await _svc.GenerateAsync(numbers, _storageRoot, ct);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty,
                $"Report generation failed: {ex.Message}");
            return View("Index");
        }

        TempData["CrbStats"] = System.Text.Json.JsonSerializer.Serialize(result);

        var bytes = await System.IO.File.ReadAllBytesAsync(result.FilePath, ct);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.FileName);
    }
}
