using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services;

public class PostNotificationMuteTests : TestBase
{
    private static IOptions<SiteSettings> CreateTestSiteSettings()
    {
        return Options.Create(new SiteSettings
        {
            SiteName = "TestSite",
            MaxFeedPostCommentDepth = 4,
            MaxFeedPostDirectCommentCount = 1000
        });
    }

    private static IWebHostEnvironment CreateMockWebHostEnvironment()
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(m => m.ContentRootPath).Returns(Path.GetTempPath());
        return mock.Object;
    }

    private static NotificationService CreateMockNotificationService()
    {
        var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        var logger = new Mock<ILogger<NotificationService>>();
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var notificationRepo = MockRepositories.CreateMockNotificationRepository();            return new NotificationService(notificationRepo.Object, dbFactory.Object, logger.Object, scopeFactory.Object, MockRepositories.CreateMockAuditLogRepository().Object);
    }

    private static UserPreferenceService CreateMockUserPreferenceService()
    {
        var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        var logger = new Mock<ILogger<UserPreferenceService>>();
        return new UserPreferenceService(dbFactory.Object, logger.Object);
    }

    private static PostNotificationHelper CreateMockPostNotificationHelper()
    {
        var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        var logger = new Mock<ILogger<PostNotificationHelper>>();
        return new PostNotificationHelper(dbFactory.Object, CreateMockNotificationService(), CreateMockUserPreferenceService(), logger.Object);
    }

    [Fact]
    public async Task MutePostNotificationsAsync_ShouldCreateMuteRecord()
    {
        // Arrange
        var dbFactory = CreateDbContextFactory("MuteTest1");
        var logger = CreateMockLogger<PostService>();
        var service = new PostService(dbFactory, MockRepositories.CreateMockFeedPostRepository().Object, MockRepositories.CreateMockFeedCommentRepository().Object, MockRepositories.CreateMockFeedPostLikeRepository().Object, MockRepositories.CreateMockFeedCommentLikeRepository().Object, MockRepositories.CreateMockPinnedPostRepository().Object, MockRepositories.CreateMockPostNotificationMuteRepository().Object, MockRepositories.CreateMockNotificationRepository().Object, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService(), CreateMockPostNotificationHelper(), MockRepositories.CreateMockAuditLogRepository().Object);

                var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@test.com" };
                using (var context = await dbFactory.CreateDbContextAsync())
                {
                    await context.Users.AddAsync(user);

                    var post = new Post
                    {
                        AuthorId = user.Id,
                        Content = "Test post",
                        CreatedAt = DateTime.UtcNow
                    };
                    await context.Posts.AddAsync(post);
                    await context.SaveChangesAsync();

                    // Act
                    var result = await service.MutePostNotificationsAsync(post.Id, user.Id);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            var mute = await context.PostNotificationMutes
                .FirstOrDefaultAsync(m => m.PostId == post.Id && m.UserId == user.Id);
            Assert.NotNull(mute);
            Assert.Equal(post.Id, mute.PostId);
            Assert.Equal(user.Id, mute.UserId);
        }
    }

    [Fact]
    public async Task MutePostNotificationsAsync_WhenAlreadyMuted_ShouldReturnSuccess()
    {
        // Arrange
        var dbFactory = CreateDbContextFactory("MuteTest2");
        var logger = CreateMockLogger<PostService>();
        var service = new PostService(dbFactory, MockRepositories.CreateMockFeedPostRepository().Object, MockRepositories.CreateMockFeedCommentRepository().Object, MockRepositories.CreateMockFeedPostLikeRepository().Object, MockRepositories.CreateMockFeedCommentLikeRepository().Object, MockRepositories.CreateMockPinnedPostRepository().Object, MockRepositories.CreateMockPostNotificationMuteRepository().Object, MockRepositories.CreateMockNotificationRepository().Object, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService(), CreateMockPostNotificationHelper(), MockRepositories.CreateMockAuditLogRepository().Object);

                using (var context = await dbFactory.CreateDbContextAsync())
                {
                    var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@test.com" };
                    await context.Users.AddAsync(user);

                    var post = new Post
                    {
                        AuthorId = user.Id,
                        Content = "Test post",
                        CreatedAt = DateTime.UtcNow
                    };
                    await context.Posts.AddAsync(post);

                    var existingMute = new PostNotificationMute
            {
                PostId = post.Id,
                UserId = user.Id
            };
            await context.PostNotificationMutes.AddAsync(existingMute);
            await context.SaveChangesAsync();

            var initialCount = await context.PostNotificationMutes.CountAsync();

            // Act
            var result = await service.MutePostNotificationsAsync(post.Id, user.Id);

            // Assert
            Assert.True(result.Success);
            var finalCount = await context.PostNotificationMutes.CountAsync();
            Assert.Equal(initialCount, finalCount); // No duplicate created
        }
    }

    [Fact]
    public async Task MutePostNotificationsAsync_WithInvalidPostId_ShouldReturnError()
    {
        // Arrange
        var dbFactory = CreateDbContextFactory("MuteTest3");
        var logger = CreateMockLogger<PostService>();
        var service = new PostService(dbFactory, MockRepositories.CreateMockFeedPostRepository().Object, MockRepositories.CreateMockFeedCommentRepository().Object, MockRepositories.CreateMockFeedPostLikeRepository().Object, MockRepositories.CreateMockFeedCommentLikeRepository().Object, MockRepositories.CreateMockPinnedPostRepository().Object, MockRepositories.CreateMockPostNotificationMuteRepository().Object, MockRepositories.CreateMockNotificationRepository().Object, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService(), CreateMockPostNotificationHelper(), MockRepositories.CreateMockAuditLogRepository().Object);

                var invalidPostId = 99999;
        var userId = "user1";

        // Act
        var result = await service.MutePostNotificationsAsync(invalidPostId, userId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Post not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task UnmutePostNotificationsAsync_ShouldRemoveMuteRecord()
    {
        // Arrange
        var dbFactory = CreateDbContextFactory("MuteTest4");
        var logger = CreateMockLogger<PostService>();
        var service = new PostService(dbFactory, MockRepositories.CreateMockFeedPostRepository().Object, MockRepositories.CreateMockFeedCommentRepository().Object, MockRepositories.CreateMockFeedPostLikeRepository().Object, MockRepositories.CreateMockFeedCommentLikeRepository().Object, MockRepositories.CreateMockPinnedPostRepository().Object, MockRepositories.CreateMockPostNotificationMuteRepository().Object, MockRepositories.CreateMockNotificationRepository().Object, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService(), CreateMockPostNotificationHelper(), MockRepositories.CreateMockAuditLogRepository().Object);

                using (var context = await dbFactory.CreateDbContextAsync())
                {
                    var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@test.com" };
                    await context.Users.AddAsync(user);

                    var post = new Post
                    {
                        AuthorId = user.Id,
                        Content = "Test post",
                        CreatedAt = DateTime.UtcNow
                    };
                    await context.Posts.AddAsync(post);

                    var mute = new PostNotificationMute
                    {
                        PostId = post.Id,
                        UserId = user.Id
                    };
                    await context.PostNotificationMutes.AddAsync(mute);
                    await context.SaveChangesAsync();

                    // Act
                    var result = await service.UnmutePostNotificationsAsync(post.Id, user.Id);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            var muteExists = await context.PostNotificationMutes
                .AnyAsync(m => m.PostId == post.Id && m.UserId == user.Id);
            Assert.False(muteExists);
        }
    }

    [Fact]
    public async Task UnmutePostNotificationsAsync_WhenNotMuted_ShouldReturnSuccess()
    {
        // Arrange
        var dbFactory = CreateDbContextFactory("MuteTest5");
        var logger = CreateMockLogger<PostService>();
        var service = new PostService(dbFactory, MockRepositories.CreateMockFeedPostRepository().Object, MockRepositories.CreateMockFeedCommentRepository().Object, MockRepositories.CreateMockFeedPostLikeRepository().Object, MockRepositories.CreateMockFeedCommentLikeRepository().Object, MockRepositories.CreateMockPinnedPostRepository().Object, MockRepositories.CreateMockPostNotificationMuteRepository().Object, MockRepositories.CreateMockNotificationRepository().Object, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService(), CreateMockPostNotificationHelper(), MockRepositories.CreateMockAuditLogRepository().Object);

                using (var context = await dbFactory.CreateDbContextAsync())
                {
                    var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@test.com" };
                    await context.Users.AddAsync(user);

                    var post = new Post
                    {
                        AuthorId = user.Id,
                        Content = "Test post",
                        CreatedAt = DateTime.UtcNow
                    };
                    await context.Posts.AddAsync(post);
                    await context.SaveChangesAsync();

                    // Act
                    var result = await service.UnmutePostNotificationsAsync(post.Id, user.Id);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }
    }

    [Fact]
    public async Task IsPostNotificationMutedAsync_WhenMuted_ShouldReturnTrue()
    {
        // Arrange
        var dbFactory = CreateDbContextFactory("MuteTest6");
        var logger = CreateMockLogger<PostService>();
        var service = new PostService(dbFactory, MockRepositories.CreateMockFeedPostRepository().Object, MockRepositories.CreateMockFeedCommentRepository().Object, MockRepositories.CreateMockFeedPostLikeRepository().Object, MockRepositories.CreateMockFeedCommentLikeRepository().Object, MockRepositories.CreateMockPinnedPostRepository().Object, MockRepositories.CreateMockPostNotificationMuteRepository().Object, MockRepositories.CreateMockNotificationRepository().Object, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService(), CreateMockPostNotificationHelper(), MockRepositories.CreateMockAuditLogRepository().Object);

                using (var context = await dbFactory.CreateDbContextAsync())
                {
                    var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@test.com" };
                    await context.Users.AddAsync(user);

                    var post = new Post
                    {
                        AuthorId = user.Id,
                        Content = "Test post",
                        CreatedAt = DateTime.UtcNow
                    };
                    await context.Posts.AddAsync(post);

                    var mute = new PostNotificationMute
                    {
                        PostId = post.Id,
                        UserId = user.Id
                    };
                    await context.PostNotificationMutes.AddAsync(mute);
                    await context.SaveChangesAsync();

                    // Act
                    var isMuted = await service.IsPostNotificationMutedAsync(post.Id, user.Id);

            // Assert
            Assert.True(isMuted);
        }
    }

    [Fact]
    public async Task IsPostNotificationMutedAsync_WhenNotMuted_ShouldReturnFalse()
    {
        // Arrange
        var dbFactory = CreateDbContextFactory("MuteTest7");
        var logger = CreateMockLogger<PostService>();
        var service = new PostService(dbFactory, MockRepositories.CreateMockFeedPostRepository().Object, MockRepositories.CreateMockFeedCommentRepository().Object, MockRepositories.CreateMockFeedPostLikeRepository().Object, MockRepositories.CreateMockFeedCommentLikeRepository().Object, MockRepositories.CreateMockPinnedPostRepository().Object, MockRepositories.CreateMockPostNotificationMuteRepository().Object, MockRepositories.CreateMockNotificationRepository().Object, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService(), CreateMockPostNotificationHelper(), MockRepositories.CreateMockAuditLogRepository().Object);

                using (var context = await dbFactory.CreateDbContextAsync())
                {
                    var user = new ApplicationUser { Id = "user1", UserName = "testuser", Email = "test@test.com" };
                    await context.Users.AddAsync(user);

                    var post = new Post
                    {
                        AuthorId = user.Id,
                        Content = "Test post",
                        CreatedAt = DateTime.UtcNow
                    };
                    await context.Posts.AddAsync(post);
                    await context.SaveChangesAsync();

                    // Act
                    var isMuted = await service.IsPostNotificationMutedAsync(post.Id, user.Id);

            // Assert
            Assert.False(isMuted);
        }
    }
}

