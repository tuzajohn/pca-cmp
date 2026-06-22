namespace PCA.Modules.Invoicing.Services;

public interface IInvoiceDocumentRegistrar
{
    Task RegisterAsync(
        string filePath,
        string lenderName,
        string monthYear,
        int scheduleId,
        string scheduleName,
        string? triggeredById);
}
