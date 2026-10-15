using FluentAssertions;
using FreeSpeakWeb.Configuration;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Services.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services.Settings
{
    /// <summary>
    /// Time provider whose clock can be advanced in tests.
    /// </summary>
    internal sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Advances the clock.
        /// </summary>
        /// <param name="by">The amount to advance.</param>
        public void Advance(TimeSpan by) => _now += by;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>
    /// Tests for <see cref="SettingsValidator"/>.
    /// </summary>
    public class SettingsValidatorTests
    {
        private static IReadOnlyList<SettingValidationError> Run(string group, params (string Key, string? Value)[] values)
        {
            var dict = values.ToDictionary(v => v.Key, v => v.Value);
            return SettingsValidator.Validate(SettingsCatalog.Find(group)!, dict, _ => false);
        }

        /// <summary>Verifies ports outside 1-65535 are rejected.</summary>
        [Theory]
        [InlineData("0")]
        [InlineData("65536")]
        [InlineData("abc")]
        public void Port_OutOfRange_IsRejected(string port)
        {
            Run("smtp", ("Smtp:Port", port)).Should().Contain(e => e.Key == "Smtp:Port");
        }

        /// <summary>Verifies a valid port is accepted.</summary>
        [Fact]
        public void Port_Valid_IsAccepted()
        {
            Run("virusscan", ("VirusScan:ClamAvPort", "3310")).Should().BeEmpty();
        }

        /// <summary>Verifies the login rate limit cannot be set below 3.</summary>
        [Fact]
        public void LoginMaxRequests_BelowThree_IsRejected()
        {
            Run("ratelimiting", ("RateLimiting:Login:MaxRequests", "2")).Should().Contain(e => e.Key == "RateLimiting:Login:MaxRequests");
            Run("ratelimiting", ("RateLimiting:Login:MaxRequests", "3")).Should().BeEmpty();
        }

        /// <summary>Verifies the global limit cannot be zero.</summary>
        [Fact]
        public void GlobalMaxRequests_Zero_IsRejected()
        {
            Run("ratelimiting", ("RateLimiting:Global:MaxRequests", "0")).Should().NotBeEmpty();
        }

        /// <summary>Verifies the sender email must be an email address.</summary>
        [Fact]
        public void SenderEmail_Invalid_IsRejected()
        {
            Run("smtp", ("Smtp:SenderEmail", "nope")).Should().Contain(e => e.Key == "Smtp:SenderEmail");
            Run("smtp", ("Smtp:SenderEmail", "a@b.com")).Should().BeEmpty();
        }

        /// <summary>Verifies hosts are validated.</summary>
        [Theory]
        [InlineData("smtp.example.com", true)]
        [InlineData("localhost", true)]
        [InlineData("bad host!", false)]
        public void Host_Validation(string host, bool valid)
        {
            SettingsValidator.IsHost(host).Should().Be(valid);
        }

        /// <summary>Verifies an unknown log level is rejected.</summary>
        [Fact]
        public void LogLevel_Unknown_IsRejected()
        {
            var key = SettingsCatalog.Find("logging")!.Fields.First().Key;
            Run("logging", (key, "Loud")).Should().NotBeEmpty();
            Run("logging", (key, "warning")).Should().BeEmpty();
        }

        /// <summary>Verifies a required text field cannot be blank.</summary>
        [Fact]
        public void RequiredText_Blank_IsRejected()
        {
            Run("smtp", ("Smtp:Server", " ")).Should().Contain(e => e.Key == "Smtp:Server");
        }
    }

    /// <summary>
    /// Tests for <see cref="DatabaseConfigurationProvider"/> and <see cref="LiveOptions{T}"/>.
    /// </summary>
    public class DatabaseConfigurationProviderTests
    {
        private sealed class FakeReader : ISystemSettingRowReader
        {
            public List<SystemSettingRow> Rows { get; } = new();
            public bool Throw { get; set; }

            public IReadOnlyList<SystemSettingRow> ReadAll()
            {
                if (Throw)
                {
                    throw new InvalidOperationException("no table");
                }

                return Rows.ToList();
            }
        }

        private sealed class Opts
        {
            public string? Name { get; set; }
        }

        /// <summary>Verifies database rows override earlier sources.</summary>
        [Fact]
        public void Rows_OverrideFileValues()
        {
            var reader = new FakeReader();
            reader.Rows.Add(new("Opts:Name", "db", false));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Opts:Name"] = "file" })
                .Add(new DatabaseConfigurationSource { RowReader = reader, PollInterval = Timeout.InfiniteTimeSpan })
                .Build();

            config["Opts:Name"].Should().Be("db");
        }

        /// <summary>Verifies a missing table behaves as no overrides.</summary>
        [Fact]
        public void MissingTable_IsTolerated()
        {
            var reader = new FakeReader { Throw = true };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Opts:Name"] = "file" })
                .Add(new DatabaseConfigurationSource { RowReader = reader, PollInterval = Timeout.InfiniteTimeSpan })
                .Build();

            config["Opts:Name"].Should().Be("file");
        }

        /// <summary>Verifies secret rows are decrypted and ignored without a decryptor.</summary>
        [Fact]
        public void SecretRows_AreDecrypted()
        {
            var reader = new FakeReader();
            reader.Rows.Add(new("Smtp:Password", "enc", true));
            var withKey = new ConfigurationBuilder()
                .Add(new DatabaseConfigurationSource { RowReader = reader, Unprotect = v => "plain-" + v, PollInterval = Timeout.InfiniteTimeSpan })
                .Build();
            var withoutKey = new ConfigurationBuilder()
                .Add(new DatabaseConfigurationSource { RowReader = reader, PollInterval = Timeout.InfiniteTimeSpan })
                .Build();

            withKey["Smtp:Password"].Should().Be("plain-enc");
            withoutKey["Smtp:Password"].Should().BeNull();
        }

        /// <summary>Verifies a reload fires only when content changed and options monitors see the update.</summary>
        [Fact]
        public void Reload_FiresOnlyOnChange_AndUpdatesOptions()
        {
            var reader = new FakeReader();
            reader.Rows.Add(new("Opts:Name", "one", false));
            var source = new DatabaseConfigurationSource { RowReader = reader, PollInterval = Timeout.InfiniteTimeSpan };
            var config = new ConfigurationBuilder().Add(source).Build();
            var changes = 0;
            ChangeToken.OnChange(() => config.GetReloadToken(), () => changes++);
            var monitor = new OptionsMonitor<Opts>(
                new OptionsFactory<Opts>(new[] { new ConfigureFromConfigurationOptions<Opts>(config.GetSection("Opts")) }, Array.Empty<IPostConfigureOptions<Opts>>()),
                new[] { new ConfigurationChangeTokenSource<Opts>(Options.DefaultName, config.GetSection("Opts")) },
                new OptionsCache<Opts>());
            var live = new LiveOptions<Opts>(monitor);

            source.Reload();
            changes.Should().Be(0);

            reader.Rows[0] = new("Opts:Name", "two", false);
            source.Reload();

            changes.Should().Be(1);
            live.Value.Name.Should().Be("two");
        }

        /// <summary>Verifies a failed read keeps previous values.</summary>
        [Fact]
        public void FailedReload_KeepsPreviousValues()
        {
            var reader = new FakeReader();
            reader.Rows.Add(new("Opts:Name", "one", false));
            var source = new DatabaseConfigurationSource { RowReader = reader, PollInterval = Timeout.InfiniteTimeSpan };
            var config = new ConfigurationBuilder().Add(source).Build();

            reader.Throw = true;
            source.Reload();

            config["Opts:Name"].Should().Be("one");
        }
    }

    /// <summary>
    /// Tests for <see cref="ReauthenticationStore"/>.
    /// </summary>
    public class ReauthenticationStoreTests
    {
        /// <summary>Verifies a grant expires after its validity.</summary>
        [Fact]
        public void Grant_ExpiresAfterValidity()
        {
            var time = new ManualTimeProvider();
            var store = new ReauthenticationStore(time);

            store.IsValid("u").Should().BeFalse();
            store.Grant("u");
            store.IsValid("u").Should().BeTrue();
            time.Advance(ReauthenticationStore.Validity + TimeSpan.FromSeconds(1));
            store.IsValid("u").Should().BeFalse();
        }

        /// <summary>Verifies lockout after too many failures and release after the window.</summary>
        [Fact]
        public void Failures_LockOut_ThenRelease()
        {
            var time = new ManualTimeProvider();
            var store = new ReauthenticationStore(time);

            for (var i = 0; i < ReauthenticationStore.MaxFailedAttempts; i++)
            {
                store.IsLockedOut("u").Should().BeFalse();
                store.RegisterFailure("u");
            }

            store.IsLockedOut("u").Should().BeTrue();
            time.Advance(ReauthenticationStore.FailureWindow + TimeSpan.FromSeconds(1));
            store.IsLockedOut("u").Should().BeFalse();
        }
    }

    /// <summary>
    /// Tests for <see cref="SystemSettingsService"/>.
    /// </summary>
    public class SystemSettingsServiceTests
    {
        private readonly Mock<ISystemSettingsRepository> _repo = new();
        private readonly Mock<IRoleService> _roles = new();
        private readonly Mock<IAuditLogRepository> _audit = new();
        private readonly Mock<ISettingsConfigurationReloader> _reloader = new();
        private readonly Mock<IClamAvPinger> _pinger = new();
        private readonly IDataProtectionProvider _dp = new EphemeralDataProtectionProvider();
        private readonly Dictionary<string, string?> _config = new();
        private readonly List<SystemSetting> _upserts = new();

        private SystemSettingsService Create(bool admin = true)
        {
            _roles.Setup(r => r.IsUserInRoleAsync("admin", "SystemAdministrator")).ReturnsAsync(admin);
            _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SystemSetting>());
            _repo.Setup(r => r.UpsertManyAsync(It.IsAny<IReadOnlyCollection<SystemSetting>>(), It.IsAny<CancellationToken>()))
                .Callback<IReadOnlyCollection<SystemSetting>, CancellationToken>((s, _) => _upserts.AddRange(s))
                .Returns(Task.CompletedTask);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(_config).Build();
            return new SystemSettingsService(_repo.Object, configuration, _roles.Object, _audit.Object, _reloader.Object,
                _dp, _pinger.Object, TimeProvider.System, NullLogger<SystemSettingsService>.Instance);
        }

        /// <summary>Verifies non-administrators are denied.</summary>
        [Fact]
        public async Task NonAdmin_IsDenied()
        {
            var svc = Create(admin: false);

            await FluentActions.Awaiting(() => svc.SaveGroupAsync("virusscan", new Dictionary<string, string?>(), "admin"))
                .Should().ThrowAsync<UnauthorizedAccessException>();
            await FluentActions.Awaiting(() => svc.GetGroupAsync("virusscan", "admin"))
                .Should().ThrowAsync<UnauthorizedAccessException>();
        }

        /// <summary>Verifies only values that differ from the effective value are written.</summary>
        [Fact]
        public async Task Save_WritesOnlyChangedKeys()
        {
            var svc = Create();

            var result = await svc.SaveGroupAsync("virusscan", new Dictionary<string, string?>
            {
                ["VirusScan:ClamAvPort"] = "3310",
                ["VirusScan:ClamAvHost"] = "clam",
            }, "admin");

            result.Success.Should().BeTrue();
            _upserts.Select(u => u.Key).Should().BeEquivalentTo("VirusScan:ClamAvHost");
            _reloader.Verify(r => r.Reload(), Times.Once);
            _audit.Verify(a => a.LogActionAsync("admin", ActionCategory.SystemSettingChanged, It.IsAny<SystemSettingChangedDetails>()), Times.Once);
        }

        /// <summary>Verifies invalid input writes nothing.</summary>
        [Fact]
        public async Task Save_Invalid_WritesNothing()
        {
            var svc = Create();

            var result = await svc.SaveGroupAsync("virusscan", new Dictionary<string, string?> { ["VirusScan:ClamAvPort"] = "0" }, "admin");

            result.Success.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
            _upserts.Should().BeEmpty();
        }

        /// <summary>Verifies secrets are encrypted and their values are not audited.</summary>
        [Fact]
        public async Task Save_Secret_IsEncrypted_AndNotAudited()
        {
            var svc = Create();
            SystemSettingChangedDetails? details = null;
            _audit.Setup(a => a.LogActionAsync("admin", ActionCategory.SystemSettingChanged, It.IsAny<SystemSettingChangedDetails>()))
                .Callback<string, ActionCategory, SystemSettingChangedDetails>((_, _, d) => details = d)
                .Returns(Task.CompletedTask);

            var result = await svc.SaveGroupAsync("smtp", new Dictionary<string, string?>
            {
                ["Smtp:AuthenticationRequired"] = "false",
                ["Smtp:Password"] = "hunter2",
            }, "admin");

            result.Success.Should().BeTrue();
            var row = _upserts.Single(u => u.Key == "Smtp:Password");
            row.IsSecret.Should().BeTrue();
            row.Value.Should().NotContain("hunter2");
            details.Should().NotBeNull();
            details!.IsSecret.Should().BeTrue();
            details.OldValue.Should().BeNull();
            details.NewValue.Should().BeNull();
        }

        /// <summary>Verifies the Smtp enabled flag is inverted onto UseIdentityNoOpEmailSender.</summary>
        [Fact]
        public async Task Save_SmtpEnabled_IsInverted()
        {
            var svc = Create();
            var field = SettingsCatalog.Find("smtp")!.Fields.First(f => f.Invert);

            await svc.SaveGroupAsync("smtp", new Dictionary<string, string?> { [field.Key] = "true", ["Smtp:AuthenticationRequired"] = "false" }, "admin");

            _upserts.Single(u => u.Key == field.Key).Value.Should().Be("false");
        }

        /// <summary>Verifies reset deletes rows and audits.</summary>
        [Fact]
        public async Task Reset_DeletesRows_AndAudits()
        {
            var svc = Create();
            _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SystemSetting> { new() { Key = "VirusScan:ClamAvHost", Value = "x" } });
            _repo.Setup(r => r.DeleteManyAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var removed = await svc.ResetGroupAsync("virusscan", "admin");

            removed.Should().Be(1);
            _reloader.Verify(r => r.Reload(), Times.Once);
            _audit.Verify(a => a.LogActionAsync("admin", ActionCategory.SystemSettingReset, It.IsAny<SystemSettingResetDetails>()), Times.Once);
        }

        /// <summary>Verifies the ClamAV connection test reports success and failure.</summary>
        [Fact]
        public async Task TestConnection_ReportsOutcome()
        {
            var svc = Create();
            _pinger.Setup(p => p.PingAsync("clam", 3310, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _pinger.Setup(p => p.PingAsync("down", 3310, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            (await svc.TestVirusScanConnectionAsync("clam", "3310", "admin")).Success.Should().BeTrue();
            var failed = await svc.TestVirusScanConnectionAsync("down", "3310", "admin");
            failed.Success.Should().BeFalse();
            failed.ErrorKey.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>
    /// Tests for <see cref="EffectiveConfigurationService"/> masking.
    /// </summary>
    public class EffectiveConfigurationMaskingTests
    {
        /// <summary>Verifies secrets and connection strings are masked.</summary>
        [Fact]
        public void MaskEntry_MasksSensitiveValues()
        {
            EffectiveConfigurationService.MaskEntry("Smtp:Password", "hunter2").Value.Should().Be(EffectiveConfigurationService.Mask);
            EffectiveConfigurationService.MaskEntry("ConnectionStrings:DefaultConnection", "Host=x;Password=y").Value.Should().NotContain("y").And.Contain("Host=x");
            EffectiveConfigurationService.MaskEntry("Smtp:Server", "smtp.example.com").Value.Should().Be("smtp.example.com");
        }
    }
}
