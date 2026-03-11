using FluentAssertions;
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

namespace FreeSpeakWeb.Tests.Services
{
    public class PostServiceEdgeCaseTests : TestBase
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
            return new NotificationService(dbFactory.Object, logger.Object, scopeFactory.Object);
        }

        private static UserPreferenceService CreateMockUserPreferenceService()
        {
            var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            var logger = new Mock<ILogger<UserPreferenceService>>();
            return new UserPreferenceService(dbFactory.Object, logger.Object);
        }

        [Fact]
        public async Task CreatePostAsync_WithWhitespaceOnly_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("EdgeCase1");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService());

            // Act
            var (success, errorMessage, post) = await service.CreatePostAsync("user1", "   ");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("Post must contain either text or images");
            post.Should().BeNull();
        }

        [Fact]
        public async Task UpdatePostAsync_WithNonExistentPost_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("EdgeCase2");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService());

            // Act
            var (success, errorMessage, _) = await service.UpdatePostAsync(99999, "user1", "Updated content");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not found");
        }

        [Fact]
        public async Task DeletePostAsync_WithNonExistentPost_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("EdgeCase3");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService());

            // Act
            var (success, errorMessage) = await service.DeletePostAsync(99999, "user1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not found");
        }

        [Fact]
        public async Task AddCommentAsync_ToNonExistentPost_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("EdgeCase4");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService());

            // Act
            var (success, errorMessage, comment) = await service.AddCommentAsync(99999, "user1", "Comment");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not found");
            comment.Should().BeNull();
        }

        [Fact]
        public async Task AddCommentAsync_WithInvalidParentComment_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("EdgeCase5");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage, comment) = await service.AddCommentAsync(
                postId, "user1", "Reply", parentCommentId: 99999);

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("Parent comment not found");
        }

        [Fact]
        public async Task ToggleLikeAsync_OnNonExistentPost_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("EdgeCase6");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService());

            // Act
            var (success, errorMessage, isLiked) = await service.ToggleLikeAsync(99999, "user1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not found");
        }

        [Fact]
        public async Task GetPostsByUserAsync_WithPagination_ShouldReturnCorrectPage()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("EdgeCase7");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                
                // Create 25 posts
                for (int i = 1; i <= 25; i++)
                {
                    context.Posts.Add(TestDataFactory.CreateTestPost("user1", $"Post {i}"));
                }
                await context.SaveChangesAsync();
            }

            // Act - Get page 2 with 10 items per page
            var posts = await service.GetPostsByUserAsync("user1", pageSize: 10, pageNumber: 2);

            // Assert
            posts.Should().HaveCount(10);
        }

        [Fact]
        public async Task DeleteCommentAsync_WithReplies_ShouldDeleteAllReplies()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("EdgeCase8");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 3);

            int postId, parentCommentId;

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                postId = context.Posts.First().Id;

                // Create parent comment
                var parentComment = new Comment
                {
                    PostId = postId,
                    AuthorId = "user1",
                    Content = "Parent",
                    CreatedAt = DateTime.UtcNow
                };
                context.Comments.Add(parentComment);
                await context.SaveChangesAsync();

                parentCommentId = parentComment.Id;

                // Create two replies
                context.Comments.Add(new Comment
                {
                    PostId = postId,
                    AuthorId = "user1",
                    Content = "Reply 1",
                    ParentCommentId = parentCommentId,
                    CreatedAt = DateTime.UtcNow
                });
                context.Comments.Add(new Comment
                {
                    PostId = postId,
                    AuthorId = "user1",
                    Content = "Reply 2",
                    ParentCommentId = parentCommentId,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            // Act - Delete parent comment
            var (success, errorMessage) = await service.DeleteCommentAsync(parentCommentId, "user1");

            // Assert
            success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingComments = context.Comments.Count();
                remainingComments.Should().Be(0); // All should be deleted

                var updatedPost = await context.Posts.FindAsync(postId);
                updatedPost!.CommentCount.Should().Be(0); // Should be updated
            }
        }

        [Fact]
        public async Task AddImageToPostAsync_MultipleImages_ShouldIncrementDisplayOrder()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("EdgeCase9");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                postId = context.Posts.First().Id;
            }

            // Act - Add three images
            await service.AddImageToPostAsync(postId, "image1.jpg", "user1");
            await service.AddImageToPostAsync(postId, "image2.jpg", "user1");
            await service.AddImageToPostAsync(postId, "image3.jpg", "user1");

            // Assert
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var images = context.PostImages
                    .Where(pi => pi.PostId == postId)
                    .OrderBy(pi => pi.DisplayOrder)
                    .ToList();

                images.Should().HaveCount(3);
                images[0].DisplayOrder.Should().Be(0);
                images[1].DisplayOrder.Should().Be(1);
                images[2].DisplayOrder.Should().Be(2);
            }
        }

        [Fact]
        public async Task GetCommentsAsync_ShouldOrderByCreatedAtAscending()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("EdgeCase10");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                postId = context.Posts.First().Id;

                // Add comments in reverse order
                context.Comments.Add(new Comment
                {
                    PostId = postId,
                    AuthorId = "user1",
                    Content = "Third comment",
                    CreatedAt = DateTime.UtcNow.AddMinutes(2)
                });
                context.Comments.Add(new Comment
                {
                    PostId = postId,
                    AuthorId = "user1",
                    Content = "First comment",
                    CreatedAt = DateTime.UtcNow
                });
                context.Comments.Add(new Comment
                {
                    PostId = postId,
                    AuthorId = "user1",
                    Content = "Second comment",
                    CreatedAt = DateTime.UtcNow.AddMinutes(1)
                });
                await context.SaveChangesAsync();
            }

            // Act
            var comments = await service.GetCommentsAsync(postId);

            // Assert
            comments.Should().HaveCount(3);
            comments[0].Content.Should().Be("First comment");
            comments[1].Content.Should().Be("Second comment");
            comments[2].Content.Should().Be("Third comment");
        }
    }
}

