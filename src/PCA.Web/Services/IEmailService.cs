namespace PCA.Web.Services;

public interface IEmailService
{
    Task SendInviteAsync(string toEmail, string toName, string activationLink);
    Task SendPasswordResetAsync(string toEmail, string toName, string resetLink);
    Task SendRawAsync(string toEmail, string toName, string subject, string htmlBody);
}
