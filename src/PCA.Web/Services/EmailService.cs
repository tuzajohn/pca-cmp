using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PCA.Web.Services;

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}

public class EmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(SmtpSettings settings, ILogger<EmailService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendInviteAsync(string toEmail, string toName, string activationLink)
    {
        var subject = "You've been invited to PCA Management Portal";
        var body = $"""
            <div style="font-family:Inter,sans-serif;max-width:560px;margin:0 auto;color:#111827;">
              <div style="background:#e8533a;padding:28px 32px;border-radius:12px 12px 0 0;">
                <h1 style="color:#fff;margin:0;font-size:22px;font-weight:700;">PCA Management Portal</h1>
              </div>
              <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                <h2 style="font-size:18px;font-weight:700;margin:0 0 8px;">Welcome, {toName}</h2>
                <p style="color:#6b7280;margin:0 0 24px;">Your account has been created. Click the button below to set your password and activate your account.</p>
                <a href="{activationLink}"
                   style="display:inline-block;background:#e8533a;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:600;font-size:14px;">
                  Activate Account
                </a>
                <p style="color:#9ca3af;font-size:12px;margin:24px 0 0;">
                  This link is single-use and will expire once used. If you did not expect this email, you can safely ignore it.
                </p>
                <hr style="border:none;border-top:1px solid #e5e7eb;margin:24px 0;" />
                <p style="color:#9ca3af;font-size:11px;margin:0;">
                  If the button above doesn't work, copy and paste this link into your browser:<br/>
                  <a href="{activationLink}" style="color:#e8533a;word-break:break-all;">{activationLink}</a>
                </p>
              </div>
            </div>
            """;

        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetLink)
    {
        var subject = "Reset your PCA Management Portal password";
        var body = $"""
            <div style="font-family:Inter,sans-serif;max-width:560px;margin:0 auto;color:#111827;">
              <div style="background:#e8533a;padding:28px 32px;border-radius:12px 12px 0 0;">
                <h1 style="color:#fff;margin:0;font-size:22px;font-weight:700;">PCA Management Portal</h1>
              </div>
              <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                <h2 style="font-size:18px;font-weight:700;margin:0 0 8px;">Password Reset</h2>
                <p style="color:#6b7280;margin:0 0 24px;">Hi {toName}, an administrator has generated a password reset link for your account. Click the button below to choose a new password.</p>
                <a href="{resetLink}"
                   style="display:inline-block;background:#e8533a;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:600;font-size:14px;">
                  Reset Password
                </a>
                <p style="color:#9ca3af;font-size:12px;margin:24px 0 0;">
                  This link is single-use. If you did not request a password reset, please contact your administrator.
                </p>
                <hr style="border:none;border-top:1px solid #e5e7eb;margin:24px 0;" />
                <p style="color:#9ca3af;font-size:11px;margin:0;">
                  If the button above doesn't work, copy and paste this link:<br/>
                  <a href="{resetLink}" style="color:#e8533a;word-break:break-all;">{resetLink}</a>
                </p>
              </div>
            </div>
            """;

        await SendAsync(toEmail, toName, subject, body);
    }

    public Task SendRawAsync(string toEmail, string toName, string subject, string htmlBody)
        => SendAsync(toEmail, toName, subject, htmlBody);

    public async Task SendApprovalRequestAsync(string toEmail, string toName, string entityLabel, string roleName, string viewLink)
    {
        var subject = $"📋 Approval Required: {entityLabel}";
        var body = $"""
            <div style="font-family:Inter,sans-serif;max-width:560px;margin:0 auto;color:#111827;">
              <div style="background:#e8533a;padding:28px 32px;border-radius:12px 12px 0 0;">
                <h1 style="color:#fff;margin:0;font-size:22px;font-weight:700;">PCA Management Portal</h1>
              </div>
              <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                <h2 style="font-size:18px;font-weight:700;margin:0 0 8px;">Approval Required</h2>
                <p style="color:#6b7280;margin:0 0 16px;">Hi {toName}, your approval is required for the following item:</p>
                <div style="background:#f9fafb;border-left:4px solid #e8533a;padding:16px;margin:0 0 24px;border-radius:4px;">
                  <p style="margin:0;font-weight:600;color:#111827;">{entityLabel}</p>
                  <p style="margin:4px 0 0;font-size:14px;color:#6b7280;">Role: {roleName}</p>
                </div>
                <p style="color:#6b7280;margin:0 0 24px;">Please review and approve or reject this request.</p>
                <a href="{viewLink}"
                   style="display:inline-block;background:#e8533a;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:600;font-size:14px;">
                  Review & Approve
                </a>
                <hr style="border:none;border-top:1px solid #e5e7eb;margin:24px 0;" />
                <p style="color:#9ca3af;font-size:11px;margin:0;">
                  If the button above doesn't work, copy and paste this link:<br/>
                  <a href="{viewLink}" style="color:#e8533a;word-break:break-all;">{viewLink}</a>
                </p>
              </div>
            </div>
            """;

        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendApprovalReminderAsync(string toEmail, string toName, string entityLabel, string roleName, string viewLink)
    {
        var subject = $"🔔 Reminder: Approval Required for {entityLabel}";
        var body = $"""
            <div style="font-family:Inter,sans-serif;max-width:560px;margin:0 auto;color:#111827;">
              <div style="background:#f59e0b;padding:28px 32px;border-radius:12px 12px 0 0;">
                <h1 style="color:#fff;margin:0;font-size:22px;font-weight:700;">PCA Management Portal</h1>
              </div>
              <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                <h2 style="font-size:18px;font-weight:700;margin:0 0 8px;">⏰ Approval Reminder</h2>
                <p style="color:#6b7280;margin:0 0 16px;">Hi {toName}, this is a friendly reminder that your approval is still pending for:</p>
                <div style="background:#fffbeb;border-left:4px solid #f59e0b;padding:16px;margin:0 0 24px;border-radius:4px;">
                  <p style="margin:0;font-weight:600;color:#111827;">{entityLabel}</p>
                  <p style="margin:4px 0 0;font-size:14px;color:#d97706;">Role: {roleName}</p>
                </div>
                <p style="color:#6b7280;margin:0 0 24px;">Please review this item at your earliest convenience to keep the workflow moving.</p>
                <a href="{viewLink}"
                   style="display:inline-block;background:#f59e0b;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:600;font-size:14px;">
                  Review Now
                </a>
                <hr style="border:none;border-top:1px solid #e5e7eb;margin:24px 0;" />
                <p style="color:#9ca3af;font-size:11px;margin:0;">
                  If the button above doesn't work, copy and paste this link:<br/>
                  <a href="{viewLink}" style="color:#f59e0b;word-break:break-all;">{viewLink}</a>
                </p>
              </div>
            </div>
            """;

        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendApprovalCompletedAsync(string toEmail, string toName, string entityLabel)
    {
        var subject = $"✅ Approved: {entityLabel}";
        var body = $"""
            <div style="font-family:Inter,sans-serif;max-width:560px;margin:0 auto;color:#111827;">
              <div style="background:#10b981;padding:28px 32px;border-radius:12px 12px 0 0;">
                <h1 style="color:#fff;margin:0;font-size:22px;font-weight:700;">PCA Management Portal</h1>
              </div>
              <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                <h2 style="font-size:18px;font-weight:700;margin:0 0 8px;">All Approvals Completed</h2>
                <p style="color:#6b7280;margin:0 0 16px;">Hi {toName}, the following item has received all required approvals:</p>
                <div style="background:#f0fdf4;border-left:4px solid #10b981;padding:16px;margin:0 0 24px;border-radius:4px;">
                  <p style="margin:0;font-weight:600;color:#111827;">{entityLabel}</p>
                  <p style="margin:4px 0 0;font-size:14px;color:#059669;">Status: Fully Approved</p>
                </div>
                <p style="color:#6b7280;margin:0;">You can now proceed with implementation or next steps.</p>
              </div>
            </div>
            """;

        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendApprovalRejectedAsync(string toEmail, string toName, string entityLabel, string rejectorName, string comment)
    {
        var subject = $"❌ Rejected: {entityLabel}";
        var body = $"""
            <div style="font-family:Inter,sans-serif;max-width:560px;margin:0 auto;color:#111827;">
              <div style="background:#dc2626;padding:28px 32px;border-radius:12px 12px 0 0;">
                <h1 style="color:#fff;margin:0;font-size:22px;font-weight:700;">PCA Management Portal</h1>
              </div>
              <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                <h2 style="font-size:18px;font-weight:700;margin:0 0 8px;">Approval Rejected</h2>
                <p style="color:#6b7280;margin:0 0 16px;">Hi {toName}, the following item has been rejected:</p>
                <div style="background:#fef2f2;border-left:4px solid #dc2626;padding:16px;margin:0 0 16px;border-radius:4px;">
                  <p style="margin:0;font-weight:600;color:#111827;">{entityLabel}</p>
                  <p style="margin:4px 0 0;font-size:14px;color:#dc2626;">Rejected by: {rejectorName}</p>
                </div>
                <p style="color:#374151;font-weight:600;margin:0 0 8px;font-size:14px;">Rejection Reason:</p>
                <div style="background:#f9fafb;padding:12px;border-radius:4px;margin:0 0 16px;">
                  <p style="margin:0;color:#6b7280;font-size:14px;white-space:pre-wrap;">{comment}</p>
                </div>
                <p style="color:#6b7280;margin:0;">Please review the feedback and take appropriate action.</p>
              </div>
            </div>
            """;

        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendApprovalReturnedAsync(string toEmail, string toName, string entityLabel, string returnerName, string comment)
    {
        var subject = $"📝 Returned for Edit: {entityLabel}";
        var body = $"""
            <div style="font-family:Inter,sans-serif;max-width:560px;margin:0 auto;color:#111827;">
              <div style="background:#d97706;padding:28px 32px;border-radius:12px 12px 0 0;">
                <h1 style="color:#fff;margin:0;font-size:22px;font-weight:700;">PCA Management Portal</h1>
              </div>
              <div style="background:#fff;padding:32px;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 12px 12px;">
                <h2 style="font-size:18px;font-weight:700;margin:0 0 8px;">Returned for Edit</h2>
                <p style="color:#6b7280;margin:0 0 16px;">Hi {toName}, the following item has been returned for corrections:</p>
                <div style="background:#fffbeb;border-left:4px solid #d97706;padding:16px;margin:0 0 16px;border-radius:4px;">
                  <p style="margin:0;font-weight:600;color:#111827;">{entityLabel}</p>
                  <p style="margin:4px 0 0;font-size:14px;color:#d97706;">Returned by: {returnerName}</p>
                </div>
                <p style="color:#374151;font-weight:600;margin:0 0 8px;font-size:14px;">Requested Changes:</p>
                <div style="background:#f9fafb;padding:12px;border-radius:4px;margin:0 0 16px;">
                  <p style="margin:0;color:#6b7280;font-size:14px;white-space:pre-wrap;">{comment}</p>
                </div>
                <p style="color:#6b7280;margin:0;">Please make the necessary changes and resubmit for approval.</p>
              </div>
            </div>
            """;

        await SendAsync(toEmail, toName, subject, body);
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.Host, _settings.Port,
                _settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} — subject: {Subject}", toEmail, subject);
            throw;
        }
    }

    public async Task SendWithAttachmentAsync(
        IEnumerable<(string Email, string Name)> recipients,
        string subject, string textBody,
        string filePath, string fileName, string mimeType)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            foreach (var (email, name) in recipients)
                message.To.Add(new MailboxAddress(name, email));
            message.Subject = subject;

            var builder = new BodyBuilder { TextBody = textBody };
            builder.Attachments.Add(fileName, await File.ReadAllBytesAsync(filePath),
                MimeKit.ContentType.Parse(mimeType));
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.Host, _settings.Port,
                _settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invoice email — subject: {Subject}", subject);
            throw;
        }
    }
}
