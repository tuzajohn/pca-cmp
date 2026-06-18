using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Invoicing.Services;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin")]
public class InvoiceRunsController : Controller
{
    private readonly IInvoicingService _svc;

    public InvoiceRunsController(IInvoicingService svc) => _svc = svc;

    public async Task<IActionResult> Details(int id)
    {
        var run = await _svc.GetRunByIdAsync(id);
        if (run == null) return NotFound();
        return View(run);
    }

    public async Task<IActionResult> Download(int id)
    {
        var run = await _svc.GetRunByIdAsync(id);
        if (run == null || string.IsNullOrEmpty(run.FilePath) || !System.IO.File.Exists(run.FilePath))
            return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(run.FilePath);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            run.FileName ?? Path.GetFileName(run.FilePath));
    }
}
