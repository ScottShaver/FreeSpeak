using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FreeSpeakWeb.Services.Settings
{
    /// <summary>
    /// Delivers a prepared message through an SMTP server. Abstracted so senders can be tested without a server.
    /// </summary>
    public interface ISmtpMailTransport
    {
        /// <summary>
        /// Connects to the server described by <paramref name="settings"/>, authenticates if required and sends the message.
        /// </summary>
        /// <param name="settings">The SMTP settings to use for this delivery.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendAsync(SmtpSettings settings, MimeMessage message, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// MailKit-based <see cref="ISmtpMailTransport"/>.
    /// </summary>
    public sealed class MailKitSmtpTransport : ISmtpMailTransport
    {
        /// <inheritdoc/>
        public async Task SendAsync(SmtpSettings settings, MimeMessage message, CancellationToken cancellationToken = default)
        {
            using var client = new SmtpClient();
            var secureSocketOptions = settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(settings.Server, settings.Port, secureSocketOptions, cancellationToken);

            if (settings.AuthenticationRequired && !string.IsNullOrEmpty(settings.Account) && !string.IsNullOrEmpty(settings.Password))
            {
                await client.AuthenticateAsync(settings.Account, settings.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
