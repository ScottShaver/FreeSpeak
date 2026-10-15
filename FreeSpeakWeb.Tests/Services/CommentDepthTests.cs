using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories;
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
    /// Verifies the service layer enforces MaxFeedPostCommentDepth: comments exist at depths 0..Max, and a reply
    /// to a comment already at depth Max is rejected (matching the UI, which hides Reply at that depth).
    /// </summary>
    public class CommentDepthTests : TestBase
    {
        private const int MaxDepth = 4;

        private PostService CreatePostService(TestRepositoryFactory repoFactory)
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
                Options.Create(new SiteSettings { SiteName = "TestSite", MaxFeedPostCommentDepth = MaxDepth }),
                env.Object,
                notificationService,
                prefs,
                helper,
                MockRepositories.CreateMockAuditLogRepository().Object,
                repoFactory.CreateFriendshipRepository());
        }

        private GroupPostService CreateGroupPostService(TestRepositoryFactory repoFactory)
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

            return new GroupPostService(
                cf,
                repoFactory.CreateGroupPostRepository(),
                repoFactory.CreateGroupCommentRepository(),
                repoFactory.CreateGroupPostLikeRepository(),
                repoFactory.CreateGroupCommentLikeRepository(),
                repoFactory.CreateGroupRepository(),
                repoFactory.CreateNotificationRepository(),
                CreateMockLogger<GroupPostService>(),
                notificationService,
                prefs,
                env.Object,
                helper,
                repoFactory.CreateGroupAccessValidator(),
                MockRepositories.CreateMockAuditLogRepository().Object,
                new GroupPointsService(cf, CreateMockLogger<GroupPointsService>()),
                Options.Create(new SiteSettings { SiteName = "TestSite", MaxFeedPostCommentDepth = MaxDepth }));
        }

        [Fact]
        public async Task FeedAddCommentAsync_AllowsDepthZeroThroughMax_AndRejectsBeyond()
        {
            var repoFactory = CreateTestRepositoryFactory("FeedDepth");
            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(TestDataFactory.CreateTestUser(id: "u1"));
                var post = TestDataFactory.CreateTestPost("u1");
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                postId = post.Id;
            }
            var service = CreatePostService(repoFactory);

            int? parentId = null;
            for (var depth = 0; depth <= MaxDepth; depth++)
            {
                var (ok, err, comment) = await service.AddCommentAsync(postId, "u1", $"depth {depth}", null, parentId);
                ok.Should().BeTrue($"depth {depth} is allowed ({err})");
                parentId = comment!.Id;
            }

            var (success, error, rejected) = await service.AddCommentAsync(postId, "u1", "too deep", null, parentId);

            success.Should().BeFalse();
            error.Should().Contain("nested more than 4");
            rejected.Should().BeNull();
        }

        [Fact]
        public async Task GroupAddCommentAsync_AllowsDepthZeroThroughMax_AndRejectsBeyond()
        {
            var repoFactory = CreateTestRepositoryFactory("GroupDepth");
            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(TestDataFactory.CreateTestUser(id: "u1"));
                var group = TestDataFactory.CreateTestGroup("u1");
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "u1", isAdmin: true));
                var post = TestDataFactory.CreateTestGroupPost(group.Id, "u1");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                postId = post.Id;
            }
            var service = CreateGroupPostService(repoFactory);

            int? parentId = null;
            for (var depth = 0; depth <= MaxDepth; depth++)
            {
                var (ok, err, comment) = await service.AddCommentAsync(postId, "u1", $"depth {depth}", null, parentId);
                ok.Should().BeTrue($"depth {depth} is allowed ({err})");
                parentId = comment!.Id;
            }

            var (success, error, rejected) = await service.AddCommentAsync(postId, "u1", "too deep", null, parentId);

            success.Should().BeFalse();
            error.Should().Contain("nested more than 4");
            rejected.Should().BeNull();
        }
    }
}
