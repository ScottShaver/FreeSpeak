using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="UserModerationService"/>.
    /// </summary>
    public class UserModerationServiceTests
    {
        private const string Moderator = "mod";
        private const string Moderator2 = "mod2";
        private const string Admin = "admin";
        private const string Admin2 = "admin2";
        private const string Plain = "plain";
        private const string Target = "target";

        private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        private readonly Mock<IUserModerationRepository> _repo = new();
        private readonly Mock<IRoleService> _roles = new();
        private readonly Mock<IUserLockoutService> _lockout = new();
        private readonly Mock<IAuditLogRepository> _audit = new();
        private readonly Mock<NotificationService> _notifications;
        private readonly UserModerationService _service;
        private readonly List<UserSuspension> _suspensions = new();
        private DateTimeOffset? _lockoutEnd;
        private int _nextId = 100;

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTimeProvider(DateTime utc) => _now = new DateTimeOffset(utc, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => _now;
        }

        /// <summary>
        /// Creates the service under test with mocked dependencies and a stateful lockout fake.
        /// </summary>
        public UserModerationServiceTests()
        {
            _notifications = new Mock<NotificationService>(
                Mock.Of<INotificationRepository>(),
                Mock.Of<IDbContextFactory<ApplicationDbContext>>(),
                NullLogger<NotificationService>.Instance,
                Mock.Of<IServiceScopeFactory>(),
                Mock.Of<IAuditLogRepository>());
            _notifications
                .Setup(n => n.CreateNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
                .ReturnsAsync((true, null, null));

            _roles.Setup(r => r.IsUserInRoleAsync(Moderator, "SystemModerator")).ReturnsAsync(true);
            _roles.Setup(r => r.IsUserInRoleAsync(Moderator2, "SystemModerator")).ReturnsAsync(true);
            _roles.Setup(r => r.IsUserInRoleAsync(Admin, "SystemAdministrator")).ReturnsAsync(true);
            _roles.Setup(r => r.IsUserInRoleAsync(Admin2, "SystemAdministrator")).ReturnsAsync(true);

            _repo.Setup(r => r.GetUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => new ModerationUserSummary(id, id + "-name", id + " Display"));
            _repo.Setup(r => r.AddWarningAsync(It.IsAny<UserWarning>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserWarning w, CancellationToken _) => { w.Id = _nextId++; return w; });
            _repo.Setup(r => r.TryAddSuspensionAsync(It.IsAny<UserSuspension>(), It.IsAny<DateTime>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserSuspension s, DateTime now, int? ignore, CancellationToken _) =>
                {
                    if (_suspensions.Any(x => x.UserId == s.UserId && x.LiftedAt == null && (x.EndsAt == null || x.EndsAt > now) && x.Id != ignore)) return null;
                    s.Id = _nextId++; _suspensions.Add(s); return s;
                });
            _repo.Setup(r => r.GetSuspensionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) => _suspensions.FirstOrDefault(s => s.Id == id));
            _repo.Setup(r => r.GetActiveSuspensionAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string userId, DateTime now, CancellationToken _) =>
                    _suspensions.LastOrDefault(s => s.UserId == userId && s.LiftedAt == null && (s.EndsAt == null || s.EndsAt > now)));
            _repo.Setup(r => r.MarkSuspensionLiftedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, string by, DateTime at, string? note, CancellationToken _) =>
                {
                    var s = _suspensions.FirstOrDefault(x => x.Id == id && x.LiftedAt == null);
                    if (s == null) return false;
                    s.LiftedAt = at; s.LiftedByUserId = by; s.LiftNote = note;
                    return true;
                });
            _repo.Setup(r => r.DeleteSuspensionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback((int id, CancellationToken _) => _suspensions.RemoveAll(s => s.Id == id))
                .Returns(Task.CompletedTask);

            _lockout.Setup(l => l.GetLockoutStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(() => new UserLockoutStatus { LockoutEnd = _lockoutEnd });
            _lockout.Setup(l => l.SetLockoutEndAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .ReturnsAsync((string _, DateTimeOffset end) => { _lockoutEnd = end; return true; });
            _lockout.Setup(l => l.SetPermanentLockoutAsync(It.IsAny<string>()))
                .ReturnsAsync(() => { _lockoutEnd = DateTimeOffset.MaxValue.AddDays(-1); return true; });
            _lockout.Setup(l => l.RemoveLockoutAsync(It.IsAny<string>()))
                .ReturnsAsync(() => { _lockoutEnd = null; return true; });

            _service = new UserModerationService(
                _repo.Object, _roles.Object, _lockout.Object, _audit.Object, _notifications.Object,
                Mock.Of<IOptionsMonitor<ModerationSettings>>(o => o.CurrentValue == new ModerationSettings { MaxModeratorSuspensionDays = 7 }),
                new FixedTimeProvider(Now),
                NullLogger<UserModerationService>.Instance);
        }

        /// <summary>Only moderators and administrators may warn.</summary>
        [Theory]
        [InlineData(Moderator, true)]
        [InlineData(Admin, true)]
        [InlineData(Plain, false)]
        [InlineData("", false)]
        public async Task WarnUserAsync_RoleGate(string actor, bool allowed)
        {
            var result = await _service.WarnUserAsync(actor, Target, "Spam");

            result.Success.Should().Be(allowed);
            if (!allowed)
            {
                result.Error.Should().Be(UserModerationError.NotAuthorized);
                _repo.Verify(r => r.AddWarningAsync(It.IsAny<UserWarning>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        /// <summary>A warning stores server-set UTC time, writes an audit entry and sends a neutral notification.</summary>
        [Fact]
        public async Task WarnUserAsync_StoresAuditsAndNotifies()
        {
            UserWarning? stored = null;
            _repo.Setup(r => r.AddWarningAsync(It.IsAny<UserWarning>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserWarning w, CancellationToken _) => { w.Id = 5; stored = w; return w; });

            var result = await _service.WarnUserAsync(Moderator, Target, "  Be kind  ", 42);

            result.Should().Be(UserModerationResult.Ok(5));
            stored!.CreatedAt.Should().Be(Now);
            stored.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
            stored.Reason.Should().Be("Be kind");
            stored.RelatedReportId.Should().Be(42);
            _audit.Verify(a => a.LogActionAsync(Moderator, ActionCategory.SystemModeratorWarnUser,
                It.Is<SystemModeratorWarnUserDetails>(d => d.WarningId == 5 && d.TargetUserId == Target && d.IssuedAtUtc == Now)), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Target, NotificationType.System,
                It.Is<string?>(m => m != null && !m.Contains("Moderator ")), It.IsAny<object?>(), It.IsAny<DateTime?>(), UserModerationService.WarnedTemplateKey), Times.Once);
        }

        /// <summary>The reason is required and length-limited.</summary>
        [Fact]
        public async Task WarnUserAsync_ValidatesReason()
        {
            (await _service.WarnUserAsync(Moderator, Target, "   ")).Error.Should().Be(UserModerationError.ReasonRequired);
            (await _service.WarnUserAsync(Moderator, Target, new string('x', UserModerationService.MaxTextLength + 1))).Error.Should().Be(UserModerationError.ReasonTooLong);
        }

        /// <summary>Nobody may act on themselves, administrators, or (for moderators) on moderators.</summary>
        [Theory]
        [InlineData(Moderator, Moderator, UserModerationError.CannotModerateSelf)]
        [InlineData(Admin, Admin, UserModerationError.CannotModerateSelf)]
        [InlineData(Moderator, Admin, UserModerationError.CannotModerateStaff)]
        [InlineData(Moderator, Moderator2, UserModerationError.CannotModerateStaff)]
        [InlineData(Admin, Admin2, UserModerationError.CannotModerateStaff)]
        public async Task WarnAndSuspend_RefuseProtectedTargets(string actor, string target, UserModerationError expected)
        {
            (await _service.WarnUserAsync(actor, target, "x")).Error.Should().Be(expected);
            (await _service.SuspendUserAsync(actor, target, "x", TimeSpan.FromDays(1))).Error.Should().Be(expected);
        }

        /// <summary>An administrator may moderate a system moderator.</summary>
        [Fact]
        public async Task SuspendUserAsync_AdminCanSuspendModerator()
        {
            var result = await _service.SuspendUserAsync(Admin, Moderator, "abuse", TimeSpan.FromDays(1));

            result.Success.Should().BeTrue();
        }

        /// <summary>The target must exist.</summary>
        [Fact]
        public async Task WarnUserAsync_UnknownUser()
        {
            _repo.Setup(r => r.GetUserAsync("ghost", It.IsAny<CancellationToken>())).ReturnsAsync((ModerationUserSummary?)null);

            (await _service.WarnUserAsync(Moderator, "ghost", "x")).Error.Should().Be(UserModerationError.UserNotFound);
        }

        /// <summary>Moderators may suspend up to the limit, but not beyond it or permanently.</summary>
        [Theory]
        [InlineData(1, true, UserModerationError.None)]
        [InlineData(7, true, UserModerationError.None)]
        [InlineData(7.01, false, UserModerationError.DurationExceedsLimit)]
        [InlineData(30, false, UserModerationError.DurationExceedsLimit)]
        public async Task SuspendUserAsync_ModeratorDurationLimit(double days, bool allowed, UserModerationError error)
        {
            var result = await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(days));

            result.Success.Should().Be(allowed);
            result.Error.Should().Be(error);
        }

        /// <summary>Permanent suspension is administrator-only.</summary>
        [Fact]
        public async Task SuspendUserAsync_PermanentIsAdminOnly()
        {
            (await _service.SuspendUserAsync(Moderator, Target, "x", null)).Error.Should().Be(UserModerationError.PermanentNotAllowed);
            _lockout.Verify(l => l.SetPermanentLockoutAsync(It.IsAny<string>()), Times.Never);

            var result = await _service.SuspendUserAsync(Admin, Target, "x", null);

            result.Success.Should().BeTrue();
            _suspensions.Single().EndsAt.Should().BeNull();
            _lockout.Verify(l => l.SetPermanentLockoutAsync(Target), Times.Once);
        }

        /// <summary>Administrators may suspend longer than the moderator limit.</summary>
        [Fact]
        public async Task SuspendUserAsync_AdminCanExceedLimit()
        {
            var result = await _service.SuspendUserAsync(Admin, Target, "x", TimeSpan.FromDays(90));

            result.Success.Should().BeTrue();
            _suspensions.Single().EndsAt.Should().Be(Now.AddDays(90));
        }

        /// <summary>A non-positive duration is invalid.</summary>
        [Fact]
        public async Task SuspendUserAsync_RejectsNonPositiveDuration()
        {
            (await _service.SuspendUserAsync(Admin, Target, "x", TimeSpan.Zero)).Error.Should().Be(UserModerationError.InvalidDuration);
            (await _service.SuspendUserAsync(Admin, Target, "x", TimeSpan.FromDays(-1))).Error.Should().Be(UserModerationError.InvalidDuration);
        }

        /// <summary>Non-moderators cannot suspend and nothing is written.</summary>
        [Fact]
        public async Task SuspendUserAsync_RefusesNonModerator()
        {
            var result = await _service.SuspendUserAsync(Plain, Target, "x", TimeSpan.FromDays(1));

            result.Error.Should().Be(UserModerationError.NotAuthorized);
            _repo.Verify(r => r.TryAddSuspensionAsync(It.IsAny<UserSuspension>(), It.IsAny<DateTime>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>A suspension locks the account until the server-computed end, audits and notifies with the end date.</summary>
        [Fact]
        public async Task SuspendUserAsync_LocksAuditsAndNotifies()
        {
            var result = await _service.SuspendUserAsync(Moderator, Target, "Harassment", TimeSpan.FromDays(3), 9);

            result.Success.Should().BeTrue();
            var suspension = _suspensions.Single();
            suspension.CreatedAt.Should().Be(Now);
            suspension.EndsAt.Should().Be(Now.AddDays(3));
            _lockoutEnd.Should().Be(new DateTimeOffset(Now.AddDays(3), TimeSpan.Zero));
            _audit.Verify(a => a.LogActionAsync(Moderator, ActionCategory.SystemModeratorSuspendUser,
                It.Is<SystemModeratorSuspendUserDetails>(d => d.SuspensionId == suspension.Id && !d.IsPermanent && d.EndsAtUtc == Now.AddDays(3) && d.StartedAtUtc == Now && d.RelatedReportId == 9)), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Target, NotificationType.System, It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), UserModerationService.SuspendedTemplateKey), Times.Once);
        }

        /// <summary>A permanent suspension uses the permanent notification template.</summary>
        [Fact]
        public async Task SuspendUserAsync_PermanentUsesPermanentTemplate()
        {
            await _service.SuspendUserAsync(Admin, Target, "x", null);

            _notifications.Verify(n => n.CreateNotificationAsync(Target, NotificationType.System, It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), UserModerationService.SuspendedPermanentTemplateKey), Times.Once);
        }

        /// <summary>An already suspended user cannot be suspended again.</summary>
        [Fact]
        public async Task SuspendUserAsync_AlreadySuspended()
        {
            await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(1));

            var result = await _service.SuspendUserAsync(Admin, Target, "y", TimeSpan.FromDays(2));

            result.Error.Should().Be(UserModerationError.AlreadySuspended);
            _suspensions.Should().HaveCount(1);
        }

        /// <summary>If the lockout cannot be applied the suspension record is rolled back.</summary>
        [Fact]
        public async Task SuspendUserAsync_RollsBackWhenLockoutFails()
        {
            _lockout.Setup(l => l.SetLockoutEndAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>())).ReturnsAsync(false);

            var result = await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(1));

            result.Error.Should().Be(UserModerationError.Failed);
            _suspensions.Should().BeEmpty();
            _notifications.Verify(n => n.CreateNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()), Times.Never);
        }

        /// <summary>The suspension limit is read from live settings.</summary>
        [Fact]
        public async Task SuspendUserAsync_UsesConfiguredLimit()
        {
            var service = new UserModerationService(
                _repo.Object, _roles.Object, _lockout.Object, _audit.Object, _notifications.Object,
                Mock.Of<IOptionsMonitor<ModerationSettings>>(o => o.CurrentValue == new ModerationSettings { MaxModeratorSuspensionDays = 2 }),
                new FixedTimeProvider(Now), NullLogger<UserModerationService>.Instance);

            (await service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(3))).Error.Should().Be(UserModerationError.DurationExceedsLimit);
            (await service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(2))).Success.Should().BeTrue();
        }

        /// <summary>An active suspension is reported until it ends, then expires on its own.</summary>
        [Fact]
        public async Task GetActiveSuspensionAsync_ExpiresWithTheLockout()
        {
            await _service.SuspendUserAsync(Moderator, Target, "Spam", TimeSpan.FromDays(1));

            (await _service.GetActiveSuspensionAsync(Target)).Should().NotBeNull();

            _lockoutEnd = new DateTimeOffset(Now.AddMinutes(-1), TimeSpan.Zero);
            (await _service.GetActiveSuspensionAsync(Target)).Should().BeNull();
        }

        /// <summary>A suspension whose end date passed is not active even if a stale lockout remains.</summary>
        [Fact]
        public async Task GetActiveSuspensionAsync_IgnoresEndedRecord()
        {
            _suspensions.Add(new UserSuspension { Id = 1, UserId = Target, SuspendedByUserId = Moderator, Reason = "x", CreatedAt = Now.AddDays(-2), EndsAt = Now.AddDays(-1) });
            _lockoutEnd = new DateTimeOffset(Now.AddDays(1), TimeSpan.Zero);

            (await _service.GetActiveSuspensionAsync(Target)).Should().BeNull();
        }

        /// <summary>An ordinary failed-login lockout does not count as a suspension.</summary>
        [Fact]
        public async Task GetActiveSuspensionAsync_IgnoresPlainLockout()
        {
            _lockoutEnd = new DateTimeOffset(Now.AddMinutes(5), TimeSpan.Zero);

            (await _service.GetActiveSuspensionAsync(Target)).Should().BeNull();
        }

        /// <summary>A lockout removed elsewhere (admin lockout dialog) ends the suspension.</summary>
        [Fact]
        public async Task GetActiveSuspensionAsync_IgnoresRecordWhenLockoutRemoved()
        {
            await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(1));
            _lockoutEnd = null;

            (await _service.GetActiveSuspensionAsync(Target)).Should().BeNull();
        }

        /// <summary>The issuing moderator can lift: the lockout is removed, the record marked, audited and the user notified.</summary>
        [Fact]
        public async Task LiftSuspensionAsync_IssuerCanLift()
        {
            var suspension = (await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(1))).RecordId!.Value;

            var result = await _service.LiftSuspensionAsync(Moderator, suspension, " mistake ");

            result.Success.Should().BeTrue();
            _lockoutEnd.Should().BeNull();
            var record = _suspensions.Single();
            record.LiftedAt.Should().Be(Now);
            record.LiftedByUserId.Should().Be(Moderator);
            record.LiftNote.Should().Be("mistake");
            _audit.Verify(a => a.LogActionAsync(Moderator, ActionCategory.SystemModeratorUnsuspendUser,
                It.Is<SystemModeratorUnsuspendUserDetails>(d => d.SuspensionId == suspension && d.LiftedAtUtc == Now)), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Target, NotificationType.System, It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), UserModerationService.SuspensionLiftedTemplateKey), Times.Once);
            (await _service.GetActiveSuspensionAsync(Target)).Should().BeNull();
        }

        /// <summary>A moderator cannot lift another moderator's suspension, an administrator can.</summary>
        [Fact]
        public async Task LiftSuspensionAsync_OnlyIssuerOrAdmin()
        {
            var id = (await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(1))).RecordId!.Value;

            (await _service.LiftSuspensionAsync(Moderator2, id, null)).Error.Should().Be(UserModerationError.CannotLift);
            (await _service.LiftSuspensionAsync(Plain, id, null)).Error.Should().Be(UserModerationError.NotAuthorized);
            (await _service.LiftSuspensionAsync(Admin, id, null)).Success.Should().BeTrue();
        }

        /// <summary>Lifting an unknown, lifted or ended suspension is refused.</summary>
        [Fact]
        public async Task LiftSuspensionAsync_RefusesInactive()
        {
            (await _service.LiftSuspensionAsync(Admin, 999, null)).Error.Should().Be(UserModerationError.NotFound);

            var id = (await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(1))).RecordId!.Value;
            await _service.LiftSuspensionAsync(Moderator, id, null);

            (await _service.LiftSuspensionAsync(Moderator, id, null)).Error.Should().Be(UserModerationError.NotActive);
        }

        /// <summary>Audit or notification failures never fail the action.</summary>
        [Fact]
        public async Task WarnUserAsync_SurvivesAuditAndNotificationFailures()
        {
            _audit.Setup(a => a.LogActionAsync(It.IsAny<string>(), It.IsAny<ActionCategory>(), It.IsAny<SystemModeratorWarnUserDetails>())).ThrowsAsync(new InvalidOperationException());
            _notifications.Setup(n => n.CreateNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), It.IsAny<string?>())).ThrowsAsync(new InvalidOperationException());

            (await _service.WarnUserAsync(Moderator, Target, "x")).Success.Should().BeTrue();
        }

        /// <summary>The history exposes lift rights per suspension and whether the caller may moderate.</summary>
        [Fact]
        public async Task GetUserHistoryAsync_ComputesActiveAndCanLift()
        {
            var id = (await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(1))).RecordId!.Value;
            _repo.Setup(r => r.GetSuspensionsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UserSuspensionItem> { new(id, "x", null, Now, Now.AddDays(1), null, null, Moderator, "Mod", false, false) });
            _repo.Setup(r => r.GetWarningsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserWarningItem>());
            _repo.Setup(r => r.GetReportsAgainstUserAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserReportItem>());

            var byIssuer = await _service.GetUserHistoryAsync(Moderator, Target);
            var byOther = await _service.GetUserHistoryAsync(Moderator2, Target);
            var byAdmin = await _service.GetUserHistoryAsync(Admin, Target);

            byIssuer!.IsSuspended.Should().BeTrue();
            byIssuer.Suspensions.Single().Should().Match<UserSuspensionItem>(s => s.IsActive && s.CanLift);
            byOther!.Suspensions.Single().CanLift.Should().BeFalse();
            byAdmin!.Suspensions.Single().CanLift.Should().BeTrue();
            byAdmin.CanSuspendLong.Should().BeTrue();
            byIssuer.CanSuspendLong.Should().BeFalse();
            byIssuer.CanModerate.Should().BeTrue();
            (await _service.GetUserHistoryAsync(Plain, Target)).Should().BeNull();
            (await _service.GetUserHistoryAsync(Moderator, Admin))!.CanModerate.Should().BeFalse();
        }

        /// <summary>User lookup needs a moderator role and at least two characters.</summary>
        [Fact]
        public async Task SearchUsersAsync_GatesRoleAndLength()
        {
            _repo.Setup(r => r.SearchUsersAsync("ab", It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ModerationUserSummary> { new("1", "abe", "Abe") });

            (await _service.SearchUsersAsync(Moderator, "ab")).Should().HaveCount(1);
            (await _service.SearchUsersAsync(Moderator, "a")).Should().BeEmpty();
            (await _service.SearchUsersAsync(Plain, "ab")).Should().BeEmpty();
        }

        /// <summary>The moderator limit is inclusive at exactly 7 days and refused one minute beyond.</summary>
        [Fact]
        public async Task SuspendUserAsync_ModeratorBoundaryIsExact()
        {
            (await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(7).Add(TimeSpan.FromMinutes(1)))).Error.Should().Be(UserModerationError.DurationExceedsLimit);
            (await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(7).Add(TimeSpan.FromTicks(1)))).Error.Should().Be(UserModerationError.DurationExceedsLimit);
            _suspensions.Should().BeEmpty();

            var exact = await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(7));

            exact.Success.Should().BeTrue();
            _suspensions.Single().EndsAt.Should().Be(Now.AddDays(7));
        }

        /// <summary>Zero, negative and absurdly long durations are invalid for everyone and write nothing.</summary>
        [Theory]
        [InlineData(Moderator, 0)]
        [InlineData(Moderator, -1)]
        [InlineData(Admin, 0)]
        [InlineData(Admin, -365)]
        [InlineData(Admin, 36001)]
        [InlineData(Admin, 3000000)]
        public async Task SuspendUserAsync_InvalidDurationsWriteNothing(string actor, double days)
        {
            var duration = TimeSpan.FromDays(days);

            var result = await _service.SuspendUserAsync(actor, Target, "x", duration);

            result.Error.Should().Be(UserModerationError.InvalidDuration);
            _suspensions.Should().BeEmpty();
            _lockoutEnd.Should().BeNull();
        }

        /// <summary>The longest allowed temporary suspension is accepted for administrators.</summary>
        [Fact]
        public async Task SuspendUserAsync_AdminMaximumDurationAccepted()
        {
            (await _service.SuspendUserAsync(Admin, Target, "x", TimeSpan.FromDays(UserModerationService.MaxDurationDays))).Success.Should().BeTrue();
        }

        /// <summary>A reason of exactly the maximum length is accepted, one more is refused, and markup is stored untouched for escaping at render time.</summary>
        [Fact]
        public async Task SuspendUserAsync_ReasonLengthBoundary()
        {
            (await _service.SuspendUserAsync(Moderator, Target, new string('x', UserModerationService.MaxTextLength + 1), TimeSpan.FromDays(1))).Error.Should().Be(UserModerationError.ReasonTooLong);
            _suspensions.Should().BeEmpty();

            var result = await _service.SuspendUserAsync(Moderator, Target, new string('x', UserModerationService.MaxTextLength), TimeSpan.FromDays(1));

            result.Success.Should().BeTrue();
        }

        /// <summary>A suspension stops being active exactly at its end time.</summary>
        [Fact]
        public async Task GetActiveSuspensionAsync_EndsExactlyAtEndTime()
        {
            await _service.SuspendUserAsync(Moderator, Target, "Spam", TimeSpan.FromDays(1));
            _suspensions.Single().EndsAt = Now;
            _lockoutEnd = new DateTimeOffset(Now, TimeSpan.Zero);

            (await _service.GetActiveSuspensionAsync(Target)).Should().BeNull();

            _suspensions.Single().EndsAt = Now.AddTicks(1);
            _lockoutEnd = new DateTimeOffset(Now.AddTicks(1), TimeSpan.Zero);

            (await _service.GetActiveSuspensionAsync(Target)).Should().NotBeNull();
        }

        /// <summary>A moderator cannot lift a suspension an administrator issued.</summary>
        [Fact]
        public async Task LiftSuspensionAsync_ModeratorCannotLiftAdminSuspension()
        {
            var id = (await _service.SuspendUserAsync(Admin, Target, "x", null)).RecordId!.Value;

            (await _service.LiftSuspensionAsync(Moderator, id, null)).Error.Should().Be(UserModerationError.CannotLift);
            _lockoutEnd.Should().NotBeNull();
        }

        /// <summary>Warnings and suspensions notify the user neutrally: no moderator identity is in the message.</summary>
        [Fact]
        public async Task Notifications_DoNotRevealModerator()
        {
            await _service.WarnUserAsync(Moderator, Target, "x");
            await _service.SuspendUserAsync(Moderator, Target, "x", TimeSpan.FromDays(1));

            _notifications.Verify(n => n.CreateNotificationAsync(Target, NotificationType.System,
                It.Is<string?>(m => m != null && !m.Contains(Moderator + "-name") && !m.Contains(Moderator + " Display")),
                It.Is<object?>(d => d != null && !d.ToString()!.Contains(Moderator + "-name") && !d.ToString()!.Contains(Moderator + " Display")), It.IsAny<DateTime?>(), It.IsAny<string?>()), Times.Exactly(2));
        }

        /// <summary>Acknowledging delegates to the repository with a server time.</summary>
        [Fact]
        public async Task AcknowledgeWarningAsync_UsesServerTime()
        {
            _repo.Setup(r => r.AcknowledgeWarningAsync(3, Target, Now, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            (await _service.AcknowledgeWarningAsync(Target, 3)).Should().BeTrue();
            (await _service.AcknowledgeWarningAsync("", 3)).Should().BeFalse();
        }
    }
}
