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
        _logger.LogInformation(
            "InvoiceRun: starting for schedule {ScheduleId} ({ScheduleName}), triggered by {TriggeredBy}",
            schedule.Id, schedule.Name, triggeredById ?? "scheduler");

        var run = await _svc.CreateRunAsync(new InvoiceRun
        {
            ScheduleId    = schedule.Id,
            TriggeredAt   = DateTime.UtcNow,
            TriggeredById = triggeredById,
            Status        = InvoiceRunStatus.Running
        });

        _logger.LogInformation("InvoiceRun: run record created with Id {RunId}", run.Id);

        try
        {
            var lender = schedule.Lender!;
            _logger.LogInformation("InvoiceRun {RunId}: lender={LenderName}, deductionCode={DeductionCode}, splitSheets={SplitSheets}",
                run.Id, lender.Name, lender.DeductionCode, schedule.SplitSheets);

            if (string.IsNullOrWhiteSpace(lender.DeductionCode))
                throw new InvalidOperationException(
                    $"Lender '{lender.Name}' has no deduction code. Re-save the lender to fetch it from IPPS.");

            var ippsRows = await _dataSvc.FetchDeductionsFromSourceAsync(_dataSvc.IppsSettings, lender.DeductionCode, "IPPS", ct);
            var hcmRows  = await _dataSvc.FetchDeductionsFromSourceAsync(_dataSvc.HcmSettings,  lender.DeductionCode, "HCM",  ct);

            _logger.LogInformation("InvoiceRun {RunId}: raw fetch — IPPS={IppsCount}, HCM={HcmCount}",
                run.Id, ippsRows.Count, hcmRows.Count);

            string filePath;
            int finalCount;
            List<DeductionRow>? hcmSheet = null;

            if (schedule.SplitSheets)
            {
                var monthYear = DateTime.UtcNow.ToString("yyyy-MM");
                _logger.LogInformation("InvoiceRun {RunId}: split sheets enabled, looking for ref file for {MonthYear}", run.Id, monthYear);

                var refFile = await _svc.GetHcmRefFileForMonthAsync(schedule.Id, monthYear);
                if (refFile == null)
                    throw new InvalidOperationException(
                        $"Split sheets is enabled but no HCM ref file has been uploaded for {monthYear}. " +
                        "Upload the ref file from the schedule details page before running.");

                _logger.LogInformation("InvoiceRun {RunId}: using ref file {RefFile}", run.Id, refFile.OriginalFileName);

                var (ippsSheet, hcmSheetRows) = _dataSvc.SplitRows(ippsRows, hcmRows, refFile.FilePath);
                hcmSheet   = hcmSheetRows;
                filePath   = InvoiceDataService.BuildExcel(ippsSheet, lender.Name, _storageRoot, hcmSheet);
                finalCount = ippsSheet.Count + hcmSheetRows.Count;

                _logger.LogInformation("InvoiceRun {RunId}: split complete — IPPS sheet={IppsSheet}, HCM sheet={HcmSheet}",
                    run.Id, ippsSheet.Count, hcmSheetRows.Count);
            }
            else
            {
                var merged = ippsRows.Concat(hcmRows)
                    .GroupBy(r => r.EmployeeNumber)
                    .Select(g => g.OrderByDescending(r => r.InstallmentAmount).First())
                    .OrderBy(r => r.EmployeeNumber)
                    .ToList();
                filePath   = InvoiceDataService.BuildExcel(merged, lender.Name, _storageRoot);
                finalCount = merged.Count;

                _logger.LogInformation("InvoiceRun {RunId}: merged — {FinalCount} rows after dedup", run.Id, finalCount);
            }

            var fileName = Path.GetFileName(filePath);
            _logger.LogInformation("InvoiceRun {RunId}: Excel written to {FilePath}", run.Id, filePath);

            var recipients = schedule.ScheduleRecipients
                .Select(sr => (sr.Recipient!.Email, sr.Recipient.Name))
                .ToList();

            if (recipients.Count > 0)
            {
                _logger.LogInformation("InvoiceRun {RunId}: sending email to {RecipientCount} recipient(s)", run.Id, recipients.Count);

                var now     = DateTime.UtcNow;
                var subject = $"Invoice Breakdown — {lender.Name} — {now:MMMM yyyy}";
                var body    = $"Please find attached the invoice breakdown for {lender.Name} ({now:MMMM yyyy}).\n\n" +
                              $"Total records: {finalCount}.\n\nThis is an automated message.";

                await _email.SendInvoiceAsync(recipients, subject, body, filePath, fileName, ct);
                _logger.LogInformation("InvoiceRun {RunId}: email sent", run.Id);
            }
            else
            {
                _logger.LogWarning("InvoiceRun {RunId}: no recipients configured — email skipped", run.Id);
            }

            run.Status        = InvoiceRunStatus.Completed;
            run.FilePath      = filePath;
            run.FileName      = fileName;
            run.IppsRowCount  = ippsRows.Count;
            run.HcmRowCount   = hcmRows.Count;
            run.FinalRowCount = finalCount;
            run.CompletedAt   = DateTime.UtcNow;

            _logger.LogInformation("InvoiceRun {RunId}: completed successfully in {Duration:N1}s",
                run.Id, (run.CompletedAt.Value - run.TriggeredAt).TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InvoiceRun {RunId}: failed for schedule {ScheduleId} — {Message}",
                run.Id, schedule.Id, ex.Message);
            run.Status       = InvoiceRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.CompletedAt  = DateTime.UtcNow;
        }
        finally
        {
            await _svc.UpdateRunAsync(run);
            _logger.LogInformation("InvoiceRun {RunId}: run record saved with status {Status}", run.Id, run.Status);
        }
    }
}
