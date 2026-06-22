using PageSort;
using PCA.Modules.Invoicing.Models;

namespace PCA.Modules.Invoicing.Services;

public interface IInvoicingService
{
    // Lenders
    Task<List<InvoiceLender>> GetLendersAsync();
    Task<InvoiceLender?> GetLenderByIdAsync(int id);
    Task<InvoiceLender> CreateLenderAsync(InvoiceLender lender);
    Task UpdateLenderAsync(InvoiceLender lender);
    Task DeleteLenderAsync(int id);

    // Recipients
    Task<List<InvoiceRecipient>> GetRecipientsAsync();
    Task<InvoiceRecipient?> GetRecipientByIdAsync(int id);
    Task<InvoiceRecipient> CreateRecipientAsync(InvoiceRecipient recipient);
    Task UpdateRecipientAsync(InvoiceRecipient recipient);
    Task DeleteRecipientAsync(int id);

    // Schedules
    Task<List<InvoiceSchedule>> GetSchedulesAsync();
    Task<InvoiceSchedule?> GetScheduleByIdAsync(int id);
    Task<InvoiceSchedule> CreateScheduleAsync(InvoiceSchedule schedule, List<int> recipientIds);
    Task UpdateScheduleAsync(InvoiceSchedule schedule, List<int> recipientIds);
    Task DeleteScheduleAsync(int id);
    Task<List<InvoiceSchedule>> GetDueSchedulesAsync();
    Task UpdateScheduleNextRunAsync(int scheduleId, DateTime? lastRunAt, DateTime? nextRunAt);

    // HCM Ref Files
    Task<List<InvoiceHcmRefFile>> GetHcmRefFilesAsync(int scheduleId);
    Task<InvoiceHcmRefFile?> GetHcmRefFileForMonthAsync(int scheduleId, string monthYear);
    Task<List<InvoiceHcmRefFile>> GetHcmRefFilesForMonthAsync(string monthYear, int excludeScheduleId);
    Task<InvoiceHcmRefFile> SaveHcmRefFileAsync(InvoiceHcmRefFile refFile);
    Task DeleteHcmRefFileAsync(int id);

    // Runs
    Task<List<InvoiceRun>> GetRunsForScheduleAsync(int scheduleId);
    Task<InvoiceRun?> GetRunByIdAsync(int id);
    Task<InvoiceRun> CreateRunAsync(InvoiceRun run);
    Task UpdateRunAsync(InvoiceRun run);
    Task<PagedResult<InvoiceRun>> GetRunsPagedAsync(int scheduleId, int page, int pageSize);
}
