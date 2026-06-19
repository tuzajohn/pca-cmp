using PCA.Modules.Invoicing.Services;

namespace PCA.Web.Services;

public class InvoiceSchedulerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceSchedulerWorker> _logger;

    public InvoiceSchedulerWorker(IServiceScopeFactory scopeFactory, ILogger<InvoiceSchedulerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { break; }

            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await RunDueSchedulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invoice scheduler tick failed");
            }
        }
    }

    private async Task RunDueSchedulesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var svc         = scope.ServiceProvider.GetRequiredService<IInvoicingService>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<InvoiceRunOrchestrator>();

        var due = await svc.GetDueSchedulesAsync();
        foreach (var schedule in due)
        {
            _logger.LogInformation("Running scheduled invoice: {Name} (id={Id})", schedule.Name, schedule.Id);

            var nextRun = ScheduleCronHelper.NextOccurrence(schedule);
            await svc.UpdateScheduleNextRunAsync(schedule.Id, DateTime.UtcNow, nextRun);

            await orchestrator.ExecuteAsync(schedule, triggeredById: null, ct);
        }
    }
}
