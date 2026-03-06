using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.SocialFeed;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    public class MultiLineCommentDisplayTests : TestContext
    {
        [Fact]
        public void MultiLineCommentDisplay_RendersUserName()
        {
            // Arrange
            var userName = "Jane Smith";

            // Act
            var cut = RenderComponent<MultiLineCommentDisplay>(parameters => parameters
                .Add(p => p.UserName, userName)
                .Add(p => p.CommentText, "Test comment")
                .Add(p => p.Timestamp, DateTime.Now));

            // Assert
            cut.Markup.Should().Contain(userName);
        }

        [Fact]
        public void MultiLineCommentDisplay_RendersCommentText()
        {
            // Arrange
            var commentText = "This is a test comment";

            // Act
            var cut = RenderComponent<MultiLineCommentDisplay>(parameters => parameters
                .Add(p => p.UserName, "Test User")
                .Add(p => p.CommentText, commentText)
                .Add(p => p.Timestamp, DateTime.Now));

            // Assert
            cut.Markup.Should().Contain(commentText);
        }

        [Fact]
        public void MultiLineCommentDisplay_RendersMultiLineComment()
        {
            // Arrange
            var multiLineComment = @"Line 1
Line 2
Line 3";

            // Act
            var cut = RenderComponent<MultiLineCommentDisplay>(parameters => parameters
                .Add(p => p.UserName, "Test User")
                .Add(p => p.CommentText, multiLineComment)
                .Add(p => p.Timestamp, DateTime.Now));

            // Assert
            cut.Markup.Should().Contain("Line 1");
            cut.Markup.Should().Contain("Line 2");
            cut.Markup.Should().Contain("Line 3");
        }

        [Fact]
        public void MultiLineCommentDisplay_DisplaysUserAvatar()
        {
            // Arrange
            var avatarUrl = "https://example.com/user.jpg";

            // Act
            var cut = RenderComponent<MultiLineCommentDisplay>(parameters => parameters
                .Add(p => p.UserName, "Test User")
                .Add(p => p.UserImageUrl, avatarUrl)
                .Add(p => p.CommentText, "Test comment")
                .Add(p => p.Timestamp, DateTime.Now));

            // Assert
            cut.Markup.Should().Contain(avatarUrl);
        }

        [Fact]
        public void MultiLineCommentDisplay_FormatsRelativeTimestamp()
        {
            // Arrange
            var pastDate = DateTime.Now.AddMinutes(-30);

            // Act
            var cut = RenderComponent<MultiLineCommentDisplay>(parameters => parameters
                .Add(p => p.UserName, "Test User")
                .Add(p => p.CommentText, "Test comment")
                .Add(p => p.Timestamp, pastDate));

            // Assert
            // Should display relative time like "30 minutes ago"
            cut.Markup.Should().Contain("30");
        }
    }
}
