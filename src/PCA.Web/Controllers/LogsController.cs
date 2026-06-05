using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PCA.Web.Data;
using PCA.Web.Services;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin")]
public class LogsController : Controller
{
    private readonly ILogService _logService;
    private readonly ApplicationDbContext _db;

    public LogsController(ILogService logService, ApplicationDbContext db)
    {
        _logService = logService;
        _db = db;
    }

    public async Task<IActionResult> Index(
        string? source, string? level, string? category, string? search,
        string? from, string? to, int page = 1)
    {
        DateTime? fromDate = DateTime.TryParse(from, out var fd) ? fd.ToUniversalTime() : null;
        DateTime? toDate   = DateTime.TryParse(to,   out var td) ? td.ToUniversalTime().AddDays(1) : null;

        var (logs, total) = await _logService.QueryAsync(source, level, category, search,
            fromDate, toDate, page, 50);

        ViewBag.Sources    = await _db.AppLogs.Select(l => l.Source).Distinct().OrderBy(s => s).ToListAsync();
        ViewBag.Source     = source;
        ViewBag.Level      = level;
        ViewBag.Category   = category;
        ViewBag.Search     = search;
        ViewBag.From       = from;
        ViewBag.To         = to;
        ViewBag.Page       = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / 50.0);
        ViewBag.Total      = total;

        return View(logs);
    }

    public async Task<IActionResult> Details(int id)
    {
        var log = await _db.AppLogs.FindAsync(id);
        if (log == null) return NotFound();
        return View(log);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(string? source, string? olderThanDays)
    {
        var query = _db.AppLogs.AsQueryable();
        if (!string.IsNullOrEmpty(source)) query = query.Where(l => l.Source == source);
        if (int.TryParse(olderThanDays, out var days))
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            query = query.Where(l => l.Timestamp < cutoff);
        }
        _db.AppLogs.RemoveRange(query);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Logs cleared.";
        return RedirectToAction(nameof(Index));
    }
}
