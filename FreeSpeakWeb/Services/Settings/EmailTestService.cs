using System.Collections.Concurrent;
using System.Net.Sockets;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using MailKit.Security;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FreeSpeakWeb.Services.Settings
{
    /// <summary>
    /// Tracks test email sends per administrator in a sliding one-hour window.
    /// Registered as a singleton so the limit survives across requests and circuits.
    /// </summary>
    public sealed class EmailTestRateLimiter
    {
        private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _sends = new();
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailTestRateLimiter"/> class.
        /// </summary>
        /// <param name="timeProvider">The time provider.</param>
        public EmailTestRateLimiter(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Attempts to consume one send for the user.
        /// </summary>
        /// <param name="userId">The administrator.</param>
        /// <param name="limit">The maximum number of sends per hour.</param>
        /// <returns>True if the send is allowed and has been counted; false if the limit is reached.</returns>
        public bool TryAcquire(string userId, int limit)
        {
            var now = _timeProvider.GetUtcNow();
            var queue = _sends.GetOrAdd(userId, _ => new Queue<DateTimeOffset>());
            lock (queue)
            {
                while (queue.Count > 0 && now - queue.Peek() >= TimeSpan.FromHours(1))
                {
                    queue.Dequeue();
                }

                if (queue.Count >= limit)
                {
                    return false;
                }

                queue.Enqueue(now);
                return true;
            }
        }
    }

    /// <summary>
    /// Default <see cref="IEmailTestService"/>.
    /// </summary>
    public class EmailTestService : IEmailTestService
    {
        /// <summary>The maximum number of test emails per administrator per hour.</summary>
        public const int MaxTestsPerHour = 5;

        private const string SystemAdministratorRole = "SystemAdministrator";

        private readonly IOptionsMonitor<SmtpSettings> _smtpSettings;
        private readonly IOptions<SiteSettings> _siteSettings;
        private readonly ISmtpMailTransport _transport;
        private readonly IRoleService _roleService;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly EmailTestRateLimiter _rateLimiter;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<EmailTestService> _logger;
        private readonly IStringLocalizer<Resources.Services.Settings.EmailTestService> _localizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailTestService"/> class.
        /// </summary>
        /// <param name="smtpSettings">Monitor for the current SMTP settings.</param>
        /// <param name="siteSettings">The site settings (for the site name in the message).</param>
        /// <param name="transport">The SMTP transport.</param>
        /// <param name="roleService">The role service used for authorization.</param>
        /// <param name="auditLogRepository">The audit log repository.</param>
        /// <param name="rateLimiter">The per-administrator rate limiter.</param>
        /// <param name="timeProvider">The time provider.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="localizer">Optional localizer for the test email subject and body.</param>
        public EmailTestService(
            IOptionsMonitor<SmtpSettings> smtpSettings,
            IOptions<SiteSettings> siteSettings,
            ISmtpMailTransport transport,
            IRoleService roleService,
            IAuditLogRepository auditLogRepository,
            EmailTestRateLimiter rateLimiter,
            TimeProvider timeProvider,
            ILogger<EmailTestService> logger,
            IStringLocalizer<Resources.Services.Settings.EmailTestService>? localizer = null)
        {
            _localizer = ServiceLocalizer.Resolve(localizer);
            _smtpSettings = smtpSettings;
            _siteSettings = siteSettings;
            _transport = transport;
            _roleService = roleService;
            _auditLogRepository = auditLogRepository;
            _rateLimiter = rateLimiter;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<TestEmailResult> SendTestEmailAsync(string? recipient, string adminUserId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adminUserId) || !await _roleService.IsUserInRoleAsync(adminUserId, SystemAdministratorRole))
            {
                throw new UnauthorizedAccessException("Only system administrators can send test emails.");
            }

            var to = (recipient ?? string.Empty).Trim();
            if (!SettingsValidator.IsEmail(to))
            {
                return new TestEmailResult(false, "E_Email");
            }

            if (!_rateLimiter.TryAcquire(adminUserId, MaxTestsPerHour))
            {
                await AuditAsync(adminUserId, to, false, "E_TestRateLimited");
                return new TestEmailResult(false, "E_TestRateLimited");
            }

            var settings = _smtpSettings.CurrentValue;
            string? errorKey = null;
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
                message.To.Add(new MailboxAddress(to, to));
                message.Subject = string.Format(_localizer["TestEmailSubject"], _siteSettings.Value.SiteName);
                message.Body = new BodyBuilder
                {
                    HtmlBody = _localizer["TestEmailBody"]
                }.ToMessageBody();

                await _transport.SendAsync(settings, message, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errorKey = Classify(ex);
                _logger.LogWarning("Test email to {Recipient} failed ({Reason}): {ExceptionType}", to, errorKey, ex.GetType().Name);
            }

            await AuditAsync(adminUserId, to, errorKey == null, errorKey);
            return new TestEmailResult(errorKey == null, errorKey);
        }

        private static string Classify(Exception ex) => ex switch
        {
            AuthenticationException => "E_SmtpAuth",
            SslHandshakeException => "E_SmtpTls",
            SocketException => "E_SmtpConnect",
            IOException => "E_SmtpConnect",
            TimeoutException => "E_SmtpConnect",
            FormatException => "E_Email",
            _ => "E_SmtpFailed"
        };

        private Task AuditAsync(string adminUserId, string recipient, bool success, string? reason) =>
            _auditLogRepository.LogActionAsync(adminUserId, ActionCategory.EmailTestSent, new EmailTestSentDetails
            {
                OperationType = nameof(OperationTypeEnum.Send),
                Recipient = recipient,
                Success = success,
                FailureReason = reason,
                SentAtUtc = _timeProvider.GetUtcNow().UtcDateTime
            });
    }
}
