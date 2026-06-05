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
