using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="NotificationTemplateService"/> rendering of moderator-entered text.
    /// </summary>
    public class NotificationTemplateServiceTests
    {
        private static NotificationTemplateService CreateService(string templateKey, string template)
        {
            var localizer = new Mock<IStringLocalizer>();
            localizer.Setup(l => l[templateKey]).Returns(new LocalizedString(templateKey, template));
            var factory = new Mock<IStringLocalizerFactory>();
            factory.Setup(f => f.Create(It.IsAny<Type>())).Returns(localizer.Object);
            var prefs = new UserPreferenceService(Mock.Of<IDbContextFactory<ApplicationDbContext>>(), NullLogger<UserPreferenceService>.Instance);
            return new NotificationTemplateService(factory.Object, prefs, NullLogger<NotificationTemplateService>.Instance);
        }

        /// <summary>HTML in a moderator reason is encoded when the notification is rendered.</summary>
        [Theory]
        [InlineData("UserWarned")]
        [InlineData("UserSuspendedPermanent")]
        public async Task RenderNotificationAsync_HtmlEncodesModeratorReason(string templateKey)
        {
            var service = CreateService(templateKey, "Notice: {0}");
            var notification = new UserNotification
            {
                UserId = string.Empty,
                TemplateKey = templateKey,
                Data = "{\"Reason\":\"<script>alert(1)</script> & <b>x</b>\"}"
            };

            var rendered = await service.RenderNotificationAsync(notification);

            rendered.Should().NotContain("<script>").And.NotContain("<b>");
            rendered.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt; &amp; &lt;b&gt;x&lt;/b&gt;");
        }

        /// <summary>The reason is encoded in the suspension template while the date stays intact.</summary>
        [Fact]
        public async Task RenderNotificationAsync_SuspendedEncodesReasonKeepsUntil()
        {
            var service = CreateService("UserSuspended", "{0} until {1}");
            var notification = new UserNotification
            {
                UserId = string.Empty,
                TemplateKey = "UserSuspended",
                Data = "{\"Reason\":\"<i>bad</i>\",\"Until\":\"2025-01-02 03:04 UTC\"}"
            };

            var rendered = await service.RenderNotificationAsync(notification);

            rendered.Should().Be("&lt;i&gt;bad&lt;/i&gt; until 2025-01-02 03:04 UTC");
        }
    }
}
