using PCA.Modules.Identity.Models;

namespace PCA.Modules.Invoicing.Models;

public class InvoiceHcmRefFile
{
    public int Id { get; set; }

    public int ScheduleId { get; set; }
    public InvoiceSchedule? Schedule { get; set; }

    public string MonthYear { get; set; } = string.Empty; // "YYYY-MM"
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? UploadedById { get; set; }
    public ApplicationUser? UploadedBy { get; set; }
}
