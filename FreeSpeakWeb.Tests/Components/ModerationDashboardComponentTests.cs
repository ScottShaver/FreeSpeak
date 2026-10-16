using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.Moderation;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    /// <summary>
    /// bUnit tests for the moderator dashboard components (overview, queue and detail).
    /// </summary>
    public class ModerationDashboardComponentTests : TestContext
    {
        private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        private readonly Mock<ISystemModerationService> _service = new();

        /// <summary>
        /// Registers the mocked service, a fixed clock and a passthrough localizer.
        /// </summary>
        public ModerationDashboardComponentTests()
        {
            Services.AddSingleton(_service.Object);
            Services.AddSingleton<TimeProvider>(new FixedClock(Now));
            Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        }

        private sealed class FixedClock : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedClock(DateTime utc) => _now = new DateTimeOffset(utc, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static FeedReportQueueItem Item(int id, DateTime created, bool stale = false, int others = 0, FeedReportContentType type = FeedReportContentType.Post)
            => new(id, type, type == FeedReportContentType.Post ? id : null, type == FeedReportContentType.Comment ? id : null, ReportReason.Spam, ReportStatus.NotReviewed,
                created, "excerpt " + id, "a", "Author A", "r", "Reporter R", others, stale);

        private static FeedReportDetail Detail(bool canAct = true, ReportStatus status = ReportStatus.NotReviewed, bool stale = false)
            => new(7, FeedReportContentType.Post, 3, null, ReportReason.HateSpeech, "reporter says", status, Now.AddDays(-5), null, null,
                "r", "Reporter R", "a", "Author A", "<p>The reported text</p>", Now.AddDays(-9),
                new List<FeedReportRelatedItem> { new(8, ReportReason.Spam, ReportStatus.NotReviewed, Now.AddDays(-1), "r2", "Reporter Two", "second") },
                2, stale, canAct);

        /// <summary>The overview shows the counts and the stale banner when stale reports exist.</summary>
        [Fact]
        public void Overview_ShowsCountsAndStaleBanner()
        {
            _service.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(5, 2, 9, 3, 3));

            var cut = Render<ModerationOverviewPanel>(p => p.Add(c => c.UserId, "m"));

            cut.Find("[data-testid='count-pending']").TextContent.Should().Be("5");
            cut.Find("[data-testid='count-under-review']").TextContent.Should().Be("2");
            cut.Find("[data-testid='count-closed']").TextContent.Should().Be("9");
            cut.Find("[data-testid='count-stale']").TextContent.Should().Be("3");
            cut.FindAll("[data-testid='stale-banner']").Should().HaveCount(1);
        }

        /// <summary>No banner is shown without stale reports.</summary>
        [Fact]
        public void Overview_NoStale_HidesBanner()
        {
            _service.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(1, 0, 0, 0, 3));

            var cut = Render<ModerationOverviewPanel>(p => p.Add(c => c.UserId, "m"));

            cut.FindAll("[data-testid='stale-banner']").Should().BeEmpty();
        }

        /// <summary>The stale banner button asks the host to open the queue filtered to stale reports.</summary>
        [Fact]
        public void Overview_StaleButton_OpensQueueStaleOnly()
        {
            _service.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(1, 0, 0, 2, 3));
            bool? received = null;

            var cut = Render<ModerationOverviewPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.OnOpenQueue, (bool v) => received = v));
            cut.Find("[data-testid='stale-banner'] button").Click();

            received.Should().BeTrue();
        }

        /// <summary>A null overview (not authorized or failed) shows an error with a retry.</summary>
        [Fact]
        public void Overview_Failure_ShowsError()
        {
            _service.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync((ModerationOverview?)null);

            var cut = Render<ModerationOverviewPanel>(p => p.Add(c => c.UserId, "m"));

            cut.Markup.Should().Contain("LoadError");
        }

        private void SetupQueue(IReadOnlyList<FeedReportQueueItem> items, int total, ModerationOverview? overview = null)
        {
            _service.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(overview ?? new ModerationOverview(items.Count, 0, 0, 0, 3));
            _service.Setup(s => s.GetFeedReportQueueAsync("m", It.IsAny<FeedReportQueueFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedReportQueuePage(items, total));
        }

        /// <summary>The queue renders rows in the order the service returns them (oldest first) with the total count.</summary>
        [Fact]
        public void Queue_RendersRowsInServiceOrder()
        {
            SetupQueue(new[] { Item(1, Now.AddDays(-5), stale: true), Item(2, Now.AddHours(-1)) }, 2);

            var cut = Render<FeedReportQueue>(p => p.Add(c => c.UserId, "m"));

            cut.FindAll("[data-testid='queue-row']").Select(r => r.GetAttribute("data-report-id")).Should().Equal("1", "2");
            cut.FindAll("[data-testid='row-stale']").Should().HaveCount(1);
        }

        /// <summary>An empty queue shows the empty message.</summary>
        [Fact]
        public void Queue_Empty_ShowsEmptyMessage()
        {
            SetupQueue(Array.Empty<FeedReportQueueItem>(), 0);

            var cut = Render<FeedReportQueue>(p => p.Add(c => c.UserId, "m"));

            cut.Find("[data-testid='queue-empty']").TextContent.Should().Be("QueueEmpty");
        }

        /// <summary>A service failure shows the error state.</summary>
        [Fact]
        public void Queue_ServiceThrows_ShowsError()
        {
            _service.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(0, 0, 0, 0, 3));
            _service.Setup(s => s.GetFeedReportQueueAsync("m", It.IsAny<FeedReportQueueFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());

            var cut = Render<FeedReportQueue>(p => p.Add(c => c.UserId, "m"));

            cut.Markup.Should().Contain("LoadError");
        }

        /// <summary>The stale button restricts the queue to stale reports and the filter can be cleared.</summary>
        [Fact]
        public void Queue_StaleButton_AppliesStaleOnlyFilter()
        {
            SetupQueue(new[] { Item(1, Now.AddDays(-5), stale: true) }, 1, new ModerationOverview(3, 0, 0, 1, 3));

            var cut = Render<FeedReportQueue>(p => p.Add(c => c.UserId, "m"));
            cut.Find("[data-testid='stale-show']").Click();

            _service.Verify(s => s.GetFeedReportQueueAsync("m", It.Is<FeedReportQueueFilter>(f => f.StaleOnly), 1, 25, It.IsAny<CancellationToken>()), Times.Once);
            cut.FindAll("[data-testid='stale-clear']").Should().HaveCount(1);
        }

        /// <summary>The sort toggle flips to newest first.</summary>
        [Fact]
        public void Queue_SortToggle_RequestsNewestFirst()
        {
            SetupQueue(new[] { Item(1, Now.AddDays(-5)) }, 1);

            var cut = Render<FeedReportQueue>(p => p.Add(c => c.UserId, "m"));
            cut.Find("[data-testid='sort-toggle']").Click();

            _service.Verify(s => s.GetFeedReportQueueAsync("m", It.Is<FeedReportQueueFilter>(f => f.NewestFirst), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>The type filter is passed to the service.</summary>
        [Fact]
        public void Queue_TypeFilter_IsPassedToService()
        {
            SetupQueue(new[] { Item(1, Now.AddDays(-5)) }, 1);

            var cut = Render<FeedReportQueue>(p => p.Add(c => c.UserId, "m"));
            cut.Find("[data-testid='flt-type']").Change(((int)FeedReportContentType.Comment).ToString());

            _service.Verify(s => s.GetFeedReportQueueAsync("m", It.Is<FeedReportQueueFilter>(f => f.ContentType == FeedReportContentType.Comment), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>Load more appends the next page.</summary>
        [Fact]
        public void Queue_LoadMore_AppendsNextPage()
        {
            _service.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(2, 0, 0, 0, 3));
            _service.Setup(s => s.GetFeedReportQueueAsync("m", It.IsAny<FeedReportQueueFilter>(), 1, 25, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedReportQueuePage(new[] { Item(1, Now.AddDays(-2)) }, 2));
            _service.Setup(s => s.GetFeedReportQueueAsync("m", It.IsAny<FeedReportQueueFilter>(), 2, 25, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FeedReportQueuePage(new[] { Item(2, Now.AddDays(-1)) }, 2));

            var cut = Render<FeedReportQueue>(p => p.Add(c => c.UserId, "m"));
            cut.Find("[data-testid='load-more']").Click();

            cut.FindAll("[data-testid='queue-row']").Should().HaveCount(2);
            cut.FindAll("[data-testid='load-more']").Should().BeEmpty();
        }

        /// <summary>Opening a row shows the detail view.</summary>
        [Fact]
        public void Queue_ClickRow_ShowsDetail()
        {
            SetupQueue(new[] { Item(7, Now.AddDays(-5)) }, 1);
            _service.Setup(s => s.GetFeedReportDetailAsync("m", 7, It.IsAny<CancellationToken>())).ReturnsAsync(Detail());

            var cut = Render<FeedReportQueue>(p => p.Add(c => c.UserId, "m"));
            cut.Find("[data-testid='queue-row']").Click();

            cut.FindAll("[data-testid='feed-report-detail']").Should().HaveCount(1);
            cut.Find("[data-testid='detail-content']").TextContent.Should().Contain("The reported text");
        }

        private IRenderedComponent<FeedReportDetailPanel> RenderDetail(FeedReportDetail detail)
        {
            _service.Setup(s => s.GetFeedReportDetailAsync("m", 7, It.IsAny<CancellationToken>())).ReturnsAsync(detail);
            return Render<FeedReportDetailPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.ReportId, 7));
        }

        /// <summary>The detail shows description, other reports, author history and the stale badge.</summary>
        [Fact]
        public void Detail_ShowsContext()
        {
            var cut = RenderDetail(Detail(stale: true));

            cut.Find("[data-testid='detail-description']").TextContent.Should().Be("reporter says");
            cut.Find("[data-testid='detail-other-reports']").TextContent.Should().Contain("Reporter Two");
            cut.FindAll("[data-testid='detail-author-history']").Should().HaveCount(1);
            cut.FindAll("[data-testid='detail-stale']").Should().HaveCount(1);
        }

        /// <summary>When the moderator cannot act (own content or already closed) no action buttons are shown.</summary>
        [Fact]
        public void Detail_CannotAct_HidesActions()
        {
            var cut = RenderDetail(Detail(canAct: false));

            cut.FindAll("[data-testid='action-dismiss']").Should().BeEmpty();
            cut.FindAll("[data-testid='detail-cannot-act']").Should().HaveCount(1);
        }

        /// <summary>The under-review action is only offered for reports that are not yet under review.</summary>
        [Fact]
        public void Detail_UnderReview_HidesMarkUnderReview()
        {
            var cut = RenderDetail(Detail(status: ReportStatus.UnderReview));

            cut.FindAll("[data-testid='action-under-review']").Should().BeEmpty();
            cut.FindAll("[data-testid='action-remove']").Should().HaveCount(1);
        }

        /// <summary>Mark under review requires confirmation before calling the service.</summary>
        [Fact]
        public void Detail_MarkUnderReview_RequiresConfirmationThenCallsService()
        {
            _service.Setup(s => s.MarkUnderReviewAsync("m", 7, It.IsAny<CancellationToken>())).ReturnsAsync(FeedReportResult.Ok(7));
            var cut = RenderDetail(Detail());

            cut.Find("[data-testid='action-under-review']").Click();
            _service.Verify(s => s.MarkUnderReviewAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            cut.Find("[data-testid='detail-confirm']").TextContent.Should().Contain("ConfirmUnderReview");

            cut.Find("[data-testid='detail-confirm-yes']").Click();

            _service.Verify(s => s.MarkUnderReviewAsync("m", 7, It.IsAny<CancellationToken>()), Times.Once);
            cut.Find("[data-testid='detail-message']").TextContent.Should().Be("DoneUnderReview");
        }

        /// <summary>Dismiss requires confirmation and then calls the service with the notes.</summary>
        [Fact]
        public void Detail_Dismiss_RequiresConfirmationThenCallsService()
        {
            _service.Setup(s => s.DismissAsync("m", 7, "not a violation", It.IsAny<CancellationToken>())).ReturnsAsync(FeedReportResult.Ok(7));
            var cut = RenderDetail(Detail());

            cut.Find("#mod-notes").Input("not a violation");
            cut.Find("[data-testid='action-dismiss']").Click();
            _service.Verify(s => s.DismissAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

            cut.Find("[data-testid='detail-confirm-yes']").Click();

            _service.Verify(s => s.DismissAsync("m", 7, "not a violation", It.IsAny<CancellationToken>()), Times.Once);
            cut.Find("[data-testid='detail-message']").TextContent.Should().Be("DoneDismissed");
        }

        /// <summary>Cancelling the confirmation does not call the service.</summary>
        [Fact]
        public void Detail_RemoveCancel_DoesNotCallService()
        {
            var cut = RenderDetail(Detail());

            cut.Find("[data-testid='action-remove']").Click();
            cut.Find("[data-testid='detail-confirm'] .btn-outline-secondary").Click();

            _service.Verify(s => s.RemoveContentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            cut.FindAll("[data-testid='detail-confirm']").Should().BeEmpty();
        }

        /// <summary>Removing content shows the success message even though the report no longer exists.</summary>
        [Fact]
        public void Detail_Remove_ShowsSuccessAfterReportDeleted()
        {
            _service.SetupSequence(s => s.GetFeedReportDetailAsync("m", 7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Detail())
                .ReturnsAsync((FeedReportDetail?)null);
            _service.Setup(s => s.RemoveContentAsync("m", 7, null, It.IsAny<CancellationToken>())).ReturnsAsync(FeedReportResult.Ok(7));
            var cut = Render<FeedReportDetailPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.ReportId, 7));

            cut.Find("[data-testid='action-remove']").Click();
            cut.Find("[data-testid='detail-confirm-yes']").Click();

            var message = cut.Find("[data-testid='detail-message']");
            message.TextContent.Should().Be("DoneRemoved");
            message.ClassList.Should().Contain("alert-success");
        }

        /// <summary>An AlreadyReviewed failure is shown as an error and the queue is told to refresh.</summary>
        [Fact]
        public void Detail_AlreadyReviewed_ShowsErrorAndNotifiesHost()
        {
            _service.Setup(s => s.ResolveAsync("m", 7, null, It.IsAny<CancellationToken>())).ReturnsAsync(FeedReportResult.Fail(FeedReportError.AlreadyReviewed));
            _service.Setup(s => s.GetFeedReportDetailAsync("m", 7, It.IsAny<CancellationToken>())).ReturnsAsync(Detail());
            bool? changed = null;
            var cut = Render<FeedReportDetailPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.ReportId, 7).Add(c => c.OnChanged, (bool v) => changed = v));

            cut.Find("[data-testid='action-resolve']").Click();
            cut.Find("[data-testid='detail-confirm-yes']").Click();

            cut.Find("[data-testid='detail-message']").TextContent.Should().Be("ErrAlreadyReviewed");
            changed.Should().BeTrue();
        }

        /// <summary>Error enums map to localization keys.</summary>
        [Theory]
        [InlineData(FeedReportError.NotAuthorized, "ErrNotAuthorized")]
        [InlineData(FeedReportError.OwnContent, "ErrOwnContent")]
        [InlineData(FeedReportError.OwnReport, "ErrOwnReport")]
        [InlineData(FeedReportError.RemovalFailed, "ErrRemovalFailed")]
        [InlineData(FeedReportError.Failed, "ErrGeneric")]
        public void ErrorKey_MapsErrors(FeedReportError error, string key)
        {
            FeedReportDisplay.ErrorKey(error).Should().Be(key);
        }
    }
}
