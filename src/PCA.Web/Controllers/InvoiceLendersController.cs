using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Identity.Models;
using PCA.Modules.Invoicing.Models;
using PCA.Modules.Invoicing.Services;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize(Policy = "Module:Invoicing")]
public class InvoiceLendersController : Controller
{
    private readonly IInvoicingService _svc;
    private readonly InvoiceDataService _dataSvc;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<InvoiceLendersController> _logger;

    public InvoiceLendersController(
        IInvoicingService svc,
        InvoiceDataService dataSvc,
        UserManager<ApplicationUser> users,
        ILogger<InvoiceLendersController> logger)
    {
        _svc     = svc;
        _dataSvc = dataSvc;
        _users   = users;
        _logger  = logger;
    }

    public async Task<IActionResult> Index() => View(await _svc.GetLendersAsync());

    [HttpGet]
    public async Task<IActionResult> IndexData(int page = 1, int pageSize = 25, string? sortCol = null, string? sortDir = "asc")
    {
        var all = await _svc.GetLendersAsync();
        var sorted = sortCol switch {
            "name"          => sortDir == "asc" ? all.OrderBy(l => l.Name).ToList() : all.OrderByDescending(l => l.Name).ToList(),
            "companyType"   => sortDir == "asc" ? all.OrderBy(l => l.CompanyType).ToList() : all.OrderByDescending(l => l.CompanyType).ToList(),
            "deductionCode" => sortDir == "asc" ? all.OrderBy(l => l.DeductionCode).ToList() : all.OrderByDescending(l => l.DeductionCode).ToList(),
            _               => all.OrderBy(l => l.Name).ToList()
        };
        var totalCount = sorted.Count;
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        int totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

        return Json(new {
            items = items.Select(l => new {
                id            = l.Id,
                name          = l.Name,
                companyType   = l.CompanyType ?? "",
                deductionCode = l.DeductionCode ?? "",
                isActive      = l.IsActive
            }),
            totalCount,
            currentPage = page,
            totalPages
        });
    }

    public IActionResult Create() => View(new InvoiceLenderCreateViewModel());

    /// <summary>
    /// AJAX endpoint — queries IPPS companies table for the selected company type
    /// and returns the results so the form can populate Name and DeductionCode.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FetchCompanies(string companyType)
    {
        if (string.IsNullOrWhiteSpace(companyType))
            return Json(new List<object>());

        try
        {
            var companies = await _dataSvc.FetchCompaniesByTypeAsync(companyType);
            return Json(companies.Select(c => new
            {
                id            = c.Id,
                companyName   = c.CompanyName,
                deductionType = c.DeductionType
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FetchCompanies [{CompanyType}]: error occurred", companyType);
            return Json(new { error = ex.Message });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceLenderBulkCreateViewModel vm)
    {
        var selected = vm.Lenders.Where(l => l.Selected && !string.IsNullOrWhiteSpace(l.Name)).ToList();
        if (!selected.Any())
        {
            TempData["Error"] = "Select at least one company to save.";
            return View(new InvoiceLenderCreateViewModel());
        }

        var user = await _users.GetUserAsync(User);
        int saved = 0;
        foreach (var item in selected)
        {
            await _svc.CreateLenderAsync(new InvoiceLender
            {
                Name          = item.Name,
                CompanyType   = vm.CompanyType,
                DeductionCode = item.DeductionCode,
                IsActive      = item.IsActive,
                CreatedById   = user?.Id
            });
            saved++;
        }

        TempData["Success"] = $"{saved} lender(s) saved.";
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
