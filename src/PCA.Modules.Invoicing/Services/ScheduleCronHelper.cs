using Cronos;
using PCA.Modules.Invoicing.Models;

namespace PCA.Modules.Invoicing.Services;

public static class ScheduleCronHelper
{
    /// <summary>Builds a cron expression from the user-friendly schedule fields.</summary>
    public static string ToCron(InvoiceSchedule s)
    {
        var hour = s.TimeOfDay.Hour;
        var min  = s.TimeOfDay.Minute;

        var dom = s.DayOfMonth == -1 ? "L" : (s.DayOfMonth ?? 1).ToString();

        return s.Frequency switch
        {
            InvoiceFrequency.Daily   => $"{min} {hour} * * *",
            InvoiceFrequency.Weekly  => $"{min} {hour} * * {s.DayOfWeek ?? 1}",
            InvoiceFrequency.Monthly => $"{min} {hour} {dom} * *",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public static DateTime? NextOccurrence(InvoiceSchedule s)
    {
        var expr = CronExpression.Parse(ToCron(s));
        return expr.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
    }

    public static string Describe(InvoiceSchedule s) => s.Frequency switch
    {
        InvoiceFrequency.Daily   => $"Every day at {s.TimeOfDay:HH\\:mm}",
        InvoiceFrequency.Weekly  => $"Every {(DayOfWeek)(s.DayOfWeek ?? 1)} at {s.TimeOfDay:HH\\:mm}",
        InvoiceFrequency.Monthly => s.DayOfMonth == -1
            ? $"Monthly on last day of month at {s.TimeOfDay:HH\\:mm}"
            : $"Monthly on day {s.DayOfMonth ?? 1} at {s.TimeOfDay:HH\\:mm}",
        _ => "Unknown"
    };
}
