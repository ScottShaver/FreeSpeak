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
    /// Unit tests for <see cref="FeedContentReportService"/>.
    /// </summary>
    public class FeedContentReportServiceTests
    {
        private const string Reporter = "reporter";
        private const string Author = "author";
        private const string Moderator = "mod";

        private readonly Mock<IFeedContentReportRepository> _repo = new();
        private readonly Mock<IFriendshipRepository> _friends = new();
        private readonly Mock<IRoleService> _roles = new();
        private readonly Mock<IAuditLogRepository> _audit = new();
        private readonly Mock<NotificationService> _notifications;
        private readonly FeedContentReportService _service;

        /// <summary>
        /// Creates the service under test with mocked dependencies and a public post by <c>author</c>.
        /// </summary>
        public FeedContentReportServiceTests()
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

            _repo.Setup(r => r.GetPostTargetAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedReportTarget(1, null, Author, Author, null, AudienceType.Public));
            _repo.Setup(r => r.GetCommentTargetAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedReportTarget(1, 5, Author, Author, null, AudienceType.Public));
            _repo.Setup(r => r.AddAsync(It.IsAny<FeedContentReport>(), It.IsAny<CancellationToken>()))
                .Callback<FeedContentReport, CancellationToken>((r, _) => r.Id = 42)
                .ReturnsAsync(true);
            _roles.Setup(r => r.IsUserInRoleAsync(Moderator, "SystemModerator")).ReturnsAsync(true);

            _service = new FeedContentReportService(
                _repo.Object, _friends.Object, _roles.Object, _audit.Object, _notifications.Object,
                Options.Create(new FeedReportSettings { MaxReportsPerUserPerHour = 5 }),
                TimeProvider.System, NullLogger<FeedContentReportService>.Instance);
        }

        /// <summary>A valid post report is stored as NotReviewed and audited.</summary>
        [Fact]
        public async Task ReportPostAsync_ValidReport_StoresAndAudits()
        {
            var result = await _service.ReportPostAsync(1, Reporter, ReportReason.Spam, "  context  ");

            result.Success.Should().BeTrue();
            result.ReportId.Should().Be(42);
            _repo.Verify(r => r.AddAsync(It.Is<FeedContentReport>(x =>
                x.PostId == 1 && x.CommentId == null && x.ContentAuthorId == Author && x.ReporterId == Reporter
                && x.Status == ReportStatus.NotReviewed && x.Description == "context"), It.IsAny<CancellationToken>()), Times.Once);
            _audit.Verify(a => a.LogActionAsync(Reporter, ActionCategory.UserReportContent, It.IsAny<UserReportContentDetails>()), Times.Once);
        }

        /// <summary>A valid comment report targets the comment.</summary>
        [Fact]
        public async Task ReportCommentAsync_ValidReport_StoresCommentTarget()
        {
            var result = await _service.ReportCommentAsync(5, Reporter, ReportReason.HarassmentOrBullying, null);

            result.Success.Should().BeTrue();
            _repo.Verify(r => r.AddAsync(It.Is<FeedContentReport>(x => x.CommentId == 5 && x.PostId == null), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>Group-specific reasons are rejected.</summary>
        [Theory]
        [InlineData(ReportReason.ViolatesGroupRule)]
        [InlineData(ReportReason.OffTopic)]
        [InlineData((ReportReason)999)]
        public async Task ReportPostAsync_InvalidReason_Fails(ReportReason reason)
        {
            var result = await _service.ReportPostAsync(1, Reporter, reason, null);

            result.Error.Should().Be(FeedReportError.InvalidReason);
            _repo.Verify(r => r.AddAsync(It.IsAny<FeedContentReport>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>Descriptions over 2000 characters are rejected.</summary>
        [Fact]
        public async Task ReportPostAsync_DescriptionTooLong_Fails()
        {
            var result = await _service.ReportPostAsync(1, Reporter, ReportReason.Spam, new string('x', 2001));

            result.Error.Should().Be(FeedReportError.DescriptionTooLong);
        }

        /// <summary>Reporting one's own content is blocked.</summary>
        [Fact]
        public async Task ReportPostAsync_OwnContent_Fails()
        {
            var result = await _service.ReportPostAsync(1, Author, ReportReason.Spam, null);

            result.Error.Should().Be(FeedReportError.OwnContent);
        }

        /// <summary>Reporting one's own comment on someone else's post is blocked.</summary>
        [Fact]
        public async Task ReportCommentAsync_OwnComment_Fails()
        {
            _repo.Setup(r => r.GetCommentTargetAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedReportTarget(1, 6, Reporter, Author, null, AudienceType.Public));

            var result = await _service.ReportCommentAsync(6, Reporter, ReportReason.Spam, null);

            result.Error.Should().Be(FeedReportError.OwnContent);
        }

        /// <summary>Missing content yields ContentNotFound.</summary>
        [Fact]
        public async Task ReportPostAsync_MissingPost_ContentNotFound()
        {
            var result = await _service.ReportPostAsync(99, Reporter, ReportReason.Spam, null);

            result.Error.Should().Be(FeedReportError.ContentNotFound);
        }

        /// <summary>Content the reporter cannot see is reported as not found.</summary>
        [Theory]
        [InlineData(AudienceType.MeOnly)]
        [InlineData(AudienceType.FriendsOnly)]
        public async Task ReportPostAsync_InvisibleContent_ContentNotFound(AudienceType audience)
        {
            _repo.Setup(r => r.GetPostTargetAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedReportTarget(2, null, Author, Author, null, audience));
            _friends.Setup(f => f.AreFriendsAsync(Author, Reporter)).ReturnsAsync(false);

            var result = await _service.ReportPostAsync(2, Reporter, ReportReason.Spam, null);

            result.Error.Should().Be(FeedReportError.ContentNotFound);
        }

        /// <summary>A friend can report a friends-only post.</summary>
        [Fact]
        public async Task ReportPostAsync_FriendsOnlyAsFriend_Succeeds()
        {
            _repo.Setup(r => r.GetPostTargetAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedReportTarget(2, null, Author, Author, null, AudienceType.FriendsOnly));
            _friends.Setup(f => f.AreFriendsAsync(Author, Reporter)).ReturnsAsync(true);

            var result = await _service.ReportPostAsync(2, Reporter, ReportReason.Spam, null);

            result.Success.Should().BeTrue();
        }

        /// <summary>A second report of the same content is rejected.</summary>
        [Fact]
        public async Task ReportPostAsync_AlreadyReported_Duplicate()
        {
            _repo.Setup(r => r.HasReportedAsync(Reporter, 1, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await _service.ReportPostAsync(1, Reporter, ReportReason.Spam, null);

            result.Error.Should().Be(FeedReportError.DuplicateReport);
        }

        /// <summary>A concurrent duplicate caught by the unique index maps to DuplicateReport.</summary>
        [Fact]
        public async Task ReportPostAsync_RepositoryReportsDuplicate_Duplicate()
        {
            _repo.Setup(r => r.AddAsync(It.IsAny<FeedContentReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var result = await _service.ReportPostAsync(1, Reporter, ReportReason.Spam, null);

            result.Error.Should().Be(FeedReportError.DuplicateReport);
        }

        /// <summary>The sixth report within an hour is rejected; the fifth is allowed.</summary>
        [Theory]
        [InlineData(4, true)]
        [InlineData(5, false)]
        public async Task ReportPostAsync_RateLimit_EnforcedAtFive(int recent, bool allowed)
        {
            _repo.Setup(r => r.CountReportsSinceAsync(Reporter, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(recent);

            var result = await _service.ReportPostAsync(1, Reporter, ReportReason.Spam, null);

            result.Success.Should().Be(allowed);
            if (!allowed)
            {
                result.Error.Should().Be(FeedReportError.RateLimited);
            }
        }

        /// <summary>A repository exception becomes a generic failure instead of propagating.</summary>
        [Fact]
        public async Task ReportPostAsync_RepositoryThrows_ReturnsFailed()
        {
            _repo.Setup(r => r.GetPostTargetAsync(1, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());

            var result = await _service.ReportPostAsync(1, Reporter, ReportReason.Spam, null);

            result.Error.Should().Be(FeedReportError.Failed);
        }

        /// <summary>Non-moderators cannot read the queue or its count.</summary>
        [Fact]
        public async Task QueueReads_NonModerator_ReturnEmpty()
        {
            (await _service.GetPendingReportsAsync(Reporter, 1, 10)).Items.Should().BeEmpty();
            (await _service.GetPendingReportCountAsync(Reporter)).Should().Be(0);
            (await _service.GetReportByIdAsync(Reporter, 1)).Should().BeNull();
            _repo.Verify(r => r.GetPendingPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>Moderators can read the count.</summary>
        [Fact]
        public async Task GetPendingReportCountAsync_Moderator_ReturnsCount()
        {
            _repo.Setup(r => r.GetPendingCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(7);

            (await _service.GetPendingReportCountAsync(Moderator)).Should().Be(7);
        }

        /// <summary>Non-moderators cannot decide reports.</summary>
        [Fact]
        public async Task DismissReportAsync_NonModerator_NotAuthorized()
        {
            var result = await _service.DismissReportAsync(Reporter, 42);

            result.Error.Should().Be(FeedReportError.NotAuthorized);
        }

        /// <summary>Each decision updates the report, audits it and notifies only the reporter.</summary>
        [Theory]
        [InlineData(ReportStatus.Dismissed)]
        [InlineData(ReportStatus.ActionTaken)]
        [InlineData(ReportStatus.Resolved)]
        public async Task ReviewDecision_UpdatesAuditsAndNotifiesReporter(ReportStatus status)
        {
            var report = new FeedContentReport { Id = 42, PostId = 1, ReporterId = Reporter, ContentAuthorId = Author, Status = ReportStatus.NotReviewed };
            _repo.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(report);
            _repo.Setup(r => r.UpdateReviewAsync(42, status, Moderator, It.IsAny<DateTime>(), "n", It.IsAny<CancellationToken>())).ReturnsAsync(report);

            var result = status switch
            {
                ReportStatus.Dismissed => await _service.DismissReportAsync(Moderator, 42, "n"),
                ReportStatus.ActionTaken => await _service.MarkActionTakenAsync(Moderator, 42, "n"),
                _ => await _service.ResolveReportAsync(Moderator, 42, "n")
            };

            result.Success.Should().BeTrue();
            _audit.Verify(a => a.LogActionAsync(Moderator, ActionCategory.SystemModeratorReviewReport, It.IsAny<SystemModeratorReviewReportDetails>()), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Reporter, NotificationType.System, It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), FeedContentReportService.ReviewedTemplateKey), Times.Once);
            _notifications.Verify(n => n.CreateNotificationAsync(Author, It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()), Times.Never);
        }

        /// <summary>A notification failure does not fail the decision.</summary>
        [Fact]
        public async Task ReviewDecision_NotificationThrows_StillSucceeds()
        {
            var report = new FeedContentReport { Id = 42, PostId = 1, ReporterId = Reporter, ContentAuthorId = Author, Status = ReportStatus.NotReviewed };
            _repo.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(report);
            _repo.Setup(r => r.UpdateReviewAsync(42, ReportStatus.Dismissed, Moderator, It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(report);
            _notifications
                .Setup(n => n.CreateNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException());

            var result = await _service.DismissReportAsync(Moderator, 42);

            result.Success.Should().BeTrue();
        }

        /// <summary>An already reviewed report cannot be decided again.</summary>
        [Fact]
        public async Task DismissReportAsync_AlreadyReviewed_Fails()
        {
            _repo.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedContentReport { Id = 42, ReporterId = Reporter, ContentAuthorId = Author, Status = ReportStatus.Dismissed });

            var result = await _service.DismissReportAsync(Moderator, 42);

            result.Error.Should().Be(FeedReportError.AlreadyReviewed);
        }

        /// <summary>The report timestamp is set by the server in UTC, and the rate limit looks back one hour.</summary>
        [Fact]
        public async Task ReportPostAsync_SetsServerUtcTimestamp_AndOneHourWindow()
        {
            var fixedNow = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(-5));
            var clock = new Mock<TimeProvider>();
            clock.Setup(c => c.GetUtcNow()).Returns(fixedNow);
            var service = new FeedContentReportService(
                _repo.Object, _friends.Object, _roles.Object, _audit.Object, _notifications.Object,
                Options.Create(new FeedReportSettings { MaxReportsPerUserPerHour = 5 }), clock.Object, NullLogger<FeedContentReportService>.Instance);

            await service.ReportPostAsync(1, Reporter, ReportReason.Spam, null);

            _repo.Verify(r => r.AddAsync(It.Is<FeedContentReport>(x => x.CreatedAt == fixedNow.UtcDateTime), It.IsAny<CancellationToken>()), Times.Once);
            _repo.Verify(r => r.CountReportsSinceAsync(Reporter, fixedNow.UtcDateTime.AddHours(-1), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>The reporter notification is neutral and carries no moderator identity or notes.</summary>
        [Fact]
        public async Task ReviewDecision_Notification_IsNeutral()
        {
            var report = new FeedContentReport { Id = 42, PostId = 1, ReporterId = Reporter, ContentAuthorId = Author, Status = ReportStatus.NotReviewed };
            _repo.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(report);
            _repo.Setup(r => r.UpdateReviewAsync(42, ReportStatus.ActionTaken, Moderator, It.IsAny<DateTime>(), "secret note", It.IsAny<CancellationToken>())).ReturnsAsync(report);

            await _service.MarkActionTakenAsync(Moderator, 42, "secret note");

            _notifications.Verify(n => n.CreateNotificationAsync(
                Reporter, NotificationType.System,
                It.Is<string?>(m => m != null && !m.Contains(Moderator) && !m.Contains("secret") && !m.Contains("action", StringComparison.OrdinalIgnoreCase)),
                It.Is<object?>(d => d != null && !d.ToString()!.Contains(Moderator) && !d.ToString()!.Contains("secret")),
                It.IsAny<DateTime?>(), FeedContentReportService.ReviewedTemplateKey), Times.Once);
        }

        /// <summary>Submitting a report never calls anything that hides or removes content, and sends no notification.</summary>
        [Fact]
        public async Task ReportPostAsync_DoesNotNotifyOrHideContent()
        {
            await _service.ReportPostAsync(1, Reporter, ReportReason.Spam, null);

            _notifications.Verify(n => n.CreateNotificationAsync(It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()), Times.Never);
            _repo.Verify(r => r.UpdateReviewAsync(It.IsAny<int>(), It.IsAny<ReportStatus>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>A comment on content the reporter cannot see is indistinguishable from a missing comment.</summary>
        [Fact]
        public async Task ReportCommentAsync_CommentOnInvisiblePost_ContentNotFound()
        {
            _repo.Setup(r => r.GetCommentTargetAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedReportTarget(2, 6, Author, Author, null, AudienceType.MeOnly));

            var result = await _service.ReportCommentAsync(6, Reporter, ReportReason.Spam, null);

            result.Error.Should().Be(FeedReportError.ContentNotFound);
        }

        /// <summary>The allowed reason list excludes the two group-specific reasons only.</summary>
        [Fact]
        public void AllowedReasons_ExcludesGroupSpecificReasons()
        {
            FeedContentReportService.AllowedReasons.Should().NotContain(new[] { ReportReason.ViolatesGroupRule, ReportReason.OffTopic });
            FeedContentReportService.AllowedReasons.Should().Contain(ReportReason.Spam);
        }
    }
}
