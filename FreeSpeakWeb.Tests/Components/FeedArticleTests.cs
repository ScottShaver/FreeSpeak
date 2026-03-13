using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.SocialFeed;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
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
            // Setup JSInterop for modules used by FeedArticle
            JSInterop.SetupModule("./Components/SocialFeed/MultiLineCommentEditor.razor.js");
            JSInterop.SetupModule("./Components/SocialFeed/FeedArticle.razor.js");
            JSInterop.SetupModule("./Components/SocialFeed/FeedArticleImages.razor.js");

            // Register required services for FeedArticle component

            // Mock IDbContextFactory for PostService
            var mockDbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();

            // Mock ILogger for PostService
            var mockLogger = new Mock<ILogger<PostService>>();

            // Mock IWebHostEnvironment for PostService
            var mockEnvironment = new Mock<IWebHostEnvironment>();
            mockEnvironment.Setup(m => m.ContentRootPath).Returns(Path.GetTempPath());

            // Mock NotificationService
            var mockNotificationLogger = new Mock<ILogger<NotificationService>>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockNotificationRepo = MockRepositories.CreateMockNotificationRepository();            var notificationService = new NotificationService(mockNotificationRepo.Object, mockDbContextFactory.Object, mockNotificationLogger.Object, mockScopeFactory.Object);

            // Mock UserPreferenceService
            var mockUserPreferenceLogger = new Mock<ILogger<UserPreferenceService>>();
            var userPreferenceService = new UserPreferenceService(mockDbContextFactory.Object, mockUserPreferenceLogger.Object);

            // Mock PostNotificationHelper
            var mockNotificationHelperLogger = new Mock<ILogger<PostNotificationHelper>>();
            var postNotificationHelper = new PostNotificationHelper(mockDbContextFactory.Object, notificationService, userPreferenceService, mockNotificationHelperLogger.Object);

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
                MockRepositories.CreateMockFeedPostRepository().Object,
                MockRepositories.CreateMockFeedCommentRepository().Object,
                MockRepositories.CreateMockFeedPostLikeRepository().Object,
                MockRepositories.CreateMockFeedCommentLikeRepository().Object,
                MockRepositories.CreateMockPinnedPostRepository().Object,
                MockRepositories.CreateMockPostNotificationMuteRepository().Object,
                MockRepositories.CreateMockNotificationRepository().Object,
                mockLogger.Object,
                siteSettingsOptions,
                mockEnvironment.Object,
                notificationService,
                userPreferenceService,
                postNotificationHelper
            );

            Services.AddSingleton(postService);
            Services.AddSingleton(siteSettingsOptions);
            Services.AddSingleton(userPreferenceService);
        }

        [Fact]
        public void FeedArticle_RendersAuthorName()
        {
            // Arrange
            var authorName = "John Doe";

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
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
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, likeCount)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.ShareCount, 0));

            // Assert
            cut.Find(".header-stat-count").TextContent.Should().Contain(likeCount.ToString());
        }

        [Fact]
        public void FeedArticle_RendersCommentCount()
        {
            // Arrange
            var commentCount = 15;

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, commentCount)
                .Add(p => p.ShareCount, 0));

            // Assert
            var statCounts = cut.FindAll(".header-stat-count");
            statCounts[1].TextContent.Should().Contain(commentCount.ToString());
        }

        [Fact]
        public void FeedArticle_RendersShareCount()
        {
            // Arrange
            var shareCount = 7;

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.ShareCount, shareCount));

            // Assert
            var statCounts = cut.FindAll(".header-stat-count");
            statCounts[2].TextContent.Should().Contain(shareCount.ToString());
        }

        [Fact]
        public void FeedArticle_RendersArticleContent()
        {
            // Arrange
            var contentText = "This is a test post content";

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
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
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
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
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
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
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert - Should show placeholder with initial instead of an img tag
            var avatar = cut.Find(".author-avatar");
            avatar.ClassList.Should().Contain("author-avatar-placeholder");
            var initial = cut.Find(".author-avatar-initial");
            initial.TextContent.Should().Contain("T"); // First letter of "Test User"
        }

        [Fact]
        public void FeedArticle_FormatsTimestamp()
        {
            // Arrange
            var pastDate = DateTime.Now.AddHours(-2);

            // Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, pastDate)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert - Should show relative time format (e.g., "2h", "3h", etc.)
            var timestamp = cut.Find(".article-timestamp");
            timestamp.TextContent.Should().MatchRegex(@"\d+h"); // Match any number followed by 'h'
        }

        [Fact]
        public void FeedArticle_HasMenuButton()
        {
            // Arrange & Act
            var cut = RenderComponent<FeedArticle>(parameters => parameters
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
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

