using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Identity.Models;
using PCA.Modules.Invoicing.Models;
using PCA.Modules.Invoicing.Services;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin")]
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
