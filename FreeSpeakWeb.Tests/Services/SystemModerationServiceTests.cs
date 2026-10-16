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
    /// Unit tests for <see cref="SystemModerationService"/>.
    /// </summary>
    public class SystemModerationServiceTests
    {
        private const string Moderator = "mod";
        private const string Admin = "admin";
        private const string Plain = "plain";
        private const string Reporter = "reporter";
        private const string Reporter2 = "reporter2";
        private const string Author = "author";

        private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        private readonly Mock<IFeedContentReportRepository> _repo = new();
        private readonly Mock<IRoleService> _roles = new();
        private readonly Mock<IAuditLogRepository> _audit = new();
        private readonly Mock<IFeedContentRemoval> _removal = new();
        private readonly Mock<NotificationService> _notifications;
        private readonly SystemModerationService _service;

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTimeProvider(DateTime utc) => _now = new DateTimeOffset(utc, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => _now;
        }

        /// <summary>
        /// Creates the service under test with mocked dependencies and a moderator and an administrator.
        /// </summary>
        public SystemModerationServiceTests()
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
            _roles.Setup(r => r.IsUserInRoleAsync(Admin, "SystemAdministrator")).ReturnsAsync(true);

            _removal.Setup(r => r.RemovePostAsModeratorAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((true, (string?)null));
            _removal.Setup(r => r.RemoveCommentAsModeratorAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync((true, (string?)null, 1));

            _repo.Setup(r => r.GetContentSnapshotAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int? postId, int? commentId, CancellationToken _) =>
                    new FeedReportContentSnapshot(postId ?? 1, commentId, Author, "Author Name", "<p>Bad content</p>", Now.AddDays(-9)));
            _repo.Setup(r => r.TryTransitionAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<ReportStatus>>(), It.IsAny<ReportStatus>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, IReadOnlyCollection<ReportStatus> _, ReportStatus status, string? reviewer, DateTime? at, string? notes, CancellationToken _) =>
                    Report(id, status: status));
            _repo.Setup(r => r.CloseOtherPendingAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<ReportStatus>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FeedContentReport>());
            _repo.Setup(r => r.GetPendingReportersAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<(int ReportId, string ReporterId)> { (10, Reporter), (11, Reporter2), (12, Reporter) });

            _service = new SystemModerationService(
                _repo.Object, _roles.Object, _audit.Object, _notifications.Object, _removal.Object,
                new HtmlSanitizationService(),
                Mock.Of<IOptionsMonitor<ModerationSettings>>(o => o.CurrentValue == new ModerationSettings { StaleReportDays = 3 }),
                new FixedTimeProvider(Now),
                NullLogger<SystemModerationService>.Instance);
        }

        private static FeedContentReport Report(int id = 10, ReportStatus status = ReportStatus.NotReviewed, int? postId = 1, int? commentId = null, string author = Author, string reporter = Reporter, DateTime? createdAt = null)
            => new()
            {
                Id = id,
                PostId = commentId == null ? postId : null,
                CommentId = commentId,
                ContentAuthorId = author,
                ReporterId = reporter,
                Reason = ReportReason.Spam,
                Status = status,
                CreatedAt = createdAt ?? Now.AddDays(-1)
            };

        private void ReportExists(FeedContentReport report)
            => _repo.Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        /// <summary>Both moderators and administrators see the overview; others get null.</summary>
        [Theory]
        [InlineData(Moderator, true)]
        [InlineData(Admin, true)]
        [InlineData(Plain, false)]
        [InlineData("", false)]
        public async Task GetOverviewAsync_RoleGate(string userId, bool allowed)
        {
            _repo.Setup(r => r.GetOverviewCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((4, 2, 7, 3));

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

        /// <summary>The stale and closed thresholds are derived from the clock and the configured days.</summary>
        [Fact]
        public async Task GetOverviewAsync_UsesConfiguredThresholds()
        {
            _repo.Setup(r => r.GetOverviewCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((0, 0, 0, 0));

            await _service.GetOverviewAsync(Moderator);

            _repo.Verify(r => r.GetOverviewCountsAsync(Now.AddDays(-3), Now.AddDays(-7), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>The queue is empty for users without a system moderation role and never reaches the repository.</summary>
        [Fact]
        public async Task GetFeedReportQueueAsync_PlainUser_ReturnsEmptyWithoutQuery()
        {
            var page = await _service.GetFeedReportQueueAsync(Plain, new FeedReportQueueFilter(), 1, 25);

            page.Items.Should().BeEmpty();
            page.TotalCount.Should().Be(0);
            _repo.Verify(r => r.GetQueueAsync(It.IsAny<FeedReportQueueFilter>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>Queue rows are mapped with excerpts, content type, display names and the strict stale boundary.</summary>
        [Fact]
        public async Task GetFeedReportQueueAsync_MapsRowsAndMarksStale()
        {
            var staleBefore = Now.AddDays(-3);
            var rows = new List<FeedReportQueueRow>
            {
                new(1, 5, null, ReportReason.Spam, ReportStatus.NotReviewed, staleBefore.AddSeconds(-1), Author, "A B", Reporter, null, "<p>Hello &amp; <b>bye</b></p>", 2),
                new(2, null, 9, ReportReason.HateSpeech, ReportStatus.UnderReview, staleBefore, Author, "A B", Reporter, "R R", "text", 0)
            };
            _repo.Setup(r => r.GetQueueAsync(It.IsAny<FeedReportQueueFilter>(), staleBefore, 1, 25, It.IsAny<CancellationToken>()))
                .ReturnsAsync((rows, 2));

            var page = await _service.GetFeedReportQueueAsync(Moderator, new FeedReportQueueFilter(), 1, 25);

            page.TotalCount.Should().Be(2);
            page.Items[0].IsStale.Should().BeTrue();
            page.Items[0].ContentType.Should().Be(FeedReportContentType.Post);
            page.Items[0].ContentExcerpt.Should().Be("Hello & bye");
            page.Items[0].ReporterDisplayName.Should().Be("?");
            page.Items[0].OtherPendingReports.Should().Be(2);
            page.Items[1].IsStale.Should().BeFalse();
            page.Items[1].ContentType.Should().Be(FeedReportContentType.Comment);
        }

        /// <summary>Page size is clamped to the maximum.</summary>
        [Fact]
        public async Task GetFeedReportQueueAsync_ClampsPageSize()
        {
            _repo.Setup(r => r.GetQueueAsync(It.IsAny<FeedReportQueueFilter>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<FeedReportQueueRow>(), 0));

            await _service.GetFeedReportQueueAsync(Moderator, new FeedReportQueueFilter(), 0, 5000);

            _repo.Verify(r => r.GetQueueAsync(It.IsAny<FeedReportQueueFilter>(), It.IsAny<DateTime>(), 1, 100, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>The detail sanitizes content, flags stale reports and allows action for a moderator.</summary>
        [Fact]
        public async Task GetFeedReportDetailAsync_Moderator_SanitizesAndAllowsAction()
        {
            ReportExists(Report(createdAt: Now.AddDays(-5)));
            _repo.Setup(r => r.GetContentSnapshotAsync(1, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedReportContentSnapshot(1, null, Author, "Author Name", "<p>Hi</p><script>alert(1)</script>", Now.AddDays(-9)));
            _repo.Setup(r => r.GetOtherReportsAsync(1, null, 10, It.IsAny<CancellationToken>())).ReturnsAsync(new List<FeedReportRelatedItem>());
            _repo.Setup(r => r.CountReportsAgainstAuthorAsync(Author, 10, It.IsAny<CancellationToken>())).ReturnsAsync(4);
            _repo.Setup(r => r.GetUserDisplayNameAsync(Reporter, It.IsAny<CancellationToken>())).ReturnsAsync("Rita Reporter");

            var detail = await _service.GetFeedReportDetailAsync(Moderator, 10);

            detail.Should().NotBeNull();
            detail!.ContentHtml.Should().NotContain("<script");
            detail.IsStale.Should().BeTrue();
            detail.CanAct.Should().BeTrue();
            detail.AuthorOtherReportCount.Should().Be(4);
            detail.ReporterDisplayName.Should().Be("Rita Reporter");
        }

        /// <summary>A moderator who authored the content may view but not act on its report.</summary>
        [Fact]
        public async Task GetFeedReportDetailAsync_OwnContent_CannotAct()
        {
            ReportExists(Report(author: Moderator));
            _repo.Setup(r => r.GetOtherReportsAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<FeedReportRelatedItem>());

            var detail = await _service.GetFeedReportDetailAsync(Moderator, 10);

            detail!.CanAct.Should().BeFalse();
        }

        /// <summary>The detail is refused for a plain user.</summary>
        [Fact]
        public async Task GetFeedReportDetailAsync_PlainUser_ReturnsNull()
        {
            ReportExists(Report());

            (await _service.GetFeedReportDetailAsync(Plain, 10)).Should().BeNull();
        }

        /// <summary>Plain users, group administrators and group moderators are refused every action.</summary>
        [Theory]
        [InlineData("plain")]
        [InlineData("groupadmin")]
        [InlineData("groupmod")]
        public async Task Actions_NonSystemModerator_AreRefused(string userId)
        {
            ReportExists(Report());

            (await _service.DismissAsync(userId, 10, null)).Error.Should().Be(FeedReportError.NotAuthorized);
            (await _service.ResolveAsync(userId, 10, null)).Error.Should().Be(FeedReportError.NotAuthorized);
            (await _service.RemoveContentAsync(userId, 10, null)).Error.Should().Be(FeedReportError.NotAuthorized);
            (await _service.MarkUnderReviewAsync(userId, 10)).Error.Should().Be(FeedReportError.NotAuthorized);
            _repo.Verify(r => r.TryTransitionAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<ReportStatus>>(), It.IsAny<ReportStatus>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            _removal.Verify(r => r.RemovePostAsModeratorAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>An unknown report is reported as not found.</summary>
        [Fact]
        public async Task DismissAsync_UnknownReport_ReturnsNotFound()
        {
            (await _service.DismissAsync(Moderator, 99, null)).Error.Should().Be(FeedReportError.ReportNotFound);
        }

        /// <summary>A report that is already closed cannot be processed again.</summary>
        [Theory]
        [InlineData(ReportStatus.Dismissed)]
        [InlineData(ReportStatus.Resolved)]
        [InlineData(ReportStatus.ActionTaken)]
        public async Task DismissAsync_AlreadyClosed_ReturnsAlreadyReviewed(ReportStatus status)
        {
            ReportExists(Report(status: status));

            (await _service.DismissAsync(Moderator, 10, null)).Error.Should().Be(FeedReportError.AlreadyReviewed);
        }

        /// <summary>When another moderator wins the race the atomic transition yields AlreadyReviewed.</summary>
        [Fact]
        public async Task DismissAsync_LostRace_ReturnsAlreadyReviewed()
        {
            ReportExists(Report());
            _repo.Setup(r => r.TryTransitionAsync(10, It.IsAny<IReadOnlyCollection<ReportStatus>>(), ReportStatus.Dismissed, It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FeedContentReport?)null);

            (await _service.DismissAsync(Moderator, 10, null)).Error.Should().Be(FeedReportError.AlreadyReviewed);
            _audit.Verify(a => a.LogActionAsync(It.IsAny<string>(), It.IsAny<ActionCategory>(), It.IsAny<It.IsAnyType>()), Times.Never);
        }

        /// <summary>Moderators cannot act on reports about their own content or reports they filed.</summary>
        [Fact]
        public async Task Actions_OwnContentOrOwnReport_AreRefused()
        {
            ReportExists(Report(id: 10, author: Moderator));
            ReportExists(Report(id: 11, reporter: Moderator));

            (await _service.DismissAsync(Moderator, 10, null)).Error.Should().Be(FeedReportError.OwnContent);
            (await _service.RemoveContentAsync(Moderator, 11, null)).Error.Should().Be(FeedReportError.OwnReport);
        }

        /// <summary>Notes longer than the limit are rejected.</summary>
        [Fact]
        public async Task DismissAsync_NotesTooLong_ReturnsDescriptionTooLong()
        {
            ReportExists(Report());

            (await _service.DismissAsync(Moderator, 10, new string('x', 2001))).Error.Should().Be(FeedReportError.DescriptionTooLong);
        }

        /// <summary>Dismiss by an administrator closes related reports, audits each and notifies each reporter.</summary>
        [Fact]
        public async Task DismissAsync_Administrator_ClosesRelatedAuditsAndNotifies()
        {
            ReportExists(Report());
            _repo.Setup(r => r.CloseOtherPendingAsync(1, null, 10, ReportStatus.Dismissed, Admin, Now, "ok", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FeedContentReport> { Report(id: 11, status: ReportStatus.Dismissed, reporter: Reporter2) });

            var result = await _service.DismissAsync(Admin, 10, "  ok  ");

            result.Success.Should().BeTrue();
            _repo.Verify(r => r.TryTransitionAsync(10, It.IsAny<IReadOnlyCollection<ReportStatus>>(), ReportStatus.Dismissed, Admin, Now, "ok", It.IsAny<CancellationToken>()), Times.Once);
            _audit.Verify(a => a.LogActionAsync(Admin, ActionCategory.SystemModeratorReviewReport, It.IsAny<SystemModeratorReviewReportDetails>()), Times.Exactly(2));
            _notifications.Verify(n => n.CreateNotificationAsync(Reporter, NotificationType.System, It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), FeedContentReportService.ReviewedTemplateKey), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Reporter2, NotificationType.System, It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), FeedContentReportService.ReviewedTemplateKey), Times.Once);
        }

        /// <summary>Resolve records the Resolved status.</summary>
        [Fact]
        public async Task ResolveAsync_Moderator_TransitionsToResolved()
        {
            ReportExists(Report());

            var result = await _service.ResolveAsync(Moderator, 10, null);

            result.Success.Should().BeTrue();
            _repo.Verify(r => r.TryTransitionAsync(10, It.IsAny<IReadOnlyCollection<ReportStatus>>(), ReportStatus.Resolved, Moderator, Now, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>Marking under review only transitions a not-reviewed report.</summary>
        [Fact]
        public async Task MarkUnderReviewAsync_Moderator_TransitionsFromNotReviewed()
        {
            ReportExists(Report());

            var result = await _service.MarkUnderReviewAsync(Moderator, 10);

            result.Success.Should().BeTrue();
            _repo.Verify(r => r.TryTransitionAsync(10, It.Is<IReadOnlyCollection<ReportStatus>>(a => a.Count == 1 && a.Contains(ReportStatus.NotReviewed)), ReportStatus.UnderReview, Moderator, Now, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>Removing a post audits category 26 and notifies each reporter once plus the author neutrally.</summary>
        [Fact]
        public async Task RemoveContentAsync_Post_RemovesAuditsAndNotifies()
        {
            ReportExists(Report());

            var result = await _service.RemoveContentAsync(Moderator, 10, "violates policy");

            result.Success.Should().BeTrue();
            _removal.Verify(r => r.RemovePostAsModeratorAsync(1, Moderator), Times.Once);
            _audit.Verify(a => a.LogActionAsync(Moderator, ActionCategory.SystemModeratorRemovePost,
                It.Is<SystemModeratorRemovePostDetails>(d => d.PostId == 1 && d.PostAuthorId == Author && d.ModeratorNotes == "violates policy" && d.ViolationReason == nameof(ReportReason.Spam))), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Reporter, It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), FeedContentReportService.ReviewedTemplateKey), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Reporter2, It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), FeedContentReportService.ReviewedTemplateKey), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Author, It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), SystemModerationService.ContentRemovedTemplateKey), Times.Once);
        }

        /// <summary>Removing a comment audits category 27 through the comment removal path.</summary>
        [Fact]
        public async Task RemoveContentAsync_Comment_UsesCommentRemovalAndCategory27()
        {
            ReportExists(Report(postId: null, commentId: 5));

            var result = await _service.RemoveContentAsync(Admin, 10, null);

            result.Success.Should().BeTrue();
            _removal.Verify(r => r.RemoveCommentAsModeratorAsync(5, Admin), Times.Once);
            _removal.Verify(r => r.RemovePostAsModeratorAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            _audit.Verify(a => a.LogActionAsync(Admin, ActionCategory.SystemModeratorRemovePostComment, It.Is<SystemModeratorRemovePostCommentDetails>(d => d.CommentId == 5)), Times.Once);
        }

        /// <summary>If deletion fails the report is returned to its previous status and nothing is audited or sent.</summary>
        [Fact]
        public async Task RemoveContentAsync_RemovalFails_RevertsAndDoesNotNotify()
        {
            ReportExists(Report(status: ReportStatus.UnderReview));
            _removal.Setup(r => r.RemovePostAsModeratorAsync(1, Moderator)).ReturnsAsync((false, "boom"));

            var result = await _service.RemoveContentAsync(Moderator, 10, null);

            result.Error.Should().Be(FeedReportError.RemovalFailed);
            _repo.Verify(r => r.TryTransitionAsync(10, It.Is<IReadOnlyCollection<ReportStatus>>(a => a.Contains(ReportStatus.ActionTaken)), ReportStatus.UnderReview, It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            _audit.Verify(a => a.LogActionAsync(It.IsAny<string>(), ActionCategory.SystemModeratorRemovePost, It.IsAny<SystemModeratorRemovePostDetails>()), Times.Never);
            _notifications.Verify(n => n.CreateNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()), Times.Never);
        }

        /// <summary>Content that no longer exists cannot be removed.</summary>
        [Fact]
        public async Task RemoveContentAsync_ContentMissing_ReturnsContentNotFound()
        {
            ReportExists(Report());
            _repo.Setup(r => r.GetContentSnapshotAsync(1, null, It.IsAny<CancellationToken>())).ReturnsAsync((FeedReportContentSnapshot?)null);

            (await _service.RemoveContentAsync(Moderator, 10, null)).Error.Should().Be(FeedReportError.ContentNotFound);
        }
    }
}
