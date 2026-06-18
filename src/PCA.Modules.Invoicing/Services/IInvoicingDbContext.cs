using Microsoft.EntityFrameworkCore;
using PCA.Modules.Invoicing.Models;

namespace PCA.Modules.Invoicing.Services;

public interface IApplicationDbContextForInvoicing
{
    DbSet<InvoiceLender> InvoiceLenders { get; }
    DbSet<InvoiceRecipient> InvoiceRecipients { get; }
    DbSet<InvoiceSchedule> InvoiceSchedules { get; }
    DbSet<InvoiceScheduleRecipient> InvoiceScheduleRecipients { get; }
    DbSet<InvoiceRun> InvoiceRuns { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
