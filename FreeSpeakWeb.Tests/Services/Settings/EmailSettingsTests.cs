using FluentAssertions;
using FreeSpeakWeb.Components.Account;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Services.Settings;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services.Settings
{
    /// <summary>
    /// Mutable options monitor so tests can simulate a live settings change.
    /// </summary>
    internal sealed class MutableSmtpMonitor : IOptionsMonitor<SmtpSettings>
    {
        /// <inheritdoc/>
        public SmtpSettings CurrentValue { get; set; } = new() { Server = "smtp.one", SenderEmail = "a@example.com", Password = "pw-secret" };

        /// <inheritdoc/>
        public SmtpSettings Get(string? name) => CurrentValue;

        /// <inheritdoc/>
        public IDisposable? OnChange(Action<SmtpSettings, string?> listener) => null;
    }

    /// <summary>
    /// Tests for <see cref="EmailTestService"/> and <see cref="EmailTestRateLimiter"/>.
    /// </summary>
    public class EmailTestServiceTests
    {
        private readonly Mock<ISmtpMailTransport> _transport = new();
        private readonly Mock<IRoleService> _roles = new();
        private readonly Mock<IAuditLogRepository> _audit = new();
        private readonly ManualTimeProvider _time = new();
        private readonly EmailTestRateLimiter _limiter;
        private readonly List<EmailTestSentDetails> _audits = new();

        public EmailTestServiceTests()
        {
            _limiter = new EmailTestRateLimiter(_time);
            _roles.Setup(r => r.IsUserInRoleAsync("admin", "SystemAdministrator")).ReturnsAsync(true);
            _audit.Setup(a => a.LogActionAsync("admin", ActionCategory.EmailTestSent, It.IsAny<AuditLogDetailsBase>()))
                .Callback<string, ActionCategory, AuditLogDetailsBase>((_, _, d) => _audits.Add((EmailTestSentDetails)d))
                .Returns(Task.CompletedTask);
        }

        private EmailTestService Create() => new(
            new MutableSmtpMonitor(), Options.Create(new SiteSettings()), _transport.Object, _roles.Object,
            _audit.Object, _limiter, _time, NullLogger<EmailTestService>.Instance);

        /// <summary>Verifies non-administrators are denied.</summary>
        [Fact]
        public async Task NonAdmin_IsDenied()
        {
            await FluentActions.Awaiting(() => Create().SendTestEmailAsync("x@example.com", "someone"))
                .Should().ThrowAsync<UnauthorizedAccessException>();
            _transport.VerifyNoOtherCalls();
        }

        /// <summary>Verifies an invalid recipient is rejected without sending.</summary>
        [Fact]
        public async Task InvalidRecipient_IsRejected()
        {
            var result = await Create().SendTestEmailAsync("not-an-email", "admin");

            result.Success.Should().BeFalse();
            result.ErrorKey.Should().Be("E_Email");
            _transport.VerifyNoOtherCalls();
        }

        /// <summary>Verifies a successful send is audited without the password.</summary>
        [Fact]
        public async Task Success_IsAudited_WithoutPassword()
        {
            var result = await Create().SendTestEmailAsync("x@example.com", "admin");

            result.Success.Should().BeTrue();
            _audits.Should().ContainSingle();
            _audits[0].Success.Should().BeTrue();
            _audits[0].SentAtUtc.Should().Be(_time.GetUtcNow().UtcDateTime);
            System.Text.Json.JsonSerializer.Serialize(_audits[0]).Should().NotContain("pw-secret");
        }

        /// <summary>Verifies failures return a sanitized key and never expose exception text or the password.</summary>
        [Fact]
        public async Task Failure_ReturnsSanitizedKey()
        {
            _transport.Setup(t => t.SendAsync(It.IsAny<SmtpSettings>(), It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new AuthenticationException("bad login pw-secret"));

            var result = await Create().SendTestEmailAsync("x@example.com", "admin");

            result.Success.Should().BeFalse();
            result.ErrorKey.Should().Be("E_SmtpAuth");
            _audits.Single().FailureReason.Should().Be("E_SmtpAuth");
            System.Text.Json.JsonSerializer.Serialize(_audits[0]).Should().NotContain("pw-secret");
        }

        /// <summary>Verifies the sixth test email within an hour is blocked, and allowed again later.</summary>
        [Fact]
        public async Task RateLimit_FiveSendsPerHourPerAdmin()
        {
            var svc = Create();
            for (var i = 0; i < 5; i++)
            {
                (await svc.SendTestEmailAsync("x@example.com", "admin")).Success.Should().BeTrue();
            }

            var blocked = await svc.SendTestEmailAsync("x@example.com", "admin");
            blocked.ErrorKey.Should().Be("E_TestRateLimited");
            _transport.Verify(t => t.SendAsync(It.IsAny<SmtpSettings>(), It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(5));

            _time.Advance(TimeSpan.FromMinutes(61));
            (await svc.SendTestEmailAsync("x@example.com", "admin")).Success.Should().BeTrue();
        }

        /// <summary>Verifies the limit is tracked per administrator.</summary>
        [Fact]
        public void RateLimiter_IsPerUser()
        {
            for (var i = 0; i < 5; i++)
            {
                _limiter.TryAcquire("a", 5).Should().BeTrue();
            }

            _limiter.TryAcquire("a", 5).Should().BeFalse();
            _limiter.TryAcquire("b", 5).Should().BeTrue();
        }
    }

    /// <summary>
    /// Tests for <see cref="SmtpEmailSender"/> live settings and Enabled behavior.
    /// </summary>
    public class SmtpEmailSenderTests
    {
        private readonly Mock<ISmtpMailTransport> _transport = new();
        private readonly MutableSmtpMonitor _monitor = new();

        private SmtpEmailSender Create(bool noOp) => new(
            Options.Create(new SiteSettings()), _monitor,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["UseIdentityNoOpEmailSender"] = noOp.ToString() }).Build(),
            _transport.Object, NullLogger<SmtpEmailSender>.Instance);

        /// <summary>Verifies each send uses the settings current at that time.</summary>
        [Fact]
        public async Task Send_UsesCurrentSettings_AfterChange()
        {
            var seen = new List<string>();
            _transport.Setup(t => t.SendAsync(It.IsAny<SmtpSettings>(), It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()))
                .Callback<SmtpSettings, MimeMessage, CancellationToken>((s, _, _) => seen.Add(s.Server)).Returns(Task.CompletedTask);
            var sender = Create(noOp: false);
            var user = new ApplicationUser { UserName = "u" };

            await sender.SendPasswordResetCodeAsync(user, "u@example.com", "123");
            _monitor.CurrentValue = new SmtpSettings { Server = "smtp.two", SenderEmail = "b@example.com" };
            await sender.SendPasswordResetCodeAsync(user, "u@example.com", "123");

            seen.Should().Equal("smtp.one", "smtp.two");
        }

        /// <summary>Verifies nothing is sent while email is disabled.</summary>
        [Fact]
        public async Task Send_IsSkipped_WhenDisabled()
        {
            await Create(noOp: true).SendConfirmationLinkAsync(new ApplicationUser { UserName = "u" }, "u@example.com", "http://x");

            _transport.VerifyNoOtherCalls();
        }
    }
}
