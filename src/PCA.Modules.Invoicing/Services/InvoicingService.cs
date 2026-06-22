using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using PageSort;
using PCA.Modules.Invoicing.Models;

namespace PCA.Modules.Invoicing.Services;

public class InvoicingService : IInvoicingService
{
    private readonly IApplicationDbContextForInvoicing _db;

    public InvoicingService(IApplicationDbContextForInvoicing db) => _db = db;

    // Lenders
    public Task<List<InvoiceLender>> GetLendersAsync() =>
        _db.InvoiceLenders.Include(l => l.CreatedBy).OrderBy(l => l.Name).ToListAsync();

    public Task<InvoiceLender?> GetLenderByIdAsync(int id) =>
        _db.InvoiceLenders.Include(l => l.CreatedBy).FirstOrDefaultAsync(l => l.Id == id);

    public async Task<InvoiceLender> CreateLenderAsync(InvoiceLender lender)
    {
        _db.InvoiceLenders.Add(lender);
        await _db.SaveChangesAsync();
        return lender;
    }

    public async Task UpdateLenderAsync(InvoiceLender lender)
    {
        _db.InvoiceLenders.Update(lender);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteLenderAsync(int id)
    {
        var l = await _db.InvoiceLenders.FindAsync(id);
        if (l != null) { _db.InvoiceLenders.Remove(l); await _db.SaveChangesAsync(); }
    }

    // Recipients
    public Task<List<InvoiceRecipient>> GetRecipientsAsync() =>
        _db.InvoiceRecipients.OrderBy(r => r.Name).ToListAsync();

    public Task<InvoiceRecipient?> GetRecipientByIdAsync(int id) =>
        _db.InvoiceRecipients.FirstOrDefaultAsync(r => r.Id == id);

    public async Task<InvoiceRecipient> CreateRecipientAsync(InvoiceRecipient recipient)
    {
        _db.InvoiceRecipients.Add(recipient);
        await _db.SaveChangesAsync();
        return recipient;
    }

    public async Task UpdateRecipientAsync(InvoiceRecipient recipient)
    {
        _db.InvoiceRecipients.Update(recipient);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteRecipientAsync(int id)
    {
        var r = await _db.InvoiceRecipients.FindAsync(id);
        if (r != null) { _db.InvoiceRecipients.Remove(r); await _db.SaveChangesAsync(); }
    }

    // Schedules
    public Task<List<InvoiceSchedule>> GetSchedulesAsync() =>
        _db.InvoiceSchedules
            .Include(s => s.Lender)
            .Include(s => s.CreatedBy)
            .Include(s => s.ScheduleRecipients).ThenInclude(sr => sr.Recipient)
            .OrderBy(s => s.Name)
            .ToListAsync();

    public Task<InvoiceSchedule?> GetScheduleByIdAsync(int id) =>
        _db.InvoiceSchedules
            .Include(s => s.Lender)
            .Include(s => s.CreatedBy)
            .Include(s => s.ScheduleRecipients).ThenInclude(sr => sr.Recipient)
            .Include(s => s.Runs).ThenInclude(r => r.TriggeredBy)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<InvoiceSchedule> CreateScheduleAsync(InvoiceSchedule schedule, List<int> recipientIds)
    {
        _db.InvoiceSchedules.Add(schedule);
        await _db.SaveChangesAsync();
        foreach (var rid in recipientIds)
            _db.InvoiceScheduleRecipients.Add(new InvoiceScheduleRecipient { InvoiceScheduleId = schedule.Id, InvoiceRecipientId = rid });
        await _db.SaveChangesAsync();
        return schedule;
    }

    public async Task UpdateScheduleAsync(InvoiceSchedule schedule, List<int> recipientIds)
    {
        _db.InvoiceSchedules.Update(schedule);
        var existing = await _db.InvoiceScheduleRecipients
            .Where(sr => sr.InvoiceScheduleId == schedule.Id).ToListAsync();
        foreach (var e in existing) _db.InvoiceScheduleRecipients.Remove(e);
        foreach (var rid in recipientIds)
            _db.InvoiceScheduleRecipients.Add(new InvoiceScheduleRecipient { InvoiceScheduleId = schedule.Id, InvoiceRecipientId = rid });
        await _db.SaveChangesAsync();
    }

    public async Task DeleteScheduleAsync(int id)
    {
        var s = await _db.InvoiceSchedules.FindAsync(id);
        if (s != null) { _db.InvoiceSchedules.Remove(s); await _db.SaveChangesAsync(); }
    }

    public Task<List<InvoiceSchedule>> GetDueSchedulesAsync() =>
        _db.InvoiceSchedules
            .Include(s => s.Lender)
            .Include(s => s.ScheduleRecipients).ThenInclude(sr => sr.Recipient)
            .Where(s => s.IsEnabled && s.NextRunAt != null && s.NextRunAt <= DateTime.UtcNow)
            .ToListAsync();

    public async Task UpdateScheduleNextRunAsync(int scheduleId, DateTime? lastRunAt, DateTime? nextRunAt)
    {
        var s = await _db.InvoiceSchedules.FindAsync(scheduleId);
        if (s == null) return;
        s.LastRunAt = lastRunAt;
        s.NextRunAt = nextRunAt;
        await _db.SaveChangesAsync();
    }

    // HCM Ref Files
    public Task<List<InvoiceHcmRefFile>> GetHcmRefFilesAsync(int scheduleId) =>
        _db.InvoiceHcmRefFiles
            .Include(f => f.UploadedBy)
            .Where(f => f.ScheduleId == scheduleId)
            .OrderByDescending(f => f.MonthYear)
            .ToListAsync();

    public Task<InvoiceHcmRefFile?> GetHcmRefFileForMonthAsync(int scheduleId, string monthYear) =>
        _db.InvoiceHcmRefFiles
            .FirstOrDefaultAsync(f => f.ScheduleId == scheduleId && f.MonthYear == monthYear);

    public async Task<InvoiceHcmRefFile> SaveHcmRefFileAsync(InvoiceHcmRefFile refFile)
    {
        var existing = await _db.InvoiceHcmRefFiles
            .FirstOrDefaultAsync(f => f.ScheduleId == refFile.ScheduleId && f.MonthYear == refFile.MonthYear);
        if (existing != null)
        {
            existing.FilePath = refFile.FilePath;
            existing.OriginalFileName = refFile.OriginalFileName;
            existing.UploadedAt = refFile.UploadedAt;
            existing.UploadedById = refFile.UploadedById;
            _db.InvoiceHcmRefFiles.Update(existing);
            await _db.SaveChangesAsync();
            return existing;
        }
        _db.InvoiceHcmRefFiles.Add(refFile);
        await _db.SaveChangesAsync();
        return refFile;
    }

    public async Task DeleteHcmRefFileAsync(int id)
    {
        var f = await _db.InvoiceHcmRefFiles.FindAsync(id);
        if (f != null) { _db.InvoiceHcmRefFiles.Remove(f); await _db.SaveChangesAsync(); }
    }

    // Runs
    public Task<List<InvoiceRun>> GetRunsForScheduleAsync(int scheduleId) =>
        _db.InvoiceRuns
            .Include(r => r.TriggeredBy)
            .Where(r => r.ScheduleId == scheduleId)
            .OrderByDescending(r => r.TriggeredAt)
            .ToListAsync();

    public Task<InvoiceRun?> GetRunByIdAsync(int id) =>
        _db.InvoiceRuns
            .Include(r => r.Schedule).ThenInclude(s => s!.Lender)
            .Include(r => r.TriggeredBy)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<InvoiceRun> CreateRunAsync(InvoiceRun run)
    {
        _db.InvoiceRuns.Add(run);
        await _db.SaveChangesAsync();
        return run;
    }

    public async Task UpdateRunAsync(InvoiceRun run)
    {
        _db.InvoiceRuns.Update(run);
        await _db.SaveChangesAsync();
    }

    public Task<PagedResult<InvoiceRun>> GetRunsPagedAsync(int scheduleId, int page, int pageSize)
    {
        var query = _db.InvoiceRuns
            .Include(r => r.TriggeredBy)
            .Where(r => r.ScheduleId == scheduleId);

        return Task.FromResult(Page<InvoiceRun>.GeneratePaging(query,
            new PageQuery { PageNumber = page, PageSize = pageSize, SortProperty = "TriggeredAt", SortDirection = ListSortDirection.Descending }));
    }
}
