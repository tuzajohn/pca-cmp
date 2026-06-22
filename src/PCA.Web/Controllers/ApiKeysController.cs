using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Identity.Models;
using PCA.Web.Services;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ApiKeysController : Controller
{
    private readonly IApiKeyService _service;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApiKeysController(IApiKeyService service, UserManager<ApplicationUser> userManager)
    {
        _service     = service;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
        => View(await _service.GetAllAsync());

    [HttpGet]
    public async Task<IActionResult> IndexData(int page = 1, int pageSize = 25, string? sortCol = null, string? sortDir = "desc")
    {
        var all = await _service.GetAllAsync();
        var sorted = sortCol switch {
            "appName"   => sortDir == "asc" ? all.OrderBy(k => k.AppName).ToList() : all.OrderByDescending(k => k.AppName).ToList(),
            "prefix"    => sortDir == "asc" ? all.OrderBy(k => k.KeyPrefix).ToList() : all.OrderByDescending(k => k.KeyPrefix).ToList(),
            "createdAt" => sortDir == "asc" ? all.OrderBy(k => k.CreatedAt).ToList() : all.OrderByDescending(k => k.CreatedAt).ToList(),
            "lastUsed"  => sortDir == "asc" ? all.OrderBy(k => k.LastUsedAt).ToList() : all.OrderByDescending(k => k.LastUsedAt).ToList(),
            _           => all.OrderByDescending(k => k.CreatedAt).ToList()
        };
        var totalCount = sorted.Count;
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        int totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

        return Json(new {
            items = items.Select(k => new {
                id        = k.Id,
                appName   = k.AppName,
                prefix    = k.KeyPrefix,
                isActive  = k.IsActive,
                createdAt = k.CreatedAt.ToString("dd MMM yyyy"),
                lastUsed  = k.LastUsedAt.HasValue ? k.LastUsedAt.Value.ToString("dd MMM yyyy HH:mm") : ""
            }),
            totalCount,
            currentPage = page,
            totalPages
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            TempData["Error"] = "Application name is required.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        var (rawKey, _) = await _service.CreateAsync(appName.Trim(), user!.Id);

        TempData["NewKey"]     = rawKey;
        TempData["NewKeyApp"]  = appName.Trim();
        TempData["Success"]    = $"API key created for {appName}. Copy it now — it will not be shown again.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id)
    {
        await _service.RevokeAsync(id);
        TempData["Success"] = "API key revoked.";
        return RedirectToAction(nameof(Index));
    }
}
