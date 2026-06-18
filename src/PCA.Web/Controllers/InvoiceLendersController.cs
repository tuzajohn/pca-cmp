using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Identity.Models;
using PCA.Modules.Invoicing.Models;
using PCA.Modules.Invoicing.Services;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin")]
public class InvoiceLendersController : Controller
{
    private readonly IInvoicingService _svc;
    private readonly UserManager<ApplicationUser> _users;

    public InvoiceLendersController(IInvoicingService svc, UserManager<ApplicationUser> users)
    {
        _svc = svc;
        _users = users;
    }

    public async Task<IActionResult> Index() => View(await _svc.GetLendersAsync());

    public IActionResult Create() => View(new InvoiceLenderCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceLenderCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = await _users.GetUserAsync(User);
        await _svc.CreateLenderAsync(new InvoiceLender
        {
            Name          = vm.Name,
            CompanyType   = vm.CompanyType,
            DeductionCode = vm.DeductionCode,
            IsActive      = vm.IsActive,
            CreatedById   = user?.Id
        });
        TempData["Success"] = "Lender created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var lender = await _svc.GetLenderByIdAsync(id);
        if (lender == null) return NotFound();
        return View(new InvoiceLenderEditViewModel
        {
            Id            = lender.Id,
            Name          = lender.Name,
            CompanyType   = lender.CompanyType,
            DeductionCode = lender.DeductionCode,
            IsActive      = lender.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InvoiceLenderEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var lender = await _svc.GetLenderByIdAsync(vm.Id);
        if (lender == null) return NotFound();
        lender.Name          = vm.Name;
        lender.CompanyType   = vm.CompanyType;
        lender.DeductionCode = vm.DeductionCode;
        lender.IsActive      = vm.IsActive;
        await _svc.UpdateLenderAsync(lender);
        TempData["Success"] = "Lender updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteLenderAsync(id);
        TempData["Success"] = "Lender deleted.";
        return RedirectToAction(nameof(Index));
    }
}
