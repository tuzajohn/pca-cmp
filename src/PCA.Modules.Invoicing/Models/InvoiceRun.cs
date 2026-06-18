using PCA.Modules.Identity.Models;

namespace PCA.Modules.Invoicing.Models;

public enum InvoiceRunStatus { Pending, Running, Completed, Failed }

public class InvoiceRun
{
    public int Id { get; set; }

    public int ScheduleId { get; set; }
    public InvoiceSchedule? Schedule { get; set; }

    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public string? TriggeredById { get; set; }      // null = system/scheduler
    public ApplicationUser? TriggeredBy { get; set; }

    public InvoiceRunStatus Status { get; set; } = InvoiceRunStatus.Pending;
    public string? ErrorMessage { get; set; }

    public string? FilePath { get; set; }
    public string? FileName { get; set; }

    public int IppsRowCount { get; set; }
    public int HcmRowCount { get; set; }
    public int FinalRowCount { get; set; }

    public DateTime? CompletedAt { get; set; }
}
