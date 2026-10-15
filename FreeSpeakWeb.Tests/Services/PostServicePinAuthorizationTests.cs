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
    /// <summary>
    /// Tests that <see cref="PostService.PinPostAsync"/> only lets users pin feed posts they are allowed to view.
    /// </summary>
    public class PostServicePinAuthorizationTests : TestBase
    {
        private PostService CreatePostService(TestRepositoryFactory repoFactory)
        {
            var factory = repoFactory.ContextFactory;
            var environment = new Mock<IWebHostEnvironment>();
            environment.Setup(m => m.ContentRootPath).Returns(Path.GetTempPath());
            var notificationService = new NotificationService(
                repoFactory.CreateNotificationRepository(),
                factory,
                new Mock<ILogger<NotificationService>>().Object,
                new Mock<IServiceScopeFactory>().Object,
                MockRepositories.CreateMockAuditLogRepository().Object);
            var userPreferenceService = new UserPreferenceService(factory, new Mock<ILogger<UserPreferenceService>>().Object);
            var helper = new PostNotificationHelper(factory, notificationService, userPreferenceService, new Mock<ILogger<PostNotificationHelper>>().Object);

            return new PostService(
                factory,
                repoFactory.CreateFeedPostRepository(),
                repoFactory.CreateFeedCommentRepository(),
                repoFactory.CreateFeedPostLikeRepository(),
                repoFactory.CreateFeedCommentLikeRepository(),
                repoFactory.CreatePinnedPostRepository(),
                repoFactory.CreatePostNotificationMuteRepository(),
                repoFactory.CreateNotificationRepository(),
                CreateMockLogger<PostService>(),
                Options.Create(new SiteSettings { SiteName = "TestSite", MaxFeedPostCommentDepth = 4, MaxFeedPostDirectCommentCount = 1000 }),
                environment.Object,
                notificationService,
                userPreferenceService,
                helper,
                MockRepositories.CreateMockAuditLogRepository().Object,
                repoFactory.CreateFriendshipRepository());
        }

        private static async Task<int> SeedPostAsync(TestRepositoryFactory repoFactory, AudienceType audience, bool friends)
        {
            using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
            context.Users.Add(TestDataFactory.CreateTestUser(id: "author"));
            context.Users.Add(TestDataFactory.CreateTestUser(id: "viewer"));
            var post = TestDataFactory.CreateTestPost("author", audienceType: audience);
            context.Posts.Add(post);
            if (friends)
            {
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("author", "viewer", FriendshipStatus.Accepted));
            }
            await context.SaveChangesAsync();
            return post.Id;
        }

        [Fact]
        public async Task PinPostAsync_PublicPost_AnyUserCanPin()
        {
            var repoFactory = CreateTestRepositoryFactory("PinAuthPublic");
            var service = CreatePostService(repoFactory);
            var postId = await SeedPostAsync(repoFactory, AudienceType.Public, friends: false);

            var (success, _) = await service.PinPostAsync(postId, "viewer");

            success.Should().BeTrue();
        }

        [Fact]
        public async Task PinPostAsync_FriendsOnlyPost_NonFriend_IsRejected()
        {
            var repoFactory = CreateTestRepositoryFactory("PinAuthNonFriend");
            var service = CreatePostService(repoFactory);
            var postId = await SeedPostAsync(repoFactory, AudienceType.FriendsOnly, friends: false);

            var (success, error) = await service.PinPostAsync(postId, "viewer");

            success.Should().BeFalse();
            error.Should().Contain("permission");
            using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
            (await context.PinnedPosts.AnyAsync(p => p.PostId == postId)).Should().BeFalse();
        }

        [Fact]
        public async Task PinPostAsync_FriendsOnlyPost_Friend_CanPin()
        {
            var repoFactory = CreateTestRepositoryFactory("PinAuthFriend");
            var service = CreatePostService(repoFactory);
            var postId = await SeedPostAsync(repoFactory, AudienceType.FriendsOnly, friends: true);

            var (success, _) = await service.PinPostAsync(postId, "viewer");

            success.Should().BeTrue();
        }

        [Fact]
        public async Task PinPostAsync_FriendsOnlyPost_Author_CanPin()
        {
            var repoFactory = CreateTestRepositoryFactory("PinAuthAuthor");
            var service = CreatePostService(repoFactory);
            var postId = await SeedPostAsync(repoFactory, AudienceType.FriendsOnly, friends: false);

            var (success, _) = await service.PinPostAsync(postId, "author");

            success.Should().BeTrue();
        }

        [Fact]
        public async Task PinPostAsync_MissingPost_ReturnsNotFound()
        {
            var repoFactory = CreateTestRepositoryFactory("PinAuthMissing");
            var service = CreatePostService(repoFactory);

            var (success, error) = await service.PinPostAsync(9999, "viewer");

            success.Should().BeFalse();
            error.Should().Contain("not found");
        }
    }
}
