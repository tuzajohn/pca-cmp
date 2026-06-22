using PCA.Modules.AccessManagement.Services;

namespace PCA.Web.Services;

public class DeprovisioningAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeprovisioningAlertWorker> _logger;

    public DeprovisioningAlertWorker(IServiceScopeFactory scopeFactory,
        ILogger<DeprovisioningAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); }
            catch (OperationCanceledException) { break; }

            if (stoppingToken.IsCancellationRequested) break;

            await RunAlertsAsync(stoppingToken);
        }
    }

    private async Task RunAlertsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var svc         = scope.ServiceProvider.GetRequiredService<IAccessManagementService>();
        var emailSvc    = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var logSvc      = scope.ServiceProvider.GetRequiredService<ILogService>();
        var db          = scope.ServiceProvider.GetRequiredService<PCA.Web.Data.ApplicationDbContext>();

        try
        {
            // Mark overdue events
            var overdue = await svc.GetOverdueDeprovisioningEventsAsync();
            foreach (var evt in overdue)
            {
                if (ct.IsCancellationRequested) return;
                evt.Status = PCA.Shared.Enums.DeprovisioningStatus.Overdue;
                evt.UpdatedAt = DateTime.UtcNow;
            }
            if (overdue.Any()) await db.SaveChangesAsync(ct);

            // SLA warning emails (< 4h remaining, not yet sent)
            var warnings = await svc.GetSlaWarningPendingAsync();
            foreach (var evt in warnings)
            {
                if (ct.IsCancellationRequested) return;

                var remaining = evt.SlaDeadline - DateTime.UtcNow;
                var subject = $"[PCA] URGENT: Deprovisioning SLA approaching — {evt.EmployeeName}";
                var body = $"""
                    <div style="font-family:Inter,sans-serif;max-width:560px;margin:0 auto;color:#111827;">
                      <div style="background:#dc2626;padding:24px 32px;border-radius:12px 12px 0 0;">
                        <h1 style="color:#fff;margin:0;font-size:20px;font-weight:700;">PCA Management Portal</h1>
                      </div>
                      <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                        <h2 style="font-size:17px;margin:0 0 8px;">Deprovisioning SLA Warning</h2>
                        <p style="color:#6b7280;margin:0 0 16px;">
                          The deprovisioning event for <strong>{evt.EmployeeName}</strong> ({evt.SerialNumber})
                          has less than <strong>{(int)remaining.TotalHours}h {remaining.Minutes}m</strong> remaining on its 24-hour SLA.
                        </p>
                        <p style="color:#6b7280;margin:0 0 8px;">
                          <strong>SLA Deadline:</strong> {evt.SlaDeadline:dd MMM yyyy HH:mm} UTC
                        </p>
                        <p style="color:#6b7280;margin:0;">
                          Please complete all system deactivations immediately in the PCA portal.
                        </p>
                      </div>
                    </div>
                    """;

                try
                {
                    // Send to all admins — fetch from Identity
                    var userManager = scope.ServiceProvider
                        .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PCA.Modules.Identity.Models.ApplicationUser>>();
                    var admins = await userManager.GetUsersInRoleAsync("Admin");
                    foreach (var admin in admins)
                    {
                        if (!string.IsNullOrEmpty(admin.Email))
                            await emailSvc.SendRawAsync(admin.Email, admin.FullName, subject, body);
                    }

                    evt.SlaWarningEmailSentAt = DateTime.UtcNow;
                    evt.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);

                    await logSvc.InfoAsync(
                        $"Deprovisioning SLA warning sent for {evt.SerialNumber}",
                        "Deprovisioning.SlaWarning", "DeprovisioningEvent", evt.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send SLA warning for deprovisioning event {Id}", evt.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeprovisioningAlertWorker");
        }
    }
}
