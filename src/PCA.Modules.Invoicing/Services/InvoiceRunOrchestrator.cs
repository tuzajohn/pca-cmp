using Microsoft.Extensions.Logging;
using PCA.Modules.Invoicing.Models;

namespace PCA.Modules.Invoicing.Services;

public class InvoiceRunOrchestrator
{
    private readonly IInvoicingService _svc;
    private readonly InvoiceDataService _dataSvc;
    private readonly IInvoiceEmailSender _email;
    private readonly string _storageRoot;
    private readonly ILogger _logger;

    public InvoiceRunOrchestrator(
        IInvoicingService svc,
        InvoiceDataService dataSvc,
        IInvoiceEmailSender email,
        string storageRoot,
        ILogger logger)
    {
        _svc         = svc;
        _dataSvc     = dataSvc;
        _email       = email;
        _storageRoot = storageRoot;
        _logger      = logger;
    }

    public async Task ExecuteAsync(InvoiceSchedule schedule, string? triggeredById, CancellationToken ct)
    {
        var run = await _svc.CreateRunAsync(new InvoiceRun
        {
            ScheduleId    = schedule.Id,
            TriggeredAt   = DateTime.UtcNow,
            TriggeredById = triggeredById,
            Status        = InvoiceRunStatus.Running
        });

        try
        {
            var lender = schedule.Lender!;

            if (string.IsNullOrWhiteSpace(lender.DeductionCode))
                throw new InvalidOperationException(
                    $"Lender '{lender.Name}' has no deduction code. Re-save the lender to fetch it from IPPS.");

            var (rows, ippsCount, hcmCount) = await _dataSvc.FetchMergedDataAsync(lender.DeductionCode, ct);

            var filePath = InvoiceDataService.BuildExcel(rows, lender.Name, _storageRoot);
            var fileName = Path.GetFileName(filePath);

            var recipients = schedule.ScheduleRecipients
                .Select(sr => (sr.Recipient!.Email, sr.Recipient.Name))
                .ToList();

            if (recipients.Count > 0)
            {
                var now     = DateTime.UtcNow;
                var subject = $"Invoice Breakdown — {lender.Name} — {now:MMMM yyyy}";
                var body    = $"Please find attached the invoice breakdown for {lender.Name} ({now:MMMM yyyy}).\n\n" +
                              $"Total records: {rows.Count}.\n\nThis is an automated message.";

                await _email.SendInvoiceAsync(recipients, subject, body, filePath, fileName, ct);
            }

            run.Status        = InvoiceRunStatus.Completed;
            run.FilePath      = filePath;
            run.FileName      = fileName;
            run.IppsRowCount  = ippsCount;
            run.HcmRowCount   = hcmCount;
            run.FinalRowCount = rows.Count;
            run.CompletedAt   = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice run {RunId} failed for schedule {ScheduleId}", run.Id, schedule.Id);
            run.Status       = InvoiceRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.CompletedAt  = DateTime.UtcNow;
        }
        finally
        {
            await _svc.UpdateRunAsync(run);
        }
    }
}
