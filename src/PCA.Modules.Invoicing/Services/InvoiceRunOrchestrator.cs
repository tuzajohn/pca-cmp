using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit.Net.Smtp;
using PCA.Modules.Invoicing.Models;

namespace PCA.Modules.Invoicing.Services;

public class InvoiceRunOrchestrator
{
    private readonly IInvoicingService _svc;
    private readonly InvoiceDataService _dataSvc;
    private readonly string _storageRoot;
    private readonly SmtpConfig _smtp;
    private readonly ILogger _logger;

    public InvoiceRunOrchestrator(
        IInvoicingService svc,
        InvoiceDataService dataSvc,
        string storageRoot,
        SmtpConfig smtp,
        ILogger logger)
    {
        _svc = svc;
        _dataSvc = dataSvc;
        _storageRoot = storageRoot;
        _smtp = smtp;
        _logger = logger;
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

            // 1. Lookup deduction code from IPPS companies table
            var deductionCode = await InvoiceDataService.LookupDeductionCodeAsync(
                _dataSvc.IppsSettings, lender.CompanyType, ct);

            // 2. Fetch + merge
            var (rows, ippsCount, hcmCount) = await _dataSvc.FetchMergedDataAsync(deductionCode, ct);

            // 3. Build Excel
            var filePath = InvoiceDataService.BuildExcel(rows, lender.Name, _storageRoot);
            var fileName = Path.GetFileName(filePath);

            // 4. Email
            var recipients = schedule.ScheduleRecipients.Select(sr => sr.Recipient!).ToList();
            if (recipients.Count > 0)
                await SendEmailAsync(fileName, filePath, lender.Name, rows.Count, recipients, ct);

            // 5. Update run
            run.Status       = InvoiceRunStatus.Completed;
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

    private async Task SendEmailAsync(
        string fileName, string filePath, string lenderName,
        int rowCount, List<InvoiceRecipient> recipients, CancellationToken ct)
    {
        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(_smtp.From));
        foreach (var r in recipients)
            msg.To.Add(new MailboxAddress(r.Name, r.Email));

        var now = DateTime.UtcNow;
        msg.Subject = $"Invoice Breakdown — {lenderName} — {now:MMMM yyyy}";

        var builder = new BodyBuilder
        {
            TextBody = $"Please find attached the invoice breakdown for {lenderName} ({now:MMMM yyyy}).\n\nTotal records: {rowCount}.\n\nThis is an automated message."
        };
        builder.Attachments.Add(fileName, await File.ReadAllBytesAsync(filePath, ct),
            new MimeKit.ContentType("application", "vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        msg.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtp.Host, _smtp.Port, _smtp.UseSsl, ct);
        if (!string.IsNullOrEmpty(_smtp.Username))
            await client.AuthenticateAsync(_smtp.Username, _smtp.Password, ct);
        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);
    }
}

public class SmtpConfig
{
    public string Host     { get; set; } = string.Empty;
    public int    Port     { get; set; } = 587;
    public bool   UseSsl   { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From     { get; set; } = string.Empty;
}
