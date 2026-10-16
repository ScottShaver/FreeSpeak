using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.Moderation;
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
    /// bUnit tests for the user warn/suspend/lift panel and the user search tab.
    /// </summary>
    public class UserModerationPanelTests : TestContext
    {
        private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        private readonly Mock<IUserModerationService> _service = new();

        /// <summary>
        /// Registers the mocked service, a fixed clock and a passthrough localizer.
        /// </summary>
        public UserModerationPanelTests()
        {
            Services.AddSingleton(_service.Object);
            Services.AddSingleton<TimeProvider>(new FixedClock());
            Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        }

        private sealed class FixedClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
        }

        private static UserModerationHistory History(bool canModerate = true, bool suspended = false,         bool canLift = false, bool canSuspendLong = false)
        {
            var suspensions = suspended
                ? new List<UserSuspensionItem> { new(4, "bad", null, Now.AddDays(-1), Now.AddDays(2), null, null, "m", "Mod", true, canLift) }
                : new List<UserSuspensionItem>();
            return new UserModerationHistory(
                new ModerationUserSummary("u1", "target", "Target User"),
                new List<UserWarningItem>(), suspensions, new List<UserReportItem>(),
                suspended, canModerate, canSuspendLong, 7);
        }

        private IRenderedComponent<UserModerationPanel> RenderPanel()
            => Render<UserModerationPanel>(p => p
                .Add(c => c.ActorId, "m")
                .Add(c => c.TargetUserId, "u1")
                .Add(c => c.RelatedReportId, 9));

        /// <summary>A moderator who can act sees the warn and suspend buttons.</summary>
        [Fact]
        public void Panel_ShowsActionButtons_WhenCanModerate()
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History());

            var cut = RenderPanel();

            cut.FindAll("[data-testid=user-warn]").Should().HaveCount(1);
            cut.FindAll("[data-testid=user-suspend]").Should().HaveCount(1);
            cut.FindAll("[data-testid=user-cannot-moderate]").Should().BeEmpty();
        }

        /// <summary>When the caller cannot act, the actions are hidden and a notice is shown.</summary>
        [Fact]
        public void Panel_HidesActions_WhenCannotModerate()
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History(canModerate: false));

            var cut = RenderPanel();

            cut.FindAll("[data-testid=user-warn]").Should().BeEmpty();
            cut.FindAll("[data-testid=user-suspend]").Should().BeEmpty();
            cut.FindAll("[data-testid=user-cannot-moderate]").Should().HaveCount(1);
        }

        /// <summary>A missing history shows the load error.</summary>
        [Fact]
        public void Panel_NullHistory_ShowsError()
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync((UserModerationHistory?)null);

            var cut = RenderPanel();

            cut.FindAll("[data-testid=user-panel-error]").Should().HaveCount(1);
        }

        /// <summary>Warning requires confirmation and passes the reason and related report to the service.</summary>
        [Fact]
        public void Panel_Warn_CallsServiceAfterConfirm()
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History());
            _service.Setup(s => s.WarnUserAsync("m", "u1", "be nice", 9, It.IsAny<CancellationToken>())).ReturnsAsync(UserModerationResult.Ok(1));
            var cut = RenderPanel();

            cut.Find("[data-testid=user-warn]").Click();
            cut.Find("[data-testid=user-reason]").Input("be nice");
            _service.Verify(s => s.WarnUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
            cut.Find("[data-testid=user-confirm]").Click();

            _service.Verify(s => s.WarnUserAsync("m", "u1", "be nice", 9, It.IsAny<CancellationToken>()), Times.Once);
            cut.Find("[data-testid=user-panel-message]").TextContent.Should().Contain("DoneWarned");
        }

        /// <summary>Choosing a duration suspends for that many days.</summary>
        [Fact]
        public void Panel_Suspend_SendsSelectedDuration()
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History());
            _service.Setup(s => s.SuspendUserAsync("m", "u1", "spam", TimeSpan.FromDays(3), 9, It.IsAny<CancellationToken>())).ReturnsAsync(UserModerationResult.Ok(2));
            var cut = RenderPanel();

            cut.Find("[data-testid=user-suspend]").Click();
            cut.Find("[data-testid=user-reason]").Input("spam");
            cut.Find("[data-testid=user-duration]").Change("3");
            cut.Find("[data-testid=user-confirm]").Click();

            _service.Verify(s => s.SuspendUserAsync("m", "u1", "spam", TimeSpan.FromDays(3), 9, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>The permanent option sends a null duration for administrators.</summary>
        [Fact]
        public void Panel_Suspend_Permanent_SendsNullDuration()
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History(canSuspendLong: true));
            _service.Setup(s => s.SuspendUserAsync("m", "u1", "x", null, 9, It.IsAny<CancellationToken>())).ReturnsAsync(UserModerationResult.Ok(2));
            var cut = RenderPanel();

            cut.Find("[data-testid=user-suspend]").Click();
            cut.Find("[data-testid=user-reason]").Input("x");
            cut.Find("[data-testid=user-duration]").Change("0");
            cut.Find("[data-testid=user-confirm]").Click();

            _service.Verify(s => s.SuspendUserAsync("m", "u1", "x", null, 9, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>Moderators are offered only durations up to the limit; administrators get all options.</summary>
        [Theory]
        [InlineData(false, new[] { "1", "3", "7" })]
        [InlineData(true, new[] { "1", "3", "7", "14", "30", "0" })]
        public void Panel_DurationOptions_DependOnRole(bool canSuspendLong, string[] expected)
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History(canSuspendLong: canSuspendLong));
            var cut = RenderPanel();

            cut.Find("[data-testid=user-suspend]").Click();

            cut.FindAll("[data-testid=user-duration] option").Select(o => o.GetAttribute("value")).Should().Equal(expected);
        }

        /// <summary>An already suspended user cannot be suspended again from the panel.</summary>
        [Fact]
        public void Panel_HidesSuspend_WhenAlreadySuspended()
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History(suspended: true, canLift: true));
            _service.Setup(s => s.GetActiveSuspensionAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActiveSuspensionInfo("bad", Now.AddDays(2)));

            var cut = RenderPanel();

            cut.FindAll("[data-testid=user-suspend]").Should().BeEmpty();
            cut.FindAll("[data-testid=user-lift]").Should().HaveCount(1);
            cut.FindAll("[data-testid=user-warn]").Should().HaveCount(1);
        }

        /// <summary>Cancel closes the form without calling the service.</summary>
        [Fact]
        public void Panel_Cancel_DoesNotCallService()
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History());
            var cut = RenderPanel();

            cut.Find("[data-testid=user-warn]").Click();
            cut.Find("[data-testid=user-cancel]").Click();

            cut.FindAll("[data-testid=user-action-form]").Should().BeEmpty();
            _service.Verify(s => s.WarnUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>An active suspension shows the status badge and lift button only when allowed.</summary>
        [Theory]
        [InlineData(true, 1)]
        [InlineData(false, 0)]
        public void Panel_LiftButton_DependsOnCanLift(bool canLift, int expected)
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History(suspended: true, canLift: canLift));
            _service.Setup(s => s.GetActiveSuspensionAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActiveSuspensionInfo("bad", Now.AddDays(2)));

            var cut = RenderPanel();

            cut.Find("[data-testid=user-status]").TextContent.Should().Contain("StatusSuspendedUntil");
            cut.FindAll("[data-testid=user-lift]").Should().HaveCount(expected);
        }

        /// <summary>Lifting calls the service with the suspension ID and note.</summary>
        [Fact]
        public void Panel_Lift_CallsService()
        {
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History(suspended: true, canLift: true));
            _service.Setup(s => s.GetActiveSuspensionAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActiveSuspensionInfo("bad", Now.AddDays(2)));
            _service.Setup(s => s.LiftSuspensionAsync("m", 4, "ok", It.IsAny<CancellationToken>())).ReturnsAsync(UserModerationResult.Ok(4));
            var cut = RenderPanel();

            cut.Find("[data-testid=user-lift]").Click();
            cut.Find("[data-testid=user-lift-note]").Input("ok");
            cut.Find("[data-testid=user-confirm]").Click();

            _service.Verify(s => s.LiftSuspensionAsync("m", 4, "ok", It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>The search tab lists matches and selecting one shows the moderation panel.</summary>
        [Fact]
        public void Tab_SearchAndSelect_ShowsPanel()
        {
            _service.Setup(s => s.SearchUsersAsync("m", "tar", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ModerationUserSummary> { new("u1", "target", "Target User") });
            _service.Setup(s => s.GetUserHistoryAsync("m", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(History());

            var cut = Render<UserModerationTab>(p => p.Add(c => c.UserId, "m"));
            cut.Find("[data-testid=user-search]").Input("tar");
            cut.Find("[data-testid=user-search-button]").Click();
            cut.Find("[data-testid=user-select]").Click();

            cut.FindAll("[data-testid=user-moderation-panel]").Should().HaveCount(1);
        }
    }
}
