using Microsoft.Extensions.Logging;
using PCA.Web.Data;
using PCA.Web.Models;

namespace PCA.Web.Services;

public sealed class DbLoggerProvider : ILoggerProvider
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DbLoggerProvider(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public ILogger CreateLogger(string categoryName) => new DbLogger(categoryName, _scopeFactory);

    public void Dispose() { }
}

internal sealed class DbLogger : ILogger
{
    private readonly string _categoryName;
    private readonly IServiceScopeFactory _scopeFactory;

    // Only log Warning+ from Microsoft/System namespaces; everything Error+ otherwise
    private static readonly HashSet<string> NoisyPrefixes = new()
    {
        "Microsoft.", "System.", "Pomelo.", "MailKit."
    };

    public DbLogger(string categoryName, IServiceScopeFactory scopeFactory)
    {
        _categoryName = categoryName;
        _scopeFactory = scopeFactory;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel < LogLevel.Warning) return false;
        if (NoisyPrefixes.Any(p => _categoryName.StartsWith(p, StringComparison.Ordinal)))
            return logLevel >= LogLevel.Error;
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var details = exception?.ToString();
        var level   = logLevel switch
        {
            LogLevel.Warning  => "Warning",
            LogLevel.Error    => "Error",
            LogLevel.Critical => "Critical",
            _                 => "Info"
        };

        // Fire-and-forget — logging must never throw or block
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.AppLogs.Add(new AppLog
                {
                    Timestamp  = DateTime.UtcNow,
                    Level      = level,
                    Category   = "App",
                    Source     = "PCA Portal",
                    Message    = message.Length > 2000 ? message[..2000] : message,
                    Details    = details,
                    Action     = _categoryName,
                    CreatedAt  = DateTime.UtcNow,
                    UpdatedAt  = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch { /* never throw from a logger */ }
        });
    }
}
