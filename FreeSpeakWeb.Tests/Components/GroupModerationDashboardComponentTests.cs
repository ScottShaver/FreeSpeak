using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.Moderation;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    /// <summary>
    /// bUnit tests for the system moderator group report queue, detail panel, overview section and activity tab.
    /// </summary>
    public class GroupModerationDashboardComponentTests : TestContext
    {
        private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        private readonly Mock<ISystemGroupReportService> _groups = new();
        private readonly Mock<ISystemModerationService> _feed = new();
        private readonly Mock<ISystemModeratorActivityService> _activity = new();

        /// <summary>
        /// Registers mocked services, a fixed clock, a passthrough localizer and loose JS interop.
        /// </summary>
        public GroupModerationDashboardComponentTests()
        {
            Services.AddSingleton(_groups.Object);
            Services.AddSingleton(_feed.Object);
            Services.AddSingleton(_activity.Object);
            Services.AddSingleton<TimeProvider>(new FixedClock(Now));
            Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
            Services.AddLogging();
            JSInterop.Mode = JSRuntimeMode.Loose;
            _groups.Setup(s => s.GetGroupOptionsAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new List<GroupReportGroupOption> { new(1, "Alpha"), new(2, "Beta") });
        }

        private sealed class FixedClock : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedClock(DateTime utc) => _now = new DateTimeOffset(utc, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static GroupReportQueueItem Item(int id, int groupId, string group, DateTime created, bool stale = false)
            => new(id, groupId, group, FeedReportContentType.Post, id, null, ReportReason.Spam, ReportStatus.NotReviewed,
                created, "excerpt " + id, "a", "Author A", "r", "Reporter R", 0, stale);

        private static GroupReportDetail Detail(bool canAct = true, ReportStatus status = ReportStatus.NotReviewed)
            => new(7, 2, "Beta", FeedReportContentType.Post, 3, null, ReportReason.HateSpeech, "Be kind", "reporter says", status, Now.AddDays(-5), null, null,
                "r", "Reporter R", "a", "Author A", "<p>The reported text</p>", Now.AddDays(-9),
                new List<FeedReportRelatedItem>(), 0, false, canAct);

        /// <summary>The overview shows the group section with its counts and stale banner.</summary>
        [Fact]
        public void Overview_ShowsGroupSection()
        {
            _feed.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(1, 0, 0, 0, 3));
            _groups.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(4, 1, 2, 2, 3));

            var cut = Render<ModerationOverviewPanel>(p => p.Add(c => c.UserId, "m"));

            cut.Find("[data-testid='group-count-pending']").TextContent.Should().Be("4");
            cut.Find("[data-testid='group-count-stale']").TextContent.Should().Be("2");
            cut.FindAll("[data-testid='group-stale-banner']").Should().HaveCount(1);
        }

        /// <summary>The group stale button asks the host to open the group queue filtered to stale reports.</summary>
        [Fact]
        public void Overview_GroupStaleButton_OpensGroupQueueStaleOnly()
        {
            _feed.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(0, 0, 0, 0, 3));
            _groups.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(1, 0, 0, 1, 3));
            bool? received = null;

            var cut = Render<ModerationOverviewPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.OnOpenGroupQueue, (bool v) => received = v));
            cut.Find("[data-testid='group-stale-show']").Click();

            received.Should().BeTrue();
        }

        /// <summary>No group stale banner without stale group reports.</summary>
        [Fact]
        public void Overview_NoGroupStale_HidesGroupBanner()
        {
            _feed.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(0, 0, 0, 0, 3));
            _groups.Setup(s => s.GetOverviewAsync("m", It.IsAny<CancellationToken>())).ReturnsAsync(new ModerationOverview(1, 0, 0, 0, 3));

            var cut = Render<ModerationOverviewPanel>(p => p.Add(c => c.UserId, "m"));

            cut.FindAll("[data-testid='group-stale-banner']").Should().BeEmpty();
        }

        /// <summary>The queue renders reports from several groups in the service order with group badges.</summary>
        [Fact]
        public void Queue_RendersRowsFromMultipleGroups()
        {
            var items = new List<GroupReportQueueItem> { Item(1, 1, "Alpha", Now.AddDays(-5), true), Item(2, 2, "Beta", Now.AddDays(-1)) };
            _groups.Setup(s => s.GetQueueAsync("m", It.IsAny<GroupReportQueueFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GroupReportQueuePage(items, 2));

            var cut = Render<GroupReportQueue>(p => p.Add(c => c.UserId, "m"));

            cut.Markup.Should().Contain("Alpha").And.Contain("Beta");
            cut.Markup.IndexOf("excerpt 1", StringComparison.Ordinal).Should().BeLessThan(cut.Markup.IndexOf("excerpt 2", StringComparison.Ordinal));
        }

        /// <summary>Starting with the stale filter passes StaleOnly to the service.</summary>
        [Fact]
        public void Queue_InitialStaleOnly_PassesFilter()
        {
            GroupReportQueueFilter? seen = null;
            _groups.Setup(s => s.GetQueueAsync("m", It.IsAny<GroupReportQueueFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<string, GroupReportQueueFilter, int, int, CancellationToken>((_, f, _, _, _) => seen = f)
                .ReturnsAsync(new GroupReportQueuePage(new List<GroupReportQueueItem>(), 0));

            Render<GroupReportQueue>(p => p.Add(c => c.UserId, "m").Add(c => c.InitialStaleOnly, true));

            seen.Should().NotBeNull();
            seen!.StaleOnly.Should().BeTrue();
        }

        /// <summary>The detail panel shows the group, rule and content and offers all actions.</summary>
        [Fact]
        public void Detail_ShowsGroupRuleAndActions()
        {
            _groups.Setup(s => s.GetDetailAsync("m", 7, It.IsAny<CancellationToken>())).ReturnsAsync(Detail());

            var cut = Render<GroupReportDetailPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.ReportId, 7));

            cut.Find("[data-testid='detail-group']").TextContent.Should().Contain("Beta");
            cut.Find("[data-testid='detail-rule']").TextContent.Should().Contain("Be kind");
            cut.Find("[data-testid='detail-content']").InnerHtml.Should().Contain("The reported text");
            cut.FindAll("[data-testid='action-dismiss']").Should().HaveCount(1);
            cut.FindAll("[data-testid='action-resolve']").Should().HaveCount(1);
            cut.FindAll("[data-testid='action-remove']").Should().HaveCount(1);
        }

        /// <summary>A report the viewer cannot act on hides the action buttons.</summary>
        [Fact]
        public void Detail_CannotAct_HidesActions()
        {
            _groups.Setup(s => s.GetDetailAsync("m", 7, It.IsAny<CancellationToken>())).ReturnsAsync(Detail(canAct: false));

            var cut = Render<GroupReportDetailPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.ReportId, 7));

            cut.FindAll("[data-testid='detail-cannot-act']").Should().HaveCount(1);
            cut.FindAll("[data-testid='action-remove']").Should().BeEmpty();
        }

        /// <summary>Removing requires confirmation and then calls the remove service method.</summary>
        [Fact]
        public void Detail_Remove_ConfirmsThenCallsService()
        {
            _groups.Setup(s => s.GetDetailAsync("m", 7, It.IsAny<CancellationToken>())).ReturnsAsync(Detail());
            _groups.Setup(s => s.RemoveContentAsync("m", 7, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(FeedReportResult.Ok(7));
            bool? changed = null;

            var cut = Render<GroupReportDetailPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.ReportId, 7).Add(c => c.OnChanged, (bool v) => changed = v));
            cut.Find("[data-testid='action-remove']").Click();
            _groups.Verify(s => s.RemoveContentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            cut.Find("[data-testid='detail-confirm-yes']").Click();

            _groups.Verify(s => s.RemoveContentAsync("m", 7, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            changed.Should().BeTrue();
        }

        /// <summary>An already-reviewed result shows an error and refreshes the queue.</summary>
        [Fact]
        public void Detail_AlreadyReviewed_ShowsErrorAndNotifiesHost()
        {
            _groups.Setup(s => s.GetDetailAsync("m", 7, It.IsAny<CancellationToken>())).ReturnsAsync(Detail());
            _groups.Setup(s => s.DismissAsync("m", 7, It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(FeedReportResult.Fail(FeedReportError.AlreadyReviewed));
            bool? changed = null;

            var cut = Render<GroupReportDetailPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.ReportId, 7).Add(c => c.OnChanged, (bool v) => changed = v));
            cut.Find("[data-testid='action-dismiss']").Click();
            cut.Find("[data-testid='detail-confirm-yes']").Click();

            cut.Find("[data-testid='detail-message']").TextContent.Should().NotBeEmpty();
            changed.Should().BeTrue();
        }

        /// <summary>The activity tab renders UTC timestamps and the localized action.</summary>
        [Fact]
        public void Activity_RendersRowsWithUtcTimestamp()
        {
            _activity.Setup(s => s.GetModeratorOptionsAsync("m")).ReturnsAsync(new List<ModeratorOption> { new("m", "Mod M") });
            var entry = new ModeratorActivityEntry(1, new DateTime(2026, 6, 9, 14, 5, 7, DateTimeKind.Utc), "m", "Mod M", ActionCategory.SystemModeratorRemovePost, "a", "Author A", "detail", 2, "Beta");
            _activity.Setup(s => s.GetActivityAsync("m", It.IsAny<ModeratorActivityFilter>(), 1, It.IsAny<int>())).ReturnsAsync(new ModeratorActivityPage(true, new List<ModeratorActivityEntry> { entry }, 1));

            var cut = Render<ModerationActivityTab>(p => p.Add(c => c.UserId, "m"));

            cut.Find("[data-testid='act-when']").TextContent.Should().Be("2026-06-09 14:05:07 UTC");
            cut.Markup.Should().Contain("Beta").And.Contain("ActionRemovePost");
        }

        /// <summary>Review rows show the localized status instead of the raw enum name.</summary>
        [Theory]
        [InlineData(ActionCategory.SystemModeratorReviewReport, "ActionTaken", "StatusActionTaken")]
        [InlineData(ActionCategory.SystemModeratorReviewReport, "Resolved", "StatusResolved")]
        [InlineData(ActionCategory.SystemModeratorRemovePost, "Spam — excerpt", "ReasonSpam — excerpt")]
        public void Activity_Detail_IsLocalized(ActionCategory action, string detail, string expected)
        {
            _activity.Setup(s => s.GetModeratorOptionsAsync("m")).ReturnsAsync(new List<ModeratorOption>());
            var entry = new ModeratorActivityEntry(1, Now, "m", "Mod M", action, null, null, detail, 2, "Beta");
            _activity.Setup(s => s.GetActivityAsync("m", It.IsAny<ModeratorActivityFilter>(), 1, It.IsAny<int>())).ReturnsAsync(new ModeratorActivityPage(true, new List<ModeratorActivityEntry> { entry }, 1));

            var cut = Render<ModerationActivityTab>(p => p.Add(c => c.UserId, "m"));

            cut.Find("[data-testid='act-detail']").TextContent.Should().Be(expected);
        }

        /// <summary>A report whose content was removed shows a localized unknown-author label and no warn button.</summary>
        [Fact]
        public void Detail_ClosedAfterRemoval_ShowsUnknownAuthorAndHidesWarn()
        {
            _groups.Setup(s => s.GetDetailAsync("m", 7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Detail(canAct: false, status: ReportStatus.ActionTaken) with { AuthorId = string.Empty, AuthorDisplayName = "?" });

            var cut = Render<GroupReportDetailPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.ReportId, 7));

            cut.Markup.Should().Contain("AuthorUnknown").And.NotContain("Author: ?");
            cut.FindAll("[data-testid='action-moderate-author']").Should().BeEmpty();
        }

        /// <summary>An open report the viewer can act on still offers the warn/suspend action.</summary>
        [Fact]
        public void Detail_Open_ShowsModerateAuthor()
        {
            _groups.Setup(s => s.GetDetailAsync("m", 7, It.IsAny<CancellationToken>())).ReturnsAsync(Detail());

            var cut = Render<GroupReportDetailPanel>(p => p.Add(c => c.UserId, "m").Add(c => c.ReportId, 7));

            cut.FindAll("[data-testid='action-moderate-author']").Should().HaveCount(1);
        }

        /// <summary>Changing the moderator filter reloads the first page with that filter.</summary>
        [Fact]
        public void Activity_ModeratorFilter_ReloadsWithFilter()
        {
            _activity.Setup(s => s.GetModeratorOptionsAsync("m")).ReturnsAsync(new List<ModeratorOption> { new("x", "Mod X") });
            _activity.Setup(s => s.GetActivityAsync("m", It.IsAny<ModeratorActivityFilter>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new ModeratorActivityPage(true, new List<ModeratorActivityEntry>(), 0));

            var cut = Render<ModerationActivityTab>(p => p.Add(c => c.UserId, "m"));
            cut.Find("[data-testid='act-moderator']").Change("x");

            _activity.Verify(s => s.GetActivityAsync("m", It.Is<ModeratorActivityFilter>(f => f.ModeratorUserId == "x"), 1, It.IsAny<int>()), Times.Once);
            cut.FindAll("[data-testid='act-empty']").Should().HaveCount(1);
        }
    }
}
