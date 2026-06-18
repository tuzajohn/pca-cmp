using PCA.Modules.Identity.Models;

namespace PCA.Modules.Invoicing.Models;

public enum InvoiceFrequency { Daily, Weekly, Monthly }

public class InvoiceSchedule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public int LenderId { get; set; }
    public InvoiceLender? Lender { get; set; }

    public InvoiceFrequency Frequency { get; set; }
    public int? DayOfWeek { get; set; }   // 0=Sunday … 6=Saturday (Weekly)
    public int? DayOfMonth { get; set; }  // 1–31 (Monthly)
    public TimeOnly TimeOfDay { get; set; }

    public bool IsEnabled { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public ICollection<InvoiceScheduleRecipient> ScheduleRecipients { get; set; } = new List<InvoiceScheduleRecipient>();
    public ICollection<InvoiceRun> Runs { get; set; } = new List<InvoiceRun>();
}
