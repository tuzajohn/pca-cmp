using PCA.Modules.Documents.Services;

namespace PCA.Web.Services;

public class DocumentReviewAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentReviewAlertWorker> _logger;

    // Alert flags (bitmask): 1=7-day, 2=3-day, 4=1-day, 8=overdue
    private static readonly (int DaysBefore, int Flag, string Label)[] AlertSchedule =
    {
        (7, 1, "7 days"),
        (3, 2, "3 days"),
        (1, 4, "1 day"),
        (0, 8, "today — overdue")
    };

    public DocumentReviewAlertWorker(IServiceScopeFactory scopeFactory,
        ILogger<DocumentReviewAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Run once per day; sleep until 01:00 UTC
            var now = DateTime.UtcNow;
            var next = now.Date.AddDays(1).AddHours(1);
            var delay = next - now;
            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }

            if (stoppingToken.IsCancellationRequested) break;

            await RunAlertsAsync(stoppingToken);
        }
    }

    private async Task RunAlertsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var docService   = scope.ServiceProvider.GetRequiredService<IDocumentService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var logService   = scope.ServiceProvider.GetRequiredService<ILogService>();

        foreach (var (daysBefore, flag, label) in AlertSchedule)
        {
            try
            {
                var docs = daysBefore == 0
                    ? await GetOverdueDocumentsAsync(docService, flag)
                    : await docService.GetDocumentsDueForReviewAlertAsync(daysBefore, flag);

                foreach (var doc in docs)
                {
                    if (ct.IsCancellationRequested) return;

                    var ownerEmail = doc.Owner?.Email;
                    if (string.IsNullOrEmpty(ownerEmail)) continue;

                    var subject = daysBefore == 0
                        ? $"⚠️ Document Review Overdue: {doc.Title}"
                        : $"📋 Document Review Due in {label}: {doc.Title}";

                    var dueText = daysBefore == 0
                        ? $"was due on <strong>{doc.NextReviewDate!.Value:dd MMM yyyy}</strong> and is now overdue"
                        : $"is due for review in <strong>{label}</strong> on <strong>{doc.NextReviewDate!.Value:dd MMM yyyy}</strong>";

                    var body = $"""
                        <div style="font-family:Inter,sans-serif;max-width:560px;margin:0 auto;color:#111827;">
                          <div style="background:{(daysBefore == 0 ? "#dc2626" : "#d97706")};padding:24px 32px;border-radius:12px 12px 0 0;">
                            <h1 style="color:#fff;margin:0;font-size:20px;font-weight:700;">PCA Management Portal</h1>
                          </div>
                          <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                            <h2 style="font-size:17px;margin:0 0 8px;">Document Review Reminder</h2>
                            <p style="color:#6b7280;margin:0 0 16px;">
                              The document <strong>{doc.Title}</strong> ({doc.SerialNumber}) {dueText}.
                            </p>
                            <p style="color:#6b7280;margin:0 0 24px;">
                              Please review the document and mark it as reviewed in the portal.
                            </p>
                          </div>
                        </div>
                        """;

                    try
                    {
                        await emailService.SendRawAsync(ownerEmail, doc.Owner!.FullName, subject, body);
                        await docService.SetReviewAlertFlagAsync(doc.Id, flag);
                        await logService.InfoAsync($"Review alert ({label}) sent for document {doc.SerialNumber}",
                            "Document.ReviewAlert", "Document", doc.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send review alert for document {Id}", doc.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing review alerts for flag {Flag}", flag);
            }
        }
    }

    private async Task<List<PCA.Modules.Documents.Models.Document>> GetOverdueDocumentsAsync(
        IDocumentService docService, int flag)
    {
        // Overdue = NextReviewDate < today and overdue flag not yet sent
        var cutoff = DateTime.UtcNow.Date;
        var all = await docService.GetDocumentsDueForReviewAlertAsync(0, flag);
        // GetDocumentsDueForReviewAlert checks exact date match; for overdue we need a different query
        // Use the service's overdue variant via flag 8 on days=0 (see DocumentService implementation)
        return all;
    }
}
