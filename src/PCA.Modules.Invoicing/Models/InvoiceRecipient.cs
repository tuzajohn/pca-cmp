using PCA.Modules.Identity.Models;

namespace PCA.Modules.Invoicing.Models;

public class InvoiceRecipient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public ICollection<InvoiceScheduleRecipient> ScheduleRecipients { get; set; } = new List<InvoiceScheduleRecipient>();
}
