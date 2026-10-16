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
    /// Unit tests for <see cref="SystemGroupReportService"/>.
    /// </summary>
    public class SystemGroupReportServiceTests
    {
        private const string Moderator = "mod";
        private const string Admin = "admin";
        private const string Plain = "plain";
        private const string GroupAdmin = "groupadmin";
        private const string Reporter = "reporter";
        private const string Reporter2 = "reporter2";
        private const string Author = "author";

        private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        private readonly Mock<IGroupReportModerationRepository> _repo = new();
        private readonly Mock<IRoleService> _roles = new();
        private readonly Mock<IAuditLogRepository> _audit = new();
        private readonly Mock<IGroupModerationLogService> _modLog = new();
        private readonly Mock<IGroupContentRemoval> _removal = new();
        private readonly Mock<NotificationService> _notifications;
        private readonly SystemGroupReportService _service;

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTimeProvider(DateTime utc) => _now = new DateTimeOffset(utc, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => _now;
        }

        /// <summary>
        /// Creates the service under test with mocked dependencies, a moderator and an administrator.
        /// </summary>
        public SystemGroupReportServiceTests()
        {
            _notifications = new Mock<NotificationService>(
                Mock.Of<INotificationRepository>(),
                Mock.Of<IDbContextFactory<ApplicationDbContext>>(),
                NullLogger<NotificationService>.Instance,
                Mock.Of<IServiceScopeFactory>(),
                Mock.Of<IAuditLogRepository>(),
                null);
            _notifications
                .Setup(n => n.CreateNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
                .ReturnsAsync((true, null, null));

            _roles.Setup(r => r.IsUserInRoleAsync(Moderator, "SystemModerator")).ReturnsAsync(true);
            _roles.Setup(r => r.IsUserInRoleAsync(Admin, "SystemAdministrator")).ReturnsAsync(true);

            _removal.Setup(r => r.RemovePostAsModeratorAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((true, (string?)null));
            _removal.Setup(r => r.RemoveCommentAsModeratorAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((true, (string?)null));

            _repo.Setup(r => r.GetContentSnapshotAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int? postId, int? commentId, CancellationToken _) =>
                    new GroupReportContentSnapshot(2, "Beta", postId ?? 1, commentId, Author, "Author Name", "<p>Bad content</p>", Now.AddDays(-9)));
            _repo.Setup(r => r.TryTransitionAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<ReportStatus>>(), It.IsAny<ReportStatus>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, IReadOnlyCollection<ReportStatus> _, ReportStatus status, string? _, DateTime? _, string? _, CancellationToken _) =>
                    Report(id, status: status));
            _repo.Setup(r => r.GetPendingReportsAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GroupPendingReportRef>
                {
                    new(10, Reporter, ReportStatus.NotReviewed, null, null, null, Author, 1, null),
                    new(11, Reporter2, ReportStatus.NotReviewed, null, null, null, Author, 1, null)
                });

            _service = new SystemGroupReportService(
                _repo.Object, _roles.Object, _audit.Object, _modLog.Object, _notifications.Object, _removal.Object,
                new HtmlSanitizationService(),
                Mock.Of<IOptionsMonitor<ModerationSettings>>(o => o.CurrentValue == new ModerationSettings { StaleReportDays = 3 }),
                new FixedTimeProvider(Now),
                NullLogger<SystemGroupReportService>.Instance);
        }

        private static GroupContentReport Report(int id = 10, ReportStatus status = ReportStatus.NotReviewed, int? postId = 1, int? commentId = null, string reporter = Reporter, DateTime? createdAt = null)
            => new()
            {
                Id = id,
                GroupId = 2,
                PostId = commentId == null ? postId : null,
                CommentId = commentId,
                ReporterId = reporter,
                Reason = ReportReason.Spam,
                Status = status,
                CreatedAt = createdAt ?? Now.AddDays(-1)
            };

        private void ReportExists(GroupContentReport report)
            => _repo.Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        /// <summary>Moderators and administrators see the overview; others get null.</summary>
        [Theory]
        [InlineData(Moderator, true)]
        [InlineData(Admin, true)]
        [InlineData(Plain, false)]
        [InlineData(GroupAdmin, false)]
        public async Task GetOverviewAsync_RoleGate(string userId, bool allowed)
        {
            _repo.Setup(r => r.GetOverviewCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync((4, 2, 7, 3));

            var overview = await _service.GetOverviewAsync(userId);

            if (allowed)
            {
                overview.Should().Be(new ModerationOverview(4, 2, 7, 3, 3));
            }
            else
            {
                overview.Should().BeNull();
            }
        }

        /// <summary>Stale and closed thresholds come from the clock and the configured days.</summary>
        [Fact]
        public async Task GetOverviewAsync_UsesConfiguredThresholds()
        {
            _repo.Setup(r => r.GetOverviewCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync((0, 0, 0, 0));

            await _service.GetOverviewAsync(Moderator);

            _repo.Verify(r => r.GetOverviewCountsAsync(Now.AddDays(-3), Now.AddDays(-7), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>The queue never reaches the repository for users without a system role.</summary>
        [Fact]
        public async Task GetQueueAsync_PlainUser_ReturnsEmpty()
        {
            var page = await _service.GetQueueAsync(Plain, new GroupReportQueueFilter(), 1, 20);

            page.Items.Should().BeEmpty();
            _repo.Verify(r => r.GetQueueAsync(It.IsAny<GroupReportQueueFilter>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>A report exactly at the stale boundary is not stale; one just older is.</summary>
        [Fact]
        public async Task GetQueueAsync_StaleBoundary()
        {
            var boundary = Now.AddDays(-3);
            _repo.Setup(r => r.GetQueueAsync(It.IsAny<GroupReportQueueFilter>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<GroupReportQueueRow>
                {
                    Row(1, boundary),
                    Row(2, boundary.AddSeconds(-1))
                }, 2));

            var page = await _service.GetQueueAsync(Moderator, new GroupReportQueueFilter(), 1, 20);

            page.Items.Single(i => i.Id == 1).IsStale.Should().BeFalse();
            page.Items.Single(i => i.Id == 2).IsStale.Should().BeTrue();
            page.Items.Select(i => i.GroupName).Should().AllBe("Beta");
        }

        private static GroupReportQueueRow Row(int id, DateTime created)
            => new(id, 2, "Beta", id, null, ReportReason.Spam, ReportStatus.NotReviewed, created, Author, "Author", Reporter, "Reporter", "<p>x</p>", 0);

        /// <summary>Dismiss closes the report, closes other pending ones, writes both logs and notifies each reporter.</summary>
        [Theory]
        [InlineData(Moderator)]
        [InlineData(Admin)]
        public async Task DismissAsync_ClosesLogsAndNotifies(string actor)
        {
            ReportExists(Report());

            var result = await _service.DismissAsync(actor, 10, "not a violation");

            result.Success.Should().BeTrue();
            _audit.Verify(a => a.LogActionAsync(actor, ActionCategory.SystemModeratorReviewReport, It.IsAny<SystemModeratorReviewReportDetails>()), Times.Exactly(2));
            _modLog.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r => r.IsSystemAction && r.GroupId == 2 && r.Action == GroupModerationAction.ReportDismissed && r.ActorUserId == actor), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _notifications.Verify(n => n.CreateNotificationAsync(Reporter, NotificationType.System, It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), FeedContentReportService.ReviewedTemplateKey), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Reporter2, NotificationType.System, It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), FeedContentReportService.ReviewedTemplateKey), Times.Once);
        }

        /// <summary>The review audit entry carries the group name so the activity log can show it.</summary>
        [Fact]
        public async Task DismissAsync_ReviewAudit_IncludesGroupName()
        {
            ReportExists(Report());

            await _service.DismissAsync(Moderator, 10, null);

            _audit.Verify(a => a.LogActionAsync(Moderator, ActionCategory.SystemModeratorReviewReport, It.Is<SystemModeratorReviewReportDetails>(d => !string.IsNullOrEmpty(d.GroupName))), Times.AtLeastOnce);
        }

        /// <summary>Resolve logs a resolved action in the group log.</summary>
        [Fact]
        public async Task ResolveAsync_LogsResolvedInGroupLog()
        {
            ReportExists(Report());

            var result = await _service.ResolveAsync(Moderator, 10, null);

            result.Success.Should().BeTrue();
            _modLog.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r => r.Action == GroupModerationAction.ReportResolved && r.IsSystemAction), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        /// <summary>Plain members and group administrators cannot act and nothing is written.</summary>
        [Theory]
        [InlineData(Plain)]
        [InlineData(GroupAdmin)]
        public async Task Actions_NonSystemUser_AreRefused(string actor)
        {
            ReportExists(Report());

            (await _service.DismissAsync(actor, 10, null)).Error.Should().Be(FeedReportError.NotAuthorized);
            (await _service.ResolveAsync(actor, 10, null)).Error.Should().Be(FeedReportError.NotAuthorized);
            (await _service.RemoveContentAsync(actor, 10, null)).Error.Should().Be(FeedReportError.NotAuthorized);
            (await _service.MarkUnderReviewAsync(actor, 10)).Error.Should().Be(FeedReportError.NotAuthorized);

            _repo.Verify(r => r.TryTransitionAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<ReportStatus>>(), It.IsAny<ReportStatus>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            _removal.Verify(r => r.RemovePostAsModeratorAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>A moderator cannot act on their own report or their own content.</summary>
        [Fact]
        public async Task Actions_OwnReportOrContent_AreRefused()
        {
            ReportExists(Report(10, reporter: Moderator));
            (await _service.DismissAsync(Moderator, 10, null)).Error.Should().Be(FeedReportError.OwnReport);

            ReportExists(Report(12));
            _repo.Setup(r => r.GetContentSnapshotAsync(1, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GroupReportContentSnapshot(2, "Beta", 1, null, Moderator, "Mod", "<p>x</p>", Now));
            (await _service.RemoveContentAsync(Moderator, 12, null)).Error.Should().Be(FeedReportError.OwnContent);
        }

        /// <summary>An already-closed report fails safely and a lost race reports AlreadyReviewed without side effects.</summary>
        [Fact]
        public async Task Dismiss_AlreadyClosedOrRaced_FailsSafely()
        {
            ReportExists(Report(10, status: ReportStatus.Dismissed));
            (await _service.DismissAsync(Moderator, 10, null)).Error.Should().Be(FeedReportError.AlreadyReviewed);

            ReportExists(Report(13));
            _repo.Setup(r => r.TryTransitionAsync(13, It.IsAny<IReadOnlyCollection<ReportStatus>>(), It.IsAny<ReportStatus>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GroupContentReport?)null);
            (await _service.DismissAsync(Moderator, 13, null)).Error.Should().Be(FeedReportError.AlreadyReviewed);

            _modLog.Verify(m => m.LogAsync(It.IsAny<GroupModerationLogRequest>(), It.IsAny<CancellationToken>()), Times.Never);
            _notifications.Verify(n => n.CreateNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()), Times.Never);
        }

        /// <summary>Removing a post deletes it, audits, logs a system action in the group log and notifies reporters and the author.</summary>
        [Fact]
        public async Task RemoveContentAsync_Post_RemovesLogsAndNotifies()
        {
            ReportExists(Report());

            var result = await _service.RemoveContentAsync(Moderator, 10, "spam");

            result.Success.Should().BeTrue();
            _removal.Verify(r => r.RemovePostAsModeratorAsync(1, Moderator), Times.Once);
            _audit.Verify(a => a.LogActionAsync(Moderator, ActionCategory.SystemModeratorRemovePost, It.IsAny<SystemModeratorRemovePostDetails>()), Times.Once);
            _modLog.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r => r.Action == GroupModerationAction.PostRemoved && r.IsSystemAction && r.GroupId == 2), It.IsAny<CancellationToken>()), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Reporter, NotificationType.System, It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), FeedContentReportService.ReviewedTemplateKey), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Author, NotificationType.System, It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), SystemModerationService.ContentRemovedTemplateKey), Times.Once);
        }

        /// <summary>Removing a comment uses the comment removal and the comment audit category.</summary>
        [Fact]
        public async Task RemoveContentAsync_Comment_RemovesComment()
        {
            ReportExists(Report(10, postId: null, commentId: 5));

            var result = await _service.RemoveContentAsync(Admin, 10, null);

            result.Success.Should().BeTrue();
            _removal.Verify(r => r.RemoveCommentAsModeratorAsync(5, Admin), Times.Once);
            _audit.Verify(a => a.LogActionAsync(Admin, ActionCategory.SystemModeratorRemovePostComment, It.IsAny<SystemModeratorRemovePostCommentDetails>()), Times.Once);
            _modLog.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r => r.Action == GroupModerationAction.CommentRemoved && r.IsSystemAction), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>When the removal fails the report statuses are restored and the failure is reported.</summary>
        [Fact]
        public async Task RemoveContentAsync_RemovalFails_RestoresStatus()
        {
            ReportExists(Report());
            _removal.Setup(r => r.RemovePostAsModeratorAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((false, "nope"));

            var result = await _service.RemoveContentAsync(Moderator, 10, null);

            result.Error.Should().Be(FeedReportError.RemovalFailed);
            _repo.Verify(r => r.TryTransitionAsync(10, It.Is<IReadOnlyCollection<ReportStatus>>(s => s.Contains(ReportStatus.ActionTaken)), ReportStatus.NotReviewed, It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            _modLog.Verify(m => m.LogAsync(It.IsAny<GroupModerationLogRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>Overlong notes are rejected before anything changes.</summary>
        [Fact]
        public async Task Dismiss_NotesTooLong_Rejected()
        {
            ReportExists(Report());

            var result = await _service.DismissAsync(Moderator, 10, new string('x', FeedContentReportService.MaxTextLength + 1));

            result.Error.Should().Be(FeedReportError.DescriptionTooLong);
        }
    }
}
