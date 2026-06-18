using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PCA.Modules.Identity.Models;
using PCA.Modules.Invoicing.Models;
using PCA.Modules.Invoicing.Services;
using PCA.Web.Models;

namespace PCA.Web.Controllers;

[Authorize(Roles = "Admin")]
public class InvoiceSchedulesController : Controller
{
    private readonly IInvoicingService _svc;
    private readonly UserManager<ApplicationUser> _users;
    private readonly InvoiceRunOrchestrator _orchestrator;

    public InvoiceSchedulesController(
        IInvoicingService svc,
        UserManager<ApplicationUser> users,
        InvoiceRunOrchestrator orchestrator)
    {
        _svc = svc;
        _users = users;
        _orchestrator = orchestrator;
    }

    public async Task<IActionResult> Index() => View(await _svc.GetSchedulesAsync());

    public async Task<IActionResult> Create()
    {
        ViewBag.Lenders    = await _svc.GetLendersAsync();
        ViewBag.Recipients = await _svc.GetRecipientsAsync();
        return View(new InvoiceScheduleCreateViewModel
        {
            SelectedRecipientIds = (await _svc.GetRecipientsAsync())
                .Where(r => r.IsDefault).Select(r => r.Id).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceScheduleCreateViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Lenders    = await _svc.GetLendersAsync();
            ViewBag.Recipients = await _svc.GetRecipientsAsync();
            return View(vm);
        }

        var user = await _users.GetUserAsync(User);
        var schedule = new InvoiceSchedule
        {
            Name        = vm.Name,
            LenderId    = vm.LenderId,
            Frequency   = vm.Frequency,
            DayOfWeek   = vm.DayOfWeek,
            DayOfMonth  = vm.DayOfMonth,
            TimeOfDay   = vm.TimeOfDay,
            IsEnabled   = vm.IsEnabled,
            CreatedById = user?.Id
        };
        schedule.NextRunAt = ScheduleCronHelper.NextOccurrence(schedule);

        await _svc.CreateScheduleAsync(schedule, vm.SelectedRecipientIds);
        TempData["Success"] = "Schedule created.";
        return RedirectToAction(nameof(Details), new { id = schedule.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var schedule = await _svc.GetScheduleByIdAsync(id);
        if (schedule == null) return NotFound();
        ViewBag.Description = ScheduleCronHelper.Describe(schedule);
        return View(schedule);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var s = await _svc.GetScheduleByIdAsync(id);
        if (s == null) return NotFound();
        ViewBag.Lenders    = await _svc.GetLendersAsync();
        ViewBag.Recipients = await _svc.GetRecipientsAsync();
        return View(new InvoiceScheduleEditViewModel
        {
            Id                   = s.Id,
            Name                 = s.Name,
            LenderId             = s.LenderId,
            Frequency            = s.Frequency,
            DayOfWeek            = s.DayOfWeek,
            DayOfMonth           = s.DayOfMonth,
            TimeOfDay            = s.TimeOfDay,
            IsEnabled            = s.IsEnabled,
            SelectedRecipientIds = s.ScheduleRecipients.Select(sr => sr.InvoiceRecipientId).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InvoiceScheduleEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Lenders    = await _svc.GetLendersAsync();
            ViewBag.Recipients = await _svc.GetRecipientsAsync();
            return View(vm);
        }

        var s = await _svc.GetScheduleByIdAsync(vm.Id);
        if (s == null) return NotFound();
        s.Name       = vm.Name;
        s.LenderId   = vm.LenderId;
        s.Frequency  = vm.Frequency;
        s.DayOfWeek  = vm.DayOfWeek;
        s.DayOfMonth = vm.DayOfMonth;
        s.TimeOfDay  = vm.TimeOfDay;
        s.IsEnabled  = vm.IsEnabled;
        s.NextRunAt  = ScheduleCronHelper.NextOccurrence(s);

        await _svc.UpdateScheduleAsync(s, vm.SelectedRecipientIds);
        TempData["Success"] = "Schedule updated.";
        return RedirectToAction(nameof(Details), new { id = s.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteScheduleAsync(id);
        TempData["Success"] = "Schedule deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(int id)
    {
        var schedule = await _svc.GetScheduleByIdAsync(id);
        if (schedule == null) return NotFound();

        var user = await _users.GetUserAsync(User);
        await _orchestrator.ExecuteAsync(schedule, user?.Id, HttpContext.RequestAborted);

        TempData["Success"] = "Invoice run triggered.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
