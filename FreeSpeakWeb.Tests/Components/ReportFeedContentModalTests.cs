using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.SocialFeed;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    /// <summary>
    /// bUnit tests for <see cref="ReportFeedContentModal"/>.
    /// </summary>
    public class ReportFeedContentModalTests : TestContext
    {
        private readonly Mock<IFeedContentReportService> _service = new();

        /// <summary>
        /// Registers the mocked service and a passthrough localizer.
        /// </summary>
        public ReportFeedContentModalTests()
        {
            Services.AddSingleton(_service.Object);
            Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
            Services.AddSingleton(typeof(ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        }

        private IRenderedComponent<ReportFeedContentModal> RenderPost() =>
            Render<ReportFeedContentModal>(p => p.Add(m => m.PostId, 7).Add(m => m.ReporterId, "u1"));

        private IRenderedComponent<ReportFeedContentModal> RenderComment() =>
            Render<ReportFeedContentModal>(p => p.Add(m => m.CommentId, 9).Add(m => m.ReporterId, "u1"));

        private static void SelectReasonAndSubmit(IRenderedComponent<ReportFeedContentModal> cut)
        {
            cut.FindAll("button.list-group-item")[0].Click();
            cut.Find("[data-testid='report-feed-submit']").Click();
        }

        /// <summary>
        /// Group-specific reasons must not be offered.
        /// </summary>
        [Fact]
        public void Renders_ElevenReasons_WithoutGroupSpecificOnes()
        {
            var cut = RenderPost();

            cut.FindAll("button.list-group-item").Count.Should().Be(11);
            cut.Markup.Should().NotContain("ReasonViolatesGroupRule").And.NotContain("ReasonOffTopic");
        }

        /// <summary>
        /// Submitting a post report calls the post service method with the chosen reason.
        /// </summary>
        [Fact]
        public void SubmitPost_CallsServiceAndShowsSuccess()
        {
            _service.Setup(s => s.ReportPostAsync(7, "u1", ReportReason.Spam, It.IsAny<string?>(), default))
                .ReturnsAsync(FeedReportResult.Ok(1));
            var cut = RenderPost();

            SelectReasonAndSubmit(cut);

            cut.Find("[data-testid='report-feed-success']").Should().NotBeNull();
            _service.Verify(s => s.ReportPostAsync(7, "u1", ReportReason.Spam, It.IsAny<string?>(), default), Times.Once);
        }

        /// <summary>
        /// Submitting a comment report calls the comment service method.
        /// </summary>
        [Fact]
        public void SubmitComment_CallsCommentService()
        {
            _service.Setup(s => s.ReportCommentAsync(9, "u1", ReportReason.Spam, It.IsAny<string?>(), default))
                .ReturnsAsync(FeedReportResult.Ok(2));
            var cut = RenderComment();

            SelectReasonAndSubmit(cut);

            _service.Verify(s => s.ReportCommentAsync(9, "u1", ReportReason.Spam, It.IsAny<string?>(), default), Times.Once);
        }

        /// <summary>
        /// A previously reported item shows the notice and no reason list.
        /// </summary>
        [Fact]
        public void AlreadyReported_ShowsNoticeInsteadOfForm()
        {
            _service.Setup(s => s.HasUserReportedPostAsync("u1", 7, default)).ReturnsAsync(true);

            var cut = RenderPost();

            cut.Find("[data-testid='report-feed-already']").Should().NotBeNull();
            cut.FindAll("button.list-group-item").Should().BeEmpty();
        }

        /// <summary>
        /// A duplicate result from the service switches the modal to the notice.
        /// </summary>
        [Fact]
        public void DuplicateResult_ShowsAlreadyReported()
        {
            _service.Setup(s => s.ReportPostAsync(7, "u1", It.IsAny<ReportReason>(), It.IsAny<string?>(), default))
                .ReturnsAsync(FeedReportResult.Fail(FeedReportError.DuplicateReport));
            var cut = RenderPost();

            SelectReasonAndSubmit(cut);

            cut.Find("[data-testid='report-feed-already']").Should().NotBeNull();
        }

        /// <summary>
        /// Rate limit and own-content failures are shown as localized errors.
        /// </summary>
        [Theory]
        [InlineData(FeedReportError.RateLimited, "ErrorRateLimited")]
        [InlineData(FeedReportError.OwnContent, "ErrorOwnContent")]
        public void Failure_ShowsMappedError(FeedReportError error, string key)
        {
            _service.Setup(s => s.ReportPostAsync(7, "u1", It.IsAny<ReportReason>(), It.IsAny<string?>(), default))
                .ReturnsAsync(FeedReportResult.Fail(error));
            var cut = RenderPost();

            SelectReasonAndSubmit(cut);

            cut.Find("[data-testid='report-feed-error']").TextContent.Should().Contain(key);
        }

        /// <summary>
        /// A thrown exception produces the generic error message.
        /// </summary>
        [Fact]
        public void Exception_ShowsGenericError()
        {
            _service.Setup(s => s.ReportPostAsync(7, "u1", It.IsAny<ReportReason>(), It.IsAny<string?>(), default))
                .ThrowsAsync(new InvalidOperationException("boom"));
            var cut = RenderPost();

            SelectReasonAndSubmit(cut);

            cut.Find("[data-testid='report-feed-error']").TextContent.Should().Contain("ErrorSubmitting");
        }
    }
}
