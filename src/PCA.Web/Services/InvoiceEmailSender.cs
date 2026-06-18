using PCA.Modules.Invoicing.Services;

namespace PCA.Web.Services;

public class InvoiceEmailSender : IInvoiceEmailSender
{
    private readonly IEmailService _email;

    public InvoiceEmailSender(IEmailService email) => _email = email;

    public Task SendInvoiceAsync(
        IEnumerable<(string Email, string Name)> recipients,
        string subject, string textBody,
        string filePath, string fileName,
        CancellationToken ct = default)
    {
        const string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return _email.SendWithAttachmentAsync(recipients, subject, textBody, filePath, fileName, mimeType);
    }
}
