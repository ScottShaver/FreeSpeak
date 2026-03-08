using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.SocialFeed;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    public class FeedArticleTests : TestContext
    {
        public FeedArticleTests()
        {
            // Setup JSInterop for MultiLineCommentEditor module
            JSInterop.SetupModule("./Components/SocialFeed/MultiLineCommentEditor.razor.js");

            // Register required services for FeedArticle component

            // Mock IDbContextFactory for PostService
            var mockDbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();

            // Mock ILogger for PostService
            var mockLogger = new Mock<ILogger<PostService>>();

            // Mock IWebHostEnvironment for PostService
            var mockEnvironment = new Mock<IWebHostEnvironment>();
            mockEnvironment.Setup(m => m.ContentRootPath).Returns(Path.GetTempPath());

            // Create SiteSettings for PostService
            var siteSettings = new SiteSettings
            {
                SiteName = "FreeSpeak Test",
                MaxFeedPostCommentDepth = 4,
                MaxFeedPostDirectCommentCount = 1000
            };
            var siteSettingsOptions = Options.Create(siteSettings);

            // Create PostService with mocked dependencies
            var postService = new PostService(
                mockDbContextFactory.Object,
                mockLogger.Object,
                siteSettingsOptions,
                mockEnvironment.Object
            );

            Services.AddSingleton(postService);
            Services.AddSingleton(siteSettingsOptions);
        }

        [Fact]
        public void FeedArticle_RendersAuthorName()
        {
            // Arrange
            var authorName = "John Doe";

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.AuthorName, authorName)
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert
            cut.Find(".author-name").TextContent.Should().Be(authorName);
        }

        [Fact]
        public void FeedArticle_RendersLikeCount()
        {
            // Arrange
            var likeCount = 42;

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, likeCount)
                .Add(p => p.CommentCount, 0));

            // Assert
            cut.Markup.Should().Contain($"{likeCount} likes");
        }

        [Fact]
        public void FeedArticle_RendersCommentCount()
        {
            // Arrange
            var commentCount = 15;

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, commentCount));

            // Assert
            cut.Markup.Should().Contain($"{commentCount} comments");
        }

        [Fact]
        public void FeedArticle_RendersArticleContent()
        {
            // Arrange
            var contentText = "This is a test post content";

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.ArticleContent, (RenderFragment)(builder =>
                {
                    builder.AddContent(0, contentText);
                })));

            // Assert
            cut.Find(".article-content").TextContent.Should().Contain(contentText);
        }

        [Fact]
        public void FeedArticle_HasActionButtons()
        {
            // Arrange & Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert
            var actionButtons = cut.FindAll(".action-button");
            actionButtons.Should().HaveCount(3); // Like, Comment, Share

            actionButtons[0].TextContent.Trim().Should().Contain("Like");
            actionButtons[1].TextContent.Trim().Should().Contain("Comment");
            actionButtons[2].TextContent.Trim().Should().Contain("Share");
        }

        [Fact]
        public void FeedArticle_DisplaysAuthorAvatar()
        {
            // Arrange
            var avatarUrl = "https://example.com/avatar.jpg";

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.AuthorImageUrl, avatarUrl)
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert
            var avatar = cut.Find(".author-avatar");
            avatar.GetAttribute("src").Should().Be(avatarUrl);
        }

        [Fact]
        public void FeedArticle_DisplaysDefaultAvatarWhenNotProvided()
        {
            // Arrange & Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert
            var avatar = cut.Find(".author-avatar");
            avatar.GetAttribute("src").Should().Contain("default-avatar");
        }

        [Fact]
        public void FeedArticle_FormatsTimestamp()
        {
            // Arrange
            var pastDate = DateTime.Now.AddHours(-2);

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, pastDate)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert
            var timestamp = cut.Find(".article-timestamp");
            timestamp.TextContent.Should().Contain("2h"); // Shortened format
        }

        [Fact]
        public void FeedArticle_HasMenuButton()
        {
            // Arrange & Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert
            var menuButton = cut.Find(".menu-button");
            menuButton.Should().NotBeNull();
        }
    }
}
