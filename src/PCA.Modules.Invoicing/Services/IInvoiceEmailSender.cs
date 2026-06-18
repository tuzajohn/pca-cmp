namespace PCA.Modules.Invoicing.Services;

public interface IInvoiceEmailSender
{
    Task SendInvoiceAsync(
        IEnumerable<(string Email, string Name)> recipients,
        string subject,
        string textBody,
        string filePath,
        string fileName,
        CancellationToken ct = default);
}
