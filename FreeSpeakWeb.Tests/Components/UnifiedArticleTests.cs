using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.SocialFeed;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Components
{
    /// <summary>
    /// Tests for the UnifiedArticle component covering both UserPost and GroupPost scenarios.
    /// </summary>
    public class UnifiedArticleTests : TestContext
    {
        public UnifiedArticleTests()
        {
            // Setup JSInterop for modules used by UnifiedArticle
            JSInterop.SetupModule("./Components/SocialFeed/MultiLineCommentEditor.razor.js");
            JSInterop.SetupModule("./Components/SocialFeed/FeedArticle.razor.js");
            JSInterop.SetupModule("./Components/SocialFeed/FeedArticleImages.razor.js");

            // Register required services for UnifiedArticle component

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
            var mockNotificationRepo = MockRepositories.CreateMockNotificationRepository();            var notificationService = new NotificationService(mockNotificationRepo.Object, mockDbContextFactory.Object, mockNotificationLogger.Object, mockScopeFactory.Object, MockRepositories.CreateMockAuditLogRepository().Object);

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
                postNotificationHelper,
                MockRepositories.CreateMockAuditLogRepository().Object,
                MockRepositories.CreateMockFriendshipRepository().Object
            );

            // Mock GroupPostService for GroupPost tests
            // Since GroupPostService has many dependencies and we're not calling its methods in these tests,
            // we'll provide a null mock that satisfies the component's dependency injection requirements
            var mockGroupPostService = new Mock<GroupPostService>(
                MockBehavior.Loose,
                new object[] { null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null! });

            // Create GroupPointsService
            var mockGroupPointsLogger = new Mock<ILogger<GroupPointsService>>();
            var groupPointsService = new GroupPointsService(mockDbContextFactory.Object, mockGroupPointsLogger.Object);

            Services.AddSingleton(postService);
            Services.AddSingleton(mockGroupPostService.Object);
            Services.AddSingleton(siteSettingsOptions);
            Services.AddSingleton(userPreferenceService);
            Services.AddSingleton(groupPointsService);
            Services.AddSingleton(MockRepositories.CreateMockAuditLogRepository().Object);

            // Register FeedArticle localizer mock
            var mockFeedArticleLocalizer = new Mock<IStringLocalizer<FreeSpeakWeb.Resources.SocialFeed.FeedArticle>>();
            mockFeedArticleLocalizer.Setup(l => l[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));
            Services.AddSingleton(mockFeedArticleLocalizer.Object);

            // Register AlertService mock
            Services.AddSingleton<AlertService>();

            // Register UserManager mock required by child components
            var mockUserManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!);
            Services.AddSingleton(mockUserManager.Object);

            // Register TimeFormatting localizer mock required by TimestampFormattingService
            var mockTimeFormattingLocalizer = new Mock<IStringLocalizer<FreeSpeakWeb.Resources.Shared.TimeFormatting>>();
            mockTimeFormattingLocalizer.Setup(l => l[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));
            Services.AddSingleton(mockTimeFormattingLocalizer.Object);

            // Register FeedArticleHeader localizer mock
            var mockFeedArticleHeaderLocalizer = new Mock<IStringLocalizer<FreeSpeakWeb.Resources.SocialFeed.FeedArticleHeader>>();
            mockFeedArticleHeaderLocalizer.Setup(l => l[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));
            Services.AddSingleton(mockFeedArticleHeaderLocalizer.Object);

            // Register FeedArticleActions localizer mock
            var mockFeedArticleActionsLocalizer = new Mock<IStringLocalizer<FreeSpeakWeb.Resources.SocialFeed.FeedArticleActions>>();
            mockFeedArticleActionsLocalizer.Setup(l => l[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));
            Services.AddSingleton(mockFeedArticleActionsLocalizer.Object);

            // Register MultiLineCommentEditor localizer mock
            var mockMultiLineCommentEditorLocalizer = new Mock<IStringLocalizer<FreeSpeakWeb.Resources.SocialFeed.MultiLineCommentEditor>>();
            mockMultiLineCommentEditorLocalizer.Setup(l => l[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));
            Services.AddSingleton(mockMultiLineCommentEditorLocalizer.Object);

            // Register EmojiPicker localizer mock
            var mockEmojiPickerLocalizer = new Mock<IStringLocalizer<FreeSpeakWeb.Resources.Shared.EmojiPicker>>();
            mockEmojiPickerLocalizer.Setup(l => l[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));
            Services.AddSingleton(mockEmojiPickerLocalizer.Object);

            // Register TimestampFormattingService required by child components
            Services.AddSingleton<TimestampFormattingService>();
        }

        #region UserPost Tests

        [Fact]
        public void UnifiedArticle_UserPost_RendersAuthorName()
        {
            // Arrange
            var authorName = "John Doe";

            // Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.UserPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, authorName)
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.AudienceType, AudienceType.Public));

            // Assert
            cut.Find(".author-name").TextContent.Should().Be(authorName);
        }

        [Fact]
        public void UnifiedArticle_UserPost_RendersLikeCount()
        {
            // Arrange
            var likeCount = 42;

            // Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.UserPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, likeCount)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.ShareCount, 0)
                .Add(p => p.AudienceType, AudienceType.Public));

            // Assert
            cut.Find(".header-stat-count").TextContent.Should().Contain(likeCount.ToString());
        }

        [Fact]
        public void UnifiedArticle_UserPost_RendersCommentCount()
        {
            // Arrange
            var commentCount = 15;

            // Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.UserPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, commentCount)
                .Add(p => p.ShareCount, 0)
                .Add(p => p.AudienceType, AudienceType.Public));

            // Assert
            var statCounts = cut.FindAll(".header-stat-count");
            statCounts[1].TextContent.Should().Contain(commentCount.ToString());
        }

        [Fact]
        public void UnifiedArticle_UserPost_RendersShareCount()
        {
            // Arrange
            var shareCount = 7;

            // Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.UserPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.ShareCount, shareCount)
                .Add(p => p.AudienceType, AudienceType.Public));

            // Assert
            var statCounts = cut.FindAll(".header-stat-count");
            statCounts[2].TextContent.Should().Contain(shareCount.ToString());
        }

        #endregion

        #region GroupPost Tests

        [Fact]
        public void UnifiedArticle_GroupPost_RendersAuthorName()
        {
            // Arrange
            var authorName = "Jane Smith";
            var groupName = "Test Group";

            // Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.GroupPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.GroupId, 10)
                .Add(p => p.GroupName, groupName)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, authorName)
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert
            cut.Find(".author-name").TextContent.Should().Be(authorName);
        }

        [Fact]
        public void UnifiedArticle_GroupPost_RendersGroupName()
        {
            // Arrange
            var groupName = "Test Group";

            // Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.GroupPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.GroupId, 10)
                .Add(p => p.GroupName, groupName)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert
            cut.Markup.Should().Contain(groupName);
        }

        [Fact]
        public void UnifiedArticle_GroupPost_RendersAuthorGroupPoints()
        {
            // Arrange
            var authorPoints = 150;

            // Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.GroupPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.GroupId, 10)
                .Add(p => p.GroupName, "Test Group")
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.AuthorGroupPoints, authorPoints)
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0));

            // Assert
            cut.Markup.Should().Contain(authorPoints.ToString());
        }

        #endregion

        #region Common Tests

        [Fact]
        public void UnifiedArticle_RendersArticleContent()
        {
            // Arrange
            var contentText = "This is a test post content";

            // Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.UserPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.AudienceType, AudienceType.Public)
                .Add(p => p.ArticleContent, (RenderFragment)(builder =>
                {
                    builder.AddContent(0, contentText);
                })));

            // Assert
            cut.Find(".article-content").TextContent.Should().Contain(contentText);
        }

        [Fact]
        public void UnifiedArticle_HasActionButtons()
        {
            // Arrange & Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.UserPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.AudienceType, AudienceType.Public));

            // Assert
            var actionButtons = cut.FindAll(".action-button");
            actionButtons.Should().HaveCount(3); // Like, Comment, Share

            actionButtons[0].TextContent.Trim().Should().Contain("Like");
            actionButtons[1].TextContent.Trim().Should().Contain("Comment");
            actionButtons[2].TextContent.Trim().Should().Contain("Share");
        }

        [Fact]
        public void UnifiedArticle_DisplaysAuthorAvatar()
        {
            // Arrange
            var avatarUrl = "https://example.com/avatar.jpg";

            // Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.UserPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.AuthorImageUrl, avatarUrl)
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.AudienceType, AudienceType.Public));

            // Assert
            var avatar = cut.Find(".author-avatar");
            avatar.GetAttribute("src").Should().Be(avatarUrl);
        }

        [Fact]
        public void UnifiedArticle_DisplaysDefaultAvatarWhenNotProvided()
        {
            // Arrange & Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.UserPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.AudienceType, AudienceType.Public));

            // Assert - Should show placeholder with initial instead of an img tag
            var avatar = cut.Find(".author-avatar");
            avatar.ClassList.Should().Contain("author-avatar-placeholder");
            var initial = cut.Find(".author-avatar-initial");
            initial.TextContent.Should().Contain("T"); // First letter of "Test User"
        }

        [Fact]
        public void UnifiedArticle_FormatsTimestamp()
        {
            // Arrange
            var pastDate = DateTime.Now.AddHours(-2);

            // Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.UserPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, pastDate)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.AudienceType, AudienceType.Public));

            // Assert - Should show relative time format (e.g., "2h", "3h", etc.)
            var timestamp = cut.Find(".article-timestamp");
            timestamp.TextContent.Should().MatchRegex(@"\d+h"); // Match any number followed by 'h'
        }

        [Fact]
        public void UnifiedArticle_HasMenuButton()
        {
            // Arrange & Act
            var cut = RenderComponent<UnifiedArticle>(parameters => parameters
                .Add(p => p.ArticlePostType, PostType.UserPost)
                .Add(p => p.PostId, 1)
                .Add(p => p.AuthorId, "test-user-id")
                .Add(p => p.AuthorName, "Test User")
                .Add(p => p.CreatedAt, DateTime.Now)
                .Add(p => p.LikeCount, 0)
                .Add(p => p.CommentCount, 0)
                .Add(p => p.AudienceType, AudienceType.Public));

            // Assert
            var menuButton = cut.Find(".menu-button");
            menuButton.Should().NotBeNull();
        }

        #endregion
    }
}

