namespace PCA.Web.Services;

public interface IEmailService
{
    Task SendInviteAsync(string toEmail, string toName, string activationLink);
    Task SendPasswordResetAsync(string toEmail, string toName, string resetLink);
    Task SendRawAsync(string toEmail, string toName, string subject, string htmlBody);

    // Approval notifications
    Task SendApprovalRequestAsync(string toEmail, string toName, string entityLabel, string roleName, string viewLink);
    Task SendApprovalReminderAsync(string toEmail, string toName, string entityLabel, string roleName, string viewLink);
    Task SendApprovalCompletedAsync(string toEmail, string toName, string entityLabel);
    Task SendApprovalRejectedAsync(string toEmail, string toName, string entityLabel, string rejectorName, string comment);
    Task SendApprovalReturnedAsync(string toEmail, string toName, string entityLabel, string returnerName, string comment);
}
