using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.IntegrationTests.Infrastructure;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace FreeSpeakWeb.IntegrationTests.Services
{
    public class PostServiceIntegrationTests : IntegrationTestBase
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

        private class TestWebHostEnvironment : IWebHostEnvironment
        {
            public string WebRootPath { get; set; } = Path.GetTempPath();
            public IFileProvider WebRootFileProvider { get; set; } = null!;
            public string ApplicationName { get; set; } = "TestApp";
            public IFileProvider ContentRootFileProvider { get; set; } = null!;
            public string ContentRootPath { get; set; } = Path.GetTempPath();
            public string EnvironmentName { get; set; } = "Test";
        }

        private static IWebHostEnvironment CreateMockWebHostEnvironment()
        {
            return new TestWebHostEnvironment();
        }

        private static NotificationService CreateMockNotificationService()
        {
            // Create a no-op notification service for testing
            // We're testing PostService, not notifications
            return new NullNotificationService();
        }

        private static UserPreferenceService CreateMockUserPreferenceService()
        {
            // Create a no-op user preference service for testing
            return new NullUserPreferenceService();
        }

        private static PostNotificationHelper CreateMockPostNotificationHelper()
        {
            // Create a no-op post notification helper for testing
            return new NullPostNotificationHelper();
        }

        // Null implementation of PostNotificationHelper for testing
        private class NullPostNotificationHelper : PostNotificationHelper
        {
            public NullPostNotificationHelper()
                : base(new NullDbContextFactory(), new NullNotificationService(), new NullUserPreferenceService(), new NullLogger<PostNotificationHelper>())
            {
            }

            private class NullDbContextFactory : IDbContextFactory<ApplicationDbContext>
            {
                public ApplicationDbContext CreateDbContext() => null!;
            }

            private class NullLogger<T> : ILogger<T>
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => false;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
            }
        }

        // Null implementation of UserPreferenceService for testing
        private class NullUserPreferenceService : UserPreferenceService
        {
            public NullUserPreferenceService()
                : base(new NullDbContextFactory(), new NullLogger<UserPreferenceService>())
            {
            }

            private class NullDbContextFactory : IDbContextFactory<ApplicationDbContext>
            {
                public ApplicationDbContext CreateDbContext() => null!;
            }

            private class NullLogger<T> : ILogger<T>
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => false;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
            }
        }

        // Null implementation of NotificationService for testing
        private class NullNotificationService : NotificationService
        {
            public NullNotificationService() 
                : base(null!, new NullDbContextFactory(), new NullLogger(), new NullServiceScopeFactory())
            {
            }

            private class NullDbContextFactory : IDbContextFactory<ApplicationDbContext>
            {
                public ApplicationDbContext CreateDbContext() => null!;
                public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<ApplicationDbContext>(null!);
            }

            private class NullServiceScopeFactory : IServiceScopeFactory
            {
                public IServiceScope CreateScope() => new NullServiceScope();
            }

            private class NullServiceScope : IServiceScope
            {
                public IServiceProvider ServiceProvider => null!;
                public void Dispose() { }
            }

            private class NullLogger : ILogger<NotificationService>
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => false;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
            }
        }

        [Fact]
        public async Task GetPostByIdAsync_WithNestedComments_ShouldLoadAllData()
        {
            // Arrange
            var factory = CreateDbContextFactory();
            var logger = CreateLogger<PostService>();
            var service = new PostService(factory, null!, null!, null!, null!, null!, null!, null!, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService(), CreateMockPostNotificationHelper());

            var author = CreateTestUser("author1", "author", "Post", "Author");
            var commenter1 = CreateTestUser("commenter1", "commenter1", "First", "Commenter");
            var commenter2 = CreateTestUser("commenter2", "commenter2", "Second", "Commenter");

            int postId, parentCommentId;

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, commenter1, commenter2);
                
                var post = new Post
                {
                    AuthorId = "author1",
                    Content = "Test post with nested comments",
                    CreatedAt = DateTime.UtcNow
                };
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                postId = post.Id;

                // Add parent comment
                var parentComment = new Comment
                {
                    PostId = postId,
                    AuthorId = "commenter1",
                    Content = "Parent comment",
                    CreatedAt = DateTime.UtcNow
                };
                context.Comments.Add(parentComment);
                await context.SaveChangesAsync();

                parentCommentId = parentComment.Id;

                // Add reply to parent comment
                var replyComment = new Comment
                {
                    PostId = postId,
                    AuthorId = "commenter2",
                    Content = "Reply to parent",
                    ParentCommentId = parentCommentId,
                    CreatedAt = DateTime.UtcNow
                };
                context.Comments.Add(replyComment);

                // Add images
                context.PostImages.Add(new PostImage
                {
                    PostId = postId,
                    ImageUrl = "https://example.com/image1.jpg",
                    DisplayOrder = 0,
                    UploadedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
            }

            // Act
            var result = await service.GetPostByIdAsync(postId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(postId);
            result.Author.Should().NotBeNull();
            result.Author.UserName.Should().Be("author");
            result.Images.Should().HaveCount(1);
            result.Comments.Should().HaveCount(1); // Only top-level comments
            result.Comments.First().Replies.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetFeedPostsAsync_WithComplexRelationships_ShouldReturnCorrectPosts()
        {
            // Arrange
            var factory = CreateDbContextFactory();
            var logger = CreateLogger<PostService>();
            var service = new PostService(factory, null!, null!, null!, null!, null!, null!, null!, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService(), CreateMockPostNotificationHelper());

            var user1 = CreateTestUser("user1", "user1", "User", "One");
            var user2 = CreateTestUser("user2", "user2", "User", "Two");
            var user3 = CreateTestUser("user3", "user3", "User", "Three");
            var stranger = CreateTestUser("stranger", "stranger", "Stranger", "User");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(user1, user2, user3, stranger);

                // User1 is friends with User2 and User3
                context.Friendships.Add(new Friendship
                {
                    RequesterId = "user1",
                    AddresseeId = "user2",
                    Status = FriendshipStatus.Accepted,
                    RequestedAt = DateTime.UtcNow
                });

                context.Friendships.Add(new Friendship
                {
                    RequesterId = "user3",
                    AddresseeId = "user1",
                    Status = FriendshipStatus.Accepted,
                    RequestedAt = DateTime.UtcNow
                });

                // Create posts
                context.Posts.Add(new Post { AuthorId = "user1", Content = "User1 post", CreatedAt = DateTime.UtcNow.AddHours(-1) });
                context.Posts.Add(new Post { AuthorId = "user2", Content = "User2 post", CreatedAt = DateTime.UtcNow.AddHours(-2) });
                context.Posts.Add(new Post { AuthorId = "user3", Content = "User3 post", CreatedAt = DateTime.UtcNow.AddHours(-3) });
                context.Posts.Add(new Post { AuthorId = "stranger", Content = "Stranger post", CreatedAt = DateTime.UtcNow });

                await context.SaveChangesAsync();
            }

            // Act
            var feedPosts = await service.GetFeedPostsAsync("user1");

            // Assert
            feedPosts.Should().HaveCount(3);
            feedPosts.Should().Contain(p => p.AuthorId == "user1");
            feedPosts.Should().Contain(p => p.AuthorId == "user2");
            feedPosts.Should().Contain(p => p.AuthorId == "user3");
            feedPosts.Should().NotContain(p => p.AuthorId == "stranger");
            
            // Should be ordered by CreatedAt descending
            feedPosts.First().AuthorId.Should().Be("user1"); // Most recent
        }

        [Fact]
        public async Task CreatePostAsync_WithImagesAndThenAddComment_ShouldMaintainIntegrity()
        {
            // Arrange
            var factory = CreateDbContextFactory();
            var logger = CreateLogger<PostService>();
            var service = new PostService(factory, null!, null!, null!, null!, null!, null!, null!, logger, CreateTestSiteSettings(), CreateMockWebHostEnvironment(), CreateMockNotificationService(), CreateMockUserPreferenceService(), CreateMockPostNotificationHelper());

            var author = CreateTestUser("author1", "author", "Post", "Author");
            var commenter = CreateTestUser("commenter1", "commenter", "Comment", "Author");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, commenter);
                await context.SaveChangesAsync();
            }

            var imageUrls = new List<string> { "image1.jpg", "image2.jpg", "image3.jpg" };

            // Act
            var (success, errorMessage, post) = await service.CreatePostAsync("author1", "Post with images", AudienceType.Public, imageUrls);

            // Assert - Post creation
            success.Should().BeTrue();
            post.Should().NotBeNull();

            await using (var context = CreateDbContext())
            {
                var savedPost = await context.Posts
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == post!.Id);

                savedPost.Should().NotBeNull();
                savedPost!.Images.Should().HaveCount(3);
                savedPost.Images.Should().BeInAscendingOrder(i => i.DisplayOrder);
            }

            // Act - Add comment
            var (commentSuccess, commentError, comment) = await service.AddCommentAsync(
                post!.Id, "commenter1", "Great post!");

            // Assert - Comment creation
            commentSuccess.Should().BeTrue();
            comment.Should().NotBeNull();

            await using (var context = CreateDbContext())
            {
                var updatedPost = await context.Posts.FindAsync(post.Id);
                updatedPost!.CommentCount.Should().Be(1);
            }
        }

        private ApplicationUser CreateTestUser(string id, string userName, string firstName, string lastName)
        {
            return new ApplicationUser
            {
                Id = id,
                UserName = userName,
                NormalizedUserName = userName.ToUpper(),
                Email = $"{userName}@example.com",
                NormalizedEmail = $"{userName.ToUpper()}@EXAMPLE.COM",
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                SecurityStamp = Guid.NewGuid().ToString()
            };
        }
    }
}



