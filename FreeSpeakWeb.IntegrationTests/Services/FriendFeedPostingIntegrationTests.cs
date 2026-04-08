using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.IntegrationTests.Infrastructure;
using FreeSpeakWeb.Repositories;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace FreeSpeakWeb.IntegrationTests.Services
{
    /// <summary>
    /// Integration tests for the friend feed (cross-feed) posting feature.
    /// Validates creating posts on a friend's feed, visibility filtering, and edge cases.
    /// </summary>
    public class FriendFeedPostingIntegrationTests : IntegrationTestBase
    {
        #region Test Infrastructure

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
            return new NullNotificationService();
        }

        private static UserPreferenceService CreateMockUserPreferenceService()
        {
            return new NullUserPreferenceService();
        }

        private static PostNotificationHelper CreateMockPostNotificationHelper()
        {
            return new NullPostNotificationHelper();
        }

        private FriendshipRepository CreateFriendshipRepository()
        {
            var factory = CreateDbContextFactory();
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FriendshipRepository>();
            var profilerSettings = Options.Create(new ProfilingSettings { Enabled = false });
            var profiler = new ProfilerHelper(profilerSettings);
            return new FriendshipRepository(factory, logger, profiler);
        }

        private FriendshipCacheService CreateFriendshipCacheService()
        {
            var factory = CreateDbContextFactory();
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FriendshipCacheService>();
            var cache = new MemoryCache(new MemoryCacheOptions());
            return new FriendshipCacheService(cache, factory, logger);
        }

        private PostRepository CreatePostRepository()
        {
            var factory = CreateDbContextFactory();
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PostRepository>();
            var profilerSettings = Options.Create(new ProfilingSettings { Enabled = false });
            var profiler = new ProfilerHelper(profilerSettings);
            var friendshipCache = CreateFriendshipCacheService();
            return new PostRepository(factory, logger, friendshipCache, profiler);
        }

        /// <summary>
        /// Creates a PostService instance configured for friend feed integration testing.
        /// Uses real FriendshipRepository and PostRepository to test actual database interactions.
        /// </summary>
        private PostService CreatePostService()
        {
            var factory = CreateDbContextFactory();
            var logger = CreateLogger<PostService>();
            var friendshipRepo = CreateFriendshipRepository();
            var postRepo = CreatePostRepository();

            return new PostService(
                factory,
                postRepo,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                logger,
                CreateTestSiteSettings(),
                CreateMockWebHostEnvironment(),
                CreateMockNotificationService(),
                CreateMockUserPreferenceService(),
                CreateMockPostNotificationHelper(),
                MockRepositories.CreateMockAuditLogRepository().Object,
                friendshipRepo);
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

        /// <summary>
        /// Seeds an accepted friendship between two users in the database.
        /// </summary>
        private async Task SeedAcceptedFriendshipAsync(string requesterId, string addresseeId)
        {
            await using var context = CreateDbContext();
            context.Friendships.Add(new Friendship
            {
                RequesterId = requesterId,
                AddresseeId = addresseeId,
                Status = FriendshipStatus.Accepted,
                RequestedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Seeds a pending friendship between two users in the database.
        /// </summary>
        private async Task SeedPendingFriendshipAsync(string requesterId, string addresseeId)
        {
            await using var context = CreateDbContext();
            context.Friendships.Add(new Friendship
            {
                RequesterId = requesterId,
                AddresseeId = addresseeId,
                Status = FriendshipStatus.Pending,
                RequestedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        #region Null Service Implementations

        private class NullPostNotificationHelper : PostNotificationHelper
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="NullPostNotificationHelper"/> class.
            /// </summary>
            public NullPostNotificationHelper()
                : base(new NullDbContextFactory(), new NullNotificationService(), new NullUserPreferenceService(), new NullLogger<PostNotificationHelper>())
            {
            }

            private class NullDbContextFactory : IDbContextFactory<ApplicationDbContext>
            {
                /// <summary>
                /// Creates a null database context (not usable).
                /// </summary>
                public ApplicationDbContext CreateDbContext() => null!;
            }

            private class NullLogger<T> : ILogger<T>
            {
                /// <summary>
                /// Returns null (no scope).
                /// </summary>
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                /// <summary>
                /// Always returns false.
                /// </summary>
                public bool IsEnabled(LogLevel logLevel) => false;

                /// <summary>
                /// No-op log method.
                /// </summary>
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
            }
        }

        private class NullUserPreferenceService : UserPreferenceService
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="NullUserPreferenceService"/> class.
            /// </summary>
            public NullUserPreferenceService()
                : base(new NullDbContextFactory(), new NullLogger<UserPreferenceService>())
            {
            }

            private class NullDbContextFactory : IDbContextFactory<ApplicationDbContext>
            {
                /// <summary>
                /// Creates a null database context (not usable).
                /// </summary>
                public ApplicationDbContext CreateDbContext() => null!;
            }

            private class NullLogger<T> : ILogger<T>
            {
                /// <summary>
                /// Returns null (no scope).
                /// </summary>
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                /// <summary>
                /// Always returns false.
                /// </summary>
                public bool IsEnabled(LogLevel logLevel) => false;

                /// <summary>
                /// No-op log method.
                /// </summary>
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
            }
        }

        private class NullNotificationService : NotificationService
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="NullNotificationService"/> class.
            /// </summary>
            public NullNotificationService()
                : base(null!, new NullDbContextFactory(), new NullLogger(), new NullServiceScopeFactory(), MockRepositories.CreateMockAuditLogRepository().Object)
            {
            }

            private class NullDbContextFactory : IDbContextFactory<ApplicationDbContext>
            {
                /// <summary>
                /// Creates a null database context (not usable).
                /// </summary>
                public ApplicationDbContext CreateDbContext() => null!;

                /// <summary>
                /// Creates a null database context asynchronously (not usable).
                /// </summary>
                public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<ApplicationDbContext>(null!);
            }

            private class NullServiceScopeFactory : IServiceScopeFactory
            {
                /// <summary>
                /// Creates a null service scope.
                /// </summary>
                public IServiceScope CreateScope() => new NullServiceScope();
            }

            private class NullServiceScope : IServiceScope
            {
                /// <summary>
                /// Gets the service provider (null).
                /// </summary>
                public IServiceProvider ServiceProvider => null!;

                /// <summary>
                /// No-op dispose.
                /// </summary>
                public void Dispose() { }
            }

            private class NullLogger : ILogger<NotificationService>
            {
                /// <summary>
                /// Returns null (no scope).
                /// </summary>
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                /// <summary>
                /// Always returns false.
                /// </summary>
                public bool IsEnabled(LogLevel logLevel) => false;

                /// <summary>
                /// No-op log method.
                /// </summary>
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
            }
        }

        #endregion

        #endregion

        #region Create Post on Friend Feed Tests

        /// <summary>
        /// Verifies that a post can be created on a friend's feed when an accepted friendship exists.
        /// </summary>
        [Fact]
        public async Task CreatePostOnFriendFeed_WithValidFriendship_ShouldSucceed()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var friend = CreateTestUser("friend1", "friend", "Bob", "Friend");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, friend);
                await context.SaveChangesAsync();
            }

            await SeedAcceptedFriendshipAsync("author1", "friend1");

            // Act
            var (success, errorMessage, post) = await service.CreatePostAsync(
                "author1", "Hello from Alice!", AudienceType.FriendsOnly, null, "friend1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            post.Should().NotBeNull();
            post!.AuthorId.Should().Be("author1");
            post.FriendId.Should().Be("friend1");
            post.Content.Should().Be("Hello from Alice!");
            post.AudienceType.Should().Be(AudienceType.FriendsOnly);
        }

        /// <summary>
        /// Verifies that creating a post on a non-friend's feed fails with an appropriate error.
        /// </summary>
        [Fact]
        public async Task CreatePostOnFriendFeed_WithNoFriendship_ShouldFail()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var stranger = CreateTestUser("stranger1", "stranger", "Charlie", "Stranger");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, stranger);
                await context.SaveChangesAsync();
            }

            // Act - no friendship seeded
            var (success, errorMessage, post) = await service.CreatePostAsync(
                "author1", "Trying to post on stranger's feed", AudienceType.Public, null, "stranger1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().NotBeNullOrEmpty();
            post.Should().BeNull();
        }

        /// <summary>
        /// Verifies that creating a post on a friend's feed fails when the friendship is still pending.
        /// </summary>
        [Fact]
        public async Task CreatePostOnFriendFeed_WithPendingFriendship_ShouldFail()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var pendingFriend = CreateTestUser("pending1", "pendingfriend", "Dave", "Pending");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, pendingFriend);
                await context.SaveChangesAsync();
            }

            await SeedPendingFriendshipAsync("author1", "pending1");

            // Act
            var (success, errorMessage, post) = await service.CreatePostAsync(
                "author1", "Trying to post on pending friend's feed", AudienceType.Public, null, "pending1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("accepted");
            post.Should().BeNull();
        }

        /// <summary>
        /// Verifies that a friend feed post is persisted with the correct FriendId in the database.
        /// </summary>
        [Fact]
        public async Task CreatePostOnFriendFeed_ShouldPersistFriendIdInDatabase()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var friend = CreateTestUser("friend1", "friend", "Bob", "Friend");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, friend);
                await context.SaveChangesAsync();
            }

            await SeedAcceptedFriendshipAsync("author1", "friend1");

            // Act
            var (success, _, post) = await service.CreatePostAsync(
                "author1", "Cross-feed post content", AudienceType.FriendsOnly, null, "friend1");

            // Assert
            success.Should().BeTrue();

            await using (var context = CreateDbContext())
            {
                var savedPost = await context.Posts.FirstOrDefaultAsync(p => p.Id == post!.Id);
                savedPost.Should().NotBeNull();
                savedPost!.FriendId.Should().Be("friend1");
                savedPost.AuthorId.Should().Be("author1");
                savedPost.Content.Should().Be("Cross-feed post content");
            }
        }

        #endregion

        #region Feed Visibility Tests

        /// <summary>
        /// Verifies that a friend feed post appears on the friend's profile feed (via GetByAuthorWithAudienceFilterAsync).
        /// </summary>
        [Fact]
        public async Task FriendFeedPost_ShouldAppearOnFriendProfileFeed()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var friend = CreateTestUser("friend1", "friend", "Bob", "Friend");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, friend);
                await context.SaveChangesAsync();
            }

            await SeedAcceptedFriendshipAsync("author1", "friend1");

            // Act - Create a cross-feed post
            var (success, _, _) = await service.CreatePostAsync(
                "author1", "Post on Bob's feed", AudienceType.FriendsOnly, null, "friend1");
            success.Should().BeTrue();

            // Get the friend's profile feed as viewed by the author
            var friendProfilePosts = await service.GetPostsByUserWithAudienceFilterAsync("friend1", "author1");

            // Assert - The cross-feed post should appear on the friend's profile
            friendProfilePosts.Should().ContainSingle();
            friendProfilePosts.First().AuthorId.Should().Be("author1");
            friendProfilePosts.First().FriendId.Should().Be("friend1");
        }

        /// <summary>
        /// Verifies that a friend feed post also appears in the author's own feed.
        /// </summary>
        [Fact]
        public async Task FriendFeedPost_ShouldAppearInAuthorFeed()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var friend = CreateTestUser("friend1", "friend", "Bob", "Friend");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, friend);
                await context.SaveChangesAsync();
            }

            await SeedAcceptedFriendshipAsync("author1", "friend1");

            // Act
            var (success, _, _) = await service.CreatePostAsync(
                "author1", "Cross-feed post for both feeds", AudienceType.FriendsOnly, null, "friend1");
            success.Should().BeTrue();

            // Get author's feed
            var authorFeed = await service.GetFeedPostsAsync("author1");

            // Assert - Post should appear in author's feed
            authorFeed.Should().ContainSingle();
            authorFeed.First().AuthorId.Should().Be("author1");
            authorFeed.First().FriendId.Should().Be("friend1");
        }

        /// <summary>
        /// Verifies that a friend feed post appears in both the author's and the friend's feeds.
        /// </summary>
        [Fact]
        public async Task FriendFeedPost_ShouldAppearInBothAuthorAndFriendFeeds()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var friend = CreateTestUser("friend1", "friend", "Bob", "Friend");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, friend);
                await context.SaveChangesAsync();
            }

            await SeedAcceptedFriendshipAsync("author1", "friend1");

            // Act
            var (success, _, _) = await service.CreatePostAsync(
                "author1", "Visible on both feeds", AudienceType.FriendsOnly, null, "friend1");
            success.Should().BeTrue();

            // Get both feeds
            var authorFeed = await service.GetFeedPostsAsync("author1");
            var friendFeed = await service.GetFeedPostsAsync("friend1");

            // Assert - Post should appear in both feeds
            authorFeed.Should().ContainSingle(p => p.Content == "Visible on both feeds");
            friendFeed.Should().ContainSingle(p => p.Content == "Visible on both feeds");
        }

        /// <summary>
        /// Verifies that FriendId filtering correctly distinguishes cross-feed posts
        /// from regular posts on the friend's profile.
        /// </summary>
        [Fact]
        public async Task FriendFeedPost_FriendIdFiltering_ShouldDistinguishCrossFeedPosts()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var friend = CreateTestUser("friend1", "friend", "Bob", "Friend");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, friend);
                await context.SaveChangesAsync();
            }

            await SeedAcceptedFriendshipAsync("author1", "friend1");

            // Create a regular post by the friend (no FriendId)
            var (success1, _, _) = await service.CreatePostAsync(
                "friend1", "Bob's own post", AudienceType.FriendsOnly);
            success1.Should().BeTrue();

            // Create a cross-feed post by author on friend's feed
            var (success2, _, _) = await service.CreatePostAsync(
                "author1", "Alice's post on Bob's feed", AudienceType.FriendsOnly, null, "friend1");
            success2.Should().BeTrue();

            // Act - Get friend's profile feed
            var friendProfilePosts = await service.GetPostsByUserWithAudienceFilterAsync("friend1", "author1");

            // Assert
            friendProfilePosts.Should().HaveCount(2);

            var regularPost = friendProfilePosts.FirstOrDefault(p => p.FriendId == null);
            regularPost.Should().NotBeNull();
            regularPost!.AuthorId.Should().Be("friend1");
            regularPost.Content.Should().Be("Bob's own post");

            var crossFeedPost = friendProfilePosts.FirstOrDefault(p => p.FriendId != null);
            crossFeedPost.Should().NotBeNull();
            crossFeedPost!.AuthorId.Should().Be("author1");
            crossFeedPost.FriendId.Should().Be("friend1");
            crossFeedPost.Content.Should().Be("Alice's post on Bob's feed");
        }

        #endregion

        #region Edge Case Tests

        /// <summary>
        /// Verifies that creating a post without a FriendId (regular post) still works correctly and has null FriendId.
        /// </summary>
        [Fact]
        public async Task CreatePost_WithoutFriendId_ShouldHaveNullFriendId()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");

            await using (var context = CreateDbContext())
            {
                context.Users.Add(author);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, _, post) = await service.CreatePostAsync(
                "author1", "Regular post without friend", AudienceType.Public);

            // Assert
            success.Should().BeTrue();
            post.Should().NotBeNull();
            post!.FriendId.Should().BeNull();
        }

        /// <summary>
        /// Verifies that a cross-feed post with images is created correctly on the friend's feed.
        /// </summary>
        [Fact]
        public async Task CreatePostOnFriendFeed_WithImages_ShouldSucceed()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var friend = CreateTestUser("friend1", "friend", "Bob", "Friend");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, friend);
                await context.SaveChangesAsync();
            }

            await SeedAcceptedFriendshipAsync("author1", "friend1");

            var imageUrls = new List<string> { "image1.jpg", "image2.jpg" };

            // Act
            var (success, errorMessage, post) = await service.CreatePostAsync(
                "author1", "Photo post on friend's feed", AudienceType.FriendsOnly, imageUrls, "friend1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            post.Should().NotBeNull();
            post!.FriendId.Should().Be("friend1");

            await using (var context = CreateDbContext())
            {
                var savedPost = await context.Posts
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == post.Id);

                savedPost.Should().NotBeNull();
                savedPost!.Images.Should().HaveCount(2);
                savedPost.FriendId.Should().Be("friend1");
            }
        }

        /// <summary>
        /// Verifies that a third-party user who is friends with the author can see a public
        /// cross-feed post, while a stranger cannot see a friends-only cross-feed post from
        /// their own feed perspective.
        /// </summary>
        [Fact]
        public async Task FriendFeedPost_AudienceFiltering_ShouldRespectVisibility()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var friend = CreateTestUser("friend1", "friend", "Bob", "Friend");
            var stranger = CreateTestUser("stranger1", "stranger", "Charlie", "Stranger");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, friend, stranger);
                await context.SaveChangesAsync();
            }

            await SeedAcceptedFriendshipAsync("author1", "friend1");

            // Create a friends-only cross-feed post
            var (success, _, _) = await service.CreatePostAsync(
                "author1", "Friends only cross-feed post", AudienceType.FriendsOnly, null, "friend1");
            success.Should().BeTrue();

            // Act - Stranger views the friend's profile
            var strangerView = await service.GetPostsByUserWithAudienceFilterAsync("friend1", "stranger1");

            // Assert - Stranger should not see friends-only post
            strangerView.Should().NotContain(p => p.Content == "Friends only cross-feed post");
        }

        /// <summary>
        /// Verifies that multiple cross-feed posts between friends are tracked and visible correctly.
        /// </summary>
        [Fact]
        public async Task MultipleFriendFeedPosts_ShouldAllBeTracked()
        {
            // Arrange
            var service = CreatePostService();
            var author = CreateTestUser("author1", "author", "Alice", "Author");
            var friend = CreateTestUser("friend1", "friend", "Bob", "Friend");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(author, friend);
                await context.SaveChangesAsync();
            }

            await SeedAcceptedFriendshipAsync("author1", "friend1");

            // Act - Create multiple cross-feed posts
            var (s1, _, _) = await service.CreatePostAsync("author1", "Post 1 on Bob's feed", AudienceType.FriendsOnly, null, "friend1");
            var (s2, _, _) = await service.CreatePostAsync("author1", "Post 2 on Bob's feed", AudienceType.FriendsOnly, null, "friend1");
            var (s3, _, _) = await service.CreatePostAsync("friend1", "Bob's reply post on Alice's feed", AudienceType.FriendsOnly, null, "author1");

            s1.Should().BeTrue();
            s2.Should().BeTrue();
            s3.Should().BeTrue();

            // Assert - All posts should be in the database with correct FriendIds
            await using (var context = CreateDbContext())
            {
                var crossFeedPosts = await context.Posts
                    .Where(p => p.FriendId != null)
                    .ToListAsync();

                crossFeedPosts.Should().HaveCount(3);

                var postsOnBobsFeed = crossFeedPosts.Where(p => p.FriendId == "friend1").ToList();
                postsOnBobsFeed.Should().HaveCount(2);
                postsOnBobsFeed.Should().OnlyContain(p => p.AuthorId == "author1");

                var postsOnAlicesFeed = crossFeedPosts.Where(p => p.FriendId == "author1").ToList();
                postsOnAlicesFeed.Should().HaveCount(1);
                postsOnAlicesFeed.First().AuthorId.Should().Be("friend1");
            }
        }

        #endregion
    }
}
