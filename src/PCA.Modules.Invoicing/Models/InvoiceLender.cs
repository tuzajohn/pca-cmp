using PCA.Modules.Identity.Models;

namespace PCA.Modules.Invoicing.Models;

public class InvoiceLender
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CompanyType { get; set; } = string.Empty; // MFI | BANK | SACCO
    public string DeductionCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public ICollection<InvoiceSchedule> Schedules { get; set; } = new List<InvoiceSchedule>();
}
