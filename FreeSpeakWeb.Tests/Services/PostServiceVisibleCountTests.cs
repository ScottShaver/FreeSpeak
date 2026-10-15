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
    /// Verifies the profile post count is viewer-aware and matches the profile posts list.
    /// </summary>
    public class PostServiceVisibleCountTests : TestBase
    {
        private PostService CreateService(TestRepositoryFactory repoFactory)
        {
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(m => m.ContentRootPath).Returns(Path.GetTempPath());
            var cf = repoFactory.ContextFactory;
            var notificationService = new NotificationService(
                repoFactory.CreateNotificationRepository(), cf,
                new Mock<ILogger<NotificationService>>().Object,
                new Mock<IServiceScopeFactory>().Object,
                MockRepositories.CreateMockAuditLogRepository().Object);
            var prefs = new UserPreferenceService(cf, new Mock<ILogger<UserPreferenceService>>().Object);
            var helper = new PostNotificationHelper(cf, notificationService, prefs, new Mock<ILogger<PostNotificationHelper>>().Object);

            return new PostService(
                cf,
                repoFactory.CreateFeedPostRepository(),
                repoFactory.CreateFeedCommentRepository(),
                repoFactory.CreateFeedPostLikeRepository(),
                repoFactory.CreateFeedCommentLikeRepository(),
                repoFactory.CreatePinnedPostRepository(),
                repoFactory.CreatePostNotificationMuteRepository(),
                repoFactory.CreateNotificationRepository(),
                CreateMockLogger<PostService>(),
                Options.Create(new SiteSettings { SiteName = "TestSite", MaxFeedPostCommentDepth = 4, MaxFeedPostDirectCommentCount = 1000 }),
                env.Object,
                notificationService,
                prefs,
                helper,
                MockRepositories.CreateMockAuditLogRepository().Object,
                repoFactory.CreateFriendshipRepository());
        }

        private static async Task SeedAsync(TestRepositoryFactory repoFactory)
        {
            using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
            context.Users.AddRange(
                TestDataFactory.CreateTestUser(id: "owner"),
                TestDataFactory.CreateTestUser(id: "friend"),
                TestDataFactory.CreateTestUser(id: "stranger"));
            context.Friendships.Add(TestDataFactory.CreateTestFriendship("owner", "friend", FriendshipStatus.Accepted));
            context.Posts.AddRange(
                TestDataFactory.CreateTestPost("owner", audienceType: AudienceType.Public),
                TestDataFactory.CreateTestPost("owner", audienceType: AudienceType.Public),
                TestDataFactory.CreateTestPost("owner", audienceType: AudienceType.FriendsOnly),
                TestDataFactory.CreateTestPost("owner", audienceType: AudienceType.MeOnly));
            await context.SaveChangesAsync();
        }

        [Theory]
        [InlineData("owner", 4)]
        [InlineData("friend", 3)]
        [InlineData("stranger", 2)]
        public async Task GetVisibleUserPostCountAsync_RespectsViewerVisibility(string viewerId, int expected)
        {
            var repoFactory = CreateTestRepositoryFactory($"VisibleCount_{viewerId}");
            await SeedAsync(repoFactory);
            var service = CreateService(repoFactory);

            var count = await service.GetVisibleUserPostCountAsync("owner", viewerId);

            count.Should().Be(expected);
        }

        [Theory]
        [InlineData("owner")]
        [InlineData("friend")]
        [InlineData("stranger")]
        public async Task GetVisibleUserPostCountAsync_MatchesPostsList(string viewerId)
        {
            var repoFactory = CreateTestRepositoryFactory($"VisibleCountList_{viewerId}");
            await SeedAsync(repoFactory);
            var service = CreateService(repoFactory);

            var count = await service.GetVisibleUserPostCountAsync("owner", viewerId);
            var list = await service.GetPostsByUserWithAudienceFilterAsync("owner", viewerId, 50, 1);

            count.Should().Be(list.Count);
        }

        [Fact]
        public async Task GetVisibleUserPostCountAsync_ExcludesMeOnlyCrossFeedPost()
        {
            var repoFactory = CreateTestRepositoryFactory("VisibleCountCrossFeed");
            await SeedAsync(repoFactory);
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Posts.Add(TestDataFactory.CreateTestPost("friend", audienceType: AudienceType.FriendsOnly, friendId: "owner"));
                context.Posts.Add(TestDataFactory.CreateTestPost("friend", audienceType: AudienceType.MeOnly, friendId: "owner"));
                await context.SaveChangesAsync();
            }
            var service = CreateService(repoFactory);

            (await service.GetVisibleUserPostCountAsync("owner", "owner")).Should().Be(5);
            (await service.GetVisibleUserPostCountAsync("owner", "friend")).Should().Be(5);
        }
    }
}
