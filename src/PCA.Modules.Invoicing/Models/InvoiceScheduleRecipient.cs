namespace PCA.Modules.Invoicing.Models;

public class InvoiceScheduleRecipient
{
    public int InvoiceScheduleId { get; set; }
    public InvoiceSchedule? Schedule { get; set; }

    public int InvoiceRecipientId { get; set; }
    public InvoiceRecipient? Recipient { get; set; }
}
