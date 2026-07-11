using FreeSpeakWeb.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FreeSpeakWeb.Components.Account;

/// <summary>
/// SMTP email sender implementation for Identity that sends emails using MailKit.
/// Configured via SmtpSettings from appsettings.json.
/// </summary>
internal sealed class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly SmtpSettings _smtpSettings;
    private readonly SiteSettings _siteSettings;
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpEmailSender"/> class.
    /// </summary>
    /// <param name="smtpSettings">The SMTP configuration settings.</param>
    /// <param name="logger">The logger instance.</param>
    public SmtpEmailSender(IOptions<SiteSettings> siteSettings,IOptions<SmtpSettings> smtpSettings, ILogger<SmtpEmailSender> logger)
    {
        _siteSettings = siteSettings.Value;
        _smtpSettings = smtpSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sends a confirmation link email to verify the user's email address.
    /// </summary>
    /// <param name="user">The user to send the email to.</param>
    /// <param name="email">The email address to send to.</param>
    /// <param name="confirmationLink">The URL link for email confirmation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var subject = $"{_siteSettings.SiteName} - Confirm your email";
        var htmlBody = $@"
            <h2>Email Confirmation</h2>
            <p>Hello {user.UserName},</p>
            <p>Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.</p>
            <p>If you did not create an account, please ignore this email.</p>";

        await SendEmailAsync(email, subject, htmlBody);
    }

    /// <summary>
    /// Sends a password reset link email to the user.
    /// </summary>
    /// <param name="user">The user to send the email to.</param>
    /// <param name="email">The email address to send to.</param>
    /// <param name="resetLink">The URL link for password reset.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var subject = $"{_siteSettings.SiteName} - Reset your password";
        var htmlBody = $@"
            <h2>Password Reset</h2>
            <p>Hello {user.UserName},</p>
            <p>Please reset your password by <a href='{resetLink}'>clicking here</a>.</p>
            <p>If you did not request a password reset, please ignore this email.</p>";

        await SendEmailAsync(email, subject, htmlBody);
    }

    /// <summary>
    /// Sends a password reset code email to the user.
    /// </summary>
    /// <param name="user">The user to send the email to.</param>
    /// <param name="email">The email address to send to.</param>
    /// <param name="resetCode">The code for password reset.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var subject = $"{_siteSettings.SiteName} - Reset your password";
        var htmlBody = $@"
            <h2>Password Reset Code</h2>
            <p>Hello {user.UserName},</p>
            <p>Please reset your password using the following code:</p>
            <p style='font-size: 20px; font-weight: bold; letter-spacing: 2px;'>{resetCode}</p>
            <p>If you did not request a password reset, please ignore this email.</p>";

        await SendEmailAsync(email, subject, htmlBody);
    }

    /// <summary>
    /// Sends an email using the configured SMTP settings via MailKit.
    /// </summary>
    /// <param name="toEmail">The recipient email address.</param>
    /// <param name="subject">The email subject.</param>
    /// <param name="htmlBody">The HTML body content of the email.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtpSettings.SenderName, _smtpSettings.SenderEmail));
            message.To.Add(new MailboxAddress(toEmail, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            // Connect to the SMTP server
            var secureSocketOptions = _smtpSettings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(_smtpSettings.Server, _smtpSettings.Port, secureSocketOptions);

            // Authenticate if credentials are provided
            if (_smtpSettings.AuthenticationRequired && !string.IsNullOrEmpty(_smtpSettings.Account) && !string.IsNullOrEmpty(_smtpSettings.Password))
            {
                await client.AuthenticateAsync(_smtpSettings.Account, _smtpSettings.Password);
            }

            // Send the email
            await client.SendAsync(message);

            // Disconnect
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {Email} with subject '{Subject}'", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} with subject '{Subject}'", toEmail, subject);
            throw;
        }
    }
}
