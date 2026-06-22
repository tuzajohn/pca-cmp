using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Identity.Models;
using PCA.Modules.Invoicing.Models;
using PCA.Modules.Invoicing.Services;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize(Policy = "Module:Invoicing")]
public class InvoiceRecipientsController : Controller
{
    private readonly IInvoicingService _svc;
    private readonly UserManager<ApplicationUser> _users;

    public InvoiceRecipientsController(IInvoicingService svc, UserManager<ApplicationUser> users)
    {
        _svc = svc;
        _users = users;
    }

    public async Task<IActionResult> Index() => View(await _svc.GetRecipientsAsync());

    [HttpGet]
    public async Task<IActionResult> IndexData(int page = 1, int pageSize = 20, string? sortCol = null, string? sortDir = "asc", string? isDefault = null, string? search = null)
    {
        var all = await _svc.GetRecipientsAsync();

        if (isDefault == "true")  all = all.Where(r => r.IsDefault).ToList();
        if (isDefault == "false") all = all.Where(r => !r.IsDefault).ToList();
        if (!string.IsNullOrEmpty(search))
            all = all.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.Email.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        var sorted = sortCol switch {
            "name"      => sortDir == "asc" ? all.OrderBy(r => r.Name).ToList() : all.OrderByDescending(r => r.Name).ToList(),
            "email"     => sortDir == "asc" ? all.OrderBy(r => r.Email).ToList() : all.OrderByDescending(r => r.Email).ToList(),
            "isDefault" => sortDir == "asc" ? all.OrderBy(r => r.IsDefault).ToList() : all.OrderByDescending(r => r.IsDefault).ToList(),
            _           => all.OrderBy(r => r.Name).ToList()
        };
        var totalCount = sorted.Count;
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        int totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

        return Json(new {
            items = items.Select(r => new {
                id        = r.Id,
                name      = r.Name,
                email     = r.Email,
                isDefault = r.IsDefault
            }),
            totalCount,
            currentPage = page,
            totalPages
        });
    }

    public IActionResult Create() => View(new InvoiceRecipientCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceRecipientCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = await _users.GetUserAsync(User);
        await _svc.CreateRecipientAsync(new InvoiceRecipient
        {
            Name        = vm.Name,
            Email       = vm.Email,
            IsDefault   = vm.IsDefault,
            CreatedById = user?.Id
        });
        TempData["Success"] = "Recipient added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var r = await _svc.GetRecipientByIdAsync(id);
        if (r == null) return NotFound();
        return View(new InvoiceRecipientEditViewModel { Id = r.Id, Name = r.Name, Email = r.Email, IsDefault = r.IsDefault });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InvoiceRecipientEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var r = await _svc.GetRecipientByIdAsync(vm.Id);
        if (r == null) return NotFound();
        r.Name      = vm.Name;
        r.Email     = vm.Email;
        r.IsDefault = vm.IsDefault;
        await _svc.UpdateRecipientAsync(r);
        TempData["Success"] = "Recipient updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteRecipientAsync(id);
        TempData["Success"] = "Recipient removed.";
        return RedirectToAction(nameof(Index));
    }
}
