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
    /// Parity tests to verify that Post and GroupPost services produce equivalent results
    /// for common operations. These tests ensure behavioral consistency between the two systems.
    /// </summary>
    public class PostGroupPostParityTests : TestBase
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

        private static IWebHostEnvironment CreateMockWebHostEnvironment()
        {
            var mock = new Mock<IWebHostEnvironment>();
            mock.Setup(m => m.ContentRootPath).Returns(Path.GetTempPath());
            return mock.Object;
        }

        /// <summary>
        /// Creates a NotificationService with a real repository using the provided context factory.
        /// </summary>
        private static NotificationService CreateNotificationService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            var logger = new Mock<ILogger<NotificationService>>();
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var notificationRepo = new NotificationRepository(contextFactory, new Mock<ILogger<NotificationRepository>>().Object, CreateMockProfilerHelper());
            return new NotificationService(notificationRepo, contextFactory, logger.Object, scopeFactory.Object, MockRepositories.CreateMockAuditLogRepository().Object);
        }

        /// <summary>
        /// Creates a UserPreferenceService with a real database context factory.
        /// </summary>
        private static UserPreferenceService CreateUserPreferenceService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            var logger = new Mock<ILogger<UserPreferenceService>>();
            return new UserPreferenceService(contextFactory, logger.Object);
        }

        /// <summary>
        /// Creates a PostNotificationHelper with real dependencies.
        /// </summary>
        private static PostNotificationHelper CreatePostNotificationHelper(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            var logger = new Mock<ILogger<PostNotificationHelper>>();
            return new PostNotificationHelper(contextFactory, CreateNotificationService(contextFactory), CreateUserPreferenceService(contextFactory), logger.Object);
        }

        /// <summary>
        /// Creates a PostService with real repositories using the test repository factory.
        /// </summary>
        private PostService CreatePostService(TestRepositoryFactory repoFactory)
        {
            return new PostService(
                repoFactory.ContextFactory,
                repoFactory.CreateFeedPostRepository(),
                repoFactory.CreateFeedCommentRepository(),
                repoFactory.CreateFeedPostLikeRepository(),
                repoFactory.CreateFeedCommentLikeRepository(),
                repoFactory.CreatePinnedPostRepository(),
                repoFactory.CreatePostNotificationMuteRepository(),
                repoFactory.CreateNotificationRepository(),
                CreateMockLogger<PostService>(),
                CreateTestSiteSettings(),
                CreateMockWebHostEnvironment(),
                CreateNotificationService(repoFactory.ContextFactory),
                CreateUserPreferenceService(repoFactory.ContextFactory),
                CreatePostNotificationHelper(repoFactory.ContextFactory),
                MockRepositories.CreateMockAuditLogRepository().Object);
        }

        /// <summary>
        /// Creates a GroupPostService with real repositories using the test repository factory.
        /// </summary>
        private GroupPostService CreateGroupPostService(TestRepositoryFactory repoFactory)
        {
            var pointsLogger = CreateMockLogger<GroupPointsService>();
            var groupPointsService = new GroupPointsService(repoFactory.ContextFactory, pointsLogger);

            return new GroupPostService(
                repoFactory.ContextFactory,
                repoFactory.CreateGroupPostRepository(),
                repoFactory.CreateGroupCommentRepository(),
                repoFactory.CreateGroupPostLikeRepository(),
                repoFactory.CreateGroupCommentLikeRepository(),
                repoFactory.CreateGroupRepository(),
                repoFactory.CreateNotificationRepository(),
                CreateMockLogger<GroupPostService>(),
                CreateNotificationService(repoFactory.ContextFactory),
                CreateUserPreferenceService(repoFactory.ContextFactory),
                CreateMockWebHostEnvironment(),
                CreatePostNotificationHelper(repoFactory.ContextFactory),
                repoFactory.CreateGroupAccessValidator(),
                MockRepositories.CreateMockAuditLogRepository().Object,
                groupPointsService);
        }

        /// <summary>
        /// Sets up a user with group membership for GroupPost tests
        /// </summary>
        private async Task<(ApplicationUser user, Group group)> SetupUserWithGroup(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            string userId = "user1")
        {
            var user = TestDataFactory.CreateTestUser(id: userId);
            var group = TestDataFactory.CreateTestGroup(userId);
            var groupUser = TestDataFactory.CreateTestGroupUser(1, userId);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Reload to get the ID
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                group = await context.Groups.FirstAsync();
            }

            return (user, group);
        }

        #endregion

        #region Create Operation Parity Tests

        [Fact]
        public async Task CreatePost_BothServices_ShouldProduceEquivalentStructure()
        {
            // Arrange - Post
            var postRepoFactory = CreateTestRepositoryFactory("ParityCreate_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityCreate_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");

            // Act
            var postResult = await postService.CreatePostAsync("user1", "Test content");
            var groupPostResult = await groupPostService.CreateGroupPostAsync(group.Id, "user1", "Test content");

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue("Post creation should succeed");
            groupPostResult.Success.Should().BeTrue("GroupPost creation should succeed");

            // Assert - Both should have equivalent structure
            postResult.Post.Should().NotBeNull();
            groupPostResult.Post.Should().NotBeNull();

            postResult.Post!.Content.Should().Be(groupPostResult.Post!.Content);
            postResult.Post.AuthorId.Should().Be(groupPostResult.Post.AuthorId);
            postResult.Post.LikeCount.Should().Be(groupPostResult.Post.LikeCount);
            postResult.Post.CommentCount.Should().Be(groupPostResult.Post.CommentCount);
            postResult.Post.ShareCount.Should().Be(groupPostResult.Post.ShareCount);
        }

        [Fact]
        public async Task CreatePost_WithEmptyContent_BothServices_ShouldRejectEquivalently()
        {
            // Arrange - Post
            var postRepoFactory = CreateTestRepositoryFactory("ParityCreateEmpty_Post");
            var postService = CreatePostService(postRepoFactory);

            // Arrange - GroupPost
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityCreateEmpty_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");

            // Act
            var postResult = await postService.CreatePostAsync("user1", "");
            var groupPostResult = await groupPostService.CreateGroupPostAsync(group.Id, "user1", "");

            // Assert - Both should fail with similar error pattern
            postResult.Success.Should().BeFalse("Post creation with empty content should fail");
            groupPostResult.Success.Should().BeFalse("GroupPost creation with empty content should fail");

            postResult.Post.Should().BeNull();
            groupPostResult.Post.Should().BeNull();

            // Both should mention content requirement
            postResult.ErrorMessage.Should().NotBeNullOrEmpty();
            groupPostResult.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task CreatePost_WithImages_BothServices_ShouldProduceEquivalentImageStructure()
        {
            // Arrange - Post
            var postRepoFactory = CreateTestRepositoryFactory("ParityCreateImages_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityCreateImages_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");

            var imageUrls = new List<string> { "image1.jpg", "image2.jpg" };

            // Act
            var postResult = await postService.CreatePostAsync("user1", "Post with images", AudienceType.Public, imageUrls);
            var groupPostResult = await groupPostService.CreateGroupPostAsync(group.Id, "user1", "Post with images", imageUrls);

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            // Verify image counts match
            using (var postContext = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            using (var groupContext = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                var postImages = await postContext.PostImages.Where(pi => pi.PostId == postResult.Post!.Id).ToListAsync();
                var groupImages = await groupContext.GroupPostImages.Where(gi => gi.PostId == groupPostResult.Post!.Id).ToListAsync();

                postImages.Should().HaveCount(2);
                groupImages.Should().HaveCount(2);

                // Display order should be equivalent
                postImages[0].DisplayOrder.Should().Be(groupImages[0].DisplayOrder);
                postImages[1].DisplayOrder.Should().Be(groupImages[1].DisplayOrder);
            }
        }

        #endregion

        #region Update Operation Parity Tests

        [Fact]
        public async Task UpdatePost_ByAuthor_BothServices_ShouldProduceEquivalentResults()
        {
            // Arrange - Post
            var postRepoFactory = CreateTestRepositoryFactory("ParityUpdate_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", "Original content");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityUpdate_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Original content");
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
            }

            // Act
            var postResult = await postService.UpdatePostAsync(post.Id, "user1", "Updated content");
            var groupPostResult = await groupPostService.UpdateGroupPostAsync(groupPost.Id, "user1", "Updated content");

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            // Verify updates are equivalent
            using (var postContext = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            using (var groupContext = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                var updatedPost = await postContext.Posts.FindAsync(post.Id);
                var updatedGroupPost = await groupContext.GroupPosts.FindAsync(groupPost.Id);

                updatedPost!.Content.Should().Be(updatedGroupPost!.Content);
                updatedPost.UpdatedAt.Should().NotBeNull();
                updatedGroupPost.UpdatedAt.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task UpdatePost_ByNonAuthor_BothServices_ShouldRejectEquivalently()
        {
            // Arrange - Post
            var postRepoFactory = CreateTestRepositoryFactory("ParityUpdateNonAuthor_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser1 = TestDataFactory.CreateTestUser(id: "user1");
            var postUser2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1", "Original content");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(postUser1, postUser2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost with non-admin user trying to edit another's post
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityUpdateNonAuthor_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                var groupUser1 = TestDataFactory.CreateTestGroupUser(group.Id, "user1");
                var groupUser2 = TestDataFactory.CreateTestGroupUser(group.Id, "user2");
                context.GroupUsers.AddRange(groupUser1, groupUser2);
                var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Original content");
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
            }

            int groupPostId;
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                groupPostId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Act - user2 tries to update user1's posts
            var postResult = await postService.UpdatePostAsync(post.Id, "user2", "Hacked content");
            var groupPostResult = await groupPostService.UpdateGroupPostAsync(groupPostId, "user2", "Hacked content");

            // Assert - Both should fail
            postResult.Success.Should().BeFalse();
            groupPostResult.Success.Should().BeFalse();

            postResult.ErrorMessage.Should().NotBeNullOrEmpty();
            groupPostResult.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region Delete Operation Parity Tests

        [Fact]
        public async Task DeletePost_ByAuthor_BothServices_ShouldProduceEquivalentCleanup()
        {
            // Arrange - Post
            var postRepoFactory = CreateTestRepositoryFactory("ParityDelete_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityDelete_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
            }

            // Act
            var postResult = await postService.DeletePostAsync(post.Id, "user1");
            var groupPostResult = await groupPostService.DeleteGroupPostAsync(groupPost.Id, "user1");

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            // Verify deletion is complete
            using (var postContext = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            using (var groupContext = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                var deletedPost = await postContext.Posts.FindAsync(post.Id);
                var deletedGroupPost = await groupContext.GroupPosts.FindAsync(groupPost.Id);

                deletedPost.Should().BeNull("Post should be deleted");
                deletedGroupPost.Should().BeNull("GroupPost should be deleted");
            }
        }

        [Fact]
        public async Task DeletePost_WithComments_BothServices_ShouldCleanupComments()
        {
            // Arrange - Post with comment
            var postRepoFactory = CreateTestRepositoryFactory("ParityDeleteComments_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 1);
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                var comment = TestDataFactory.CreateTestComment(post.Id, "user1");
                context.Comments.Add(comment);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost with comment
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityDeleteComments_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", commentCount: 1);
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                var groupComment = TestDataFactory.CreateTestGroupPostComment(groupPost.Id, "user1");
                context.GroupPostComments.Add(groupComment);
                await context.SaveChangesAsync();
            }

            // Act
            var postResult = await postService.DeletePostAsync(post.Id, "user1");
            var groupPostResult = await groupPostService.DeleteGroupPostAsync(groupPost.Id, "user1");

            // Assert - Both should succeed and clean up comments
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            using (var postContext = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            using (var groupContext = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                var postComments = await postContext.Comments.Where(c => c.PostId == post.Id).ToListAsync();
                var groupComments = await groupContext.GroupPostComments.Where(c => c.PostId == groupPost.Id).ToListAsync();

                postComments.Should().BeEmpty("Post comments should be deleted");
                groupComments.Should().BeEmpty("GroupPost comments should be deleted");
            }
        }

        #endregion

        #region Comment Operation Parity Tests

        [Fact]
        public async Task AddComment_BothServices_ShouldProduceEquivalentStructure()
        {
            // Arrange - Post
            var postRepoFactory = CreateTestRepositoryFactory("ParityComment_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityComment_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
            }

            // Act
            var postResult = await postService.AddCommentAsync(post.Id, "user1", "Test comment");
            var groupPostResult = await groupPostService.AddCommentAsync(groupPost.Id, "user1", "Test comment");

            // Assert - Both should succeed with equivalent structure
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            postResult.Comment.Should().NotBeNull();
            groupPostResult.Comment.Should().NotBeNull();

            postResult.Comment!.Content.Should().Be(groupPostResult.Comment!.Content);
            postResult.Comment.AuthorId.Should().Be(groupPostResult.Comment.AuthorId);

            // Verify comment counts updated
            using (var postContext = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            using (var groupContext = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                var updatedPost = await postContext.Posts.FindAsync(post.Id);
                var updatedGroupPost = await groupContext.GroupPosts.FindAsync(groupPost.Id);

                updatedPost!.CommentCount.Should().Be(updatedGroupPost!.CommentCount);
            }
        }

        [Fact]
        public async Task AddReply_BothServices_ShouldProduceEquivalentNestedStructure()
        {
            // Arrange - Post with parent comment
            var postRepoFactory = CreateTestRepositoryFactory("ParityReply_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 1);
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                var parentComment = TestDataFactory.CreateTestComment(post.Id, "user1", "Parent comment");
                context.Comments.Add(parentComment);
                await context.SaveChangesAsync();
            }

            int postParentCommentId;
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                postParentCommentId = (await context.Comments.FirstAsync()).Id;
            }

            // Arrange - GroupPost with parent comment
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityReply_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", commentCount: 1);
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                var groupParentComment = TestDataFactory.CreateTestGroupPostComment(groupPost.Id, "user1", "Parent comment");
                context.GroupPostComments.Add(groupParentComment);
                await context.SaveChangesAsync();
            }

            int groupPostParentCommentId;
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                groupPostParentCommentId = (await context.GroupPostComments.FirstAsync()).Id;
            }

            // Act - Add replies
            var postResult = await postService.AddCommentAsync(post.Id, "user1", "Reply comment", parentCommentId: postParentCommentId);
            var groupPostResult = await groupPostService.AddCommentAsync(groupPost.Id, "user1", "Reply comment", parentCommentId: groupPostParentCommentId);

            // Assert - Both should succeed with equivalent nested structure
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            postResult.Comment!.ParentCommentId.Should().NotBeNull();
            groupPostResult.Comment!.ParentCommentId.Should().NotBeNull();

            postResult.Comment.Content.Should().Be(groupPostResult.Comment.Content);
        }

        [Fact]
        public async Task DeleteComment_BothServices_ShouldProduceEquivalentCleanup()
        {
            // Arrange - Post with comment
            var postRepoFactory = CreateTestRepositoryFactory("ParityDeleteComment_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 1);
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                var comment = TestDataFactory.CreateTestComment(post.Id, "user1");
                context.Comments.Add(comment);
                await context.SaveChangesAsync();
            }

            int postCommentId;
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                postCommentId = (await context.Comments.FirstAsync()).Id;
            }

            // Arrange - GroupPost with comment
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityDeleteComment_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", commentCount: 1);
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                var groupComment = TestDataFactory.CreateTestGroupPostComment(groupPost.Id, "user1");
                context.GroupPostComments.Add(groupComment);
                await context.SaveChangesAsync();
            }

            int groupCommentId;
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                groupCommentId = (await context.GroupPostComments.FirstAsync()).Id;
            }

            // Act
            var postResult = await postService.DeleteCommentAsync(postCommentId, "user1");
            var groupPostResult = await groupPostService.DeleteCommentAsync(groupCommentId, "user1");

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            // Verify comment counts updated equivalently
            using (var postContext = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            using (var groupContext = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                var updatedPost = await postContext.Posts.FindAsync(post.Id);
                var updatedGroupPost = await groupContext.GroupPosts.FindAsync(groupPost.Id);

                updatedPost!.CommentCount.Should().Be(0);
                updatedGroupPost!.CommentCount.Should().Be(0);
            }
        }

        #endregion

        #region Reaction Operation Parity Tests

        [Fact(Skip = "ExecuteUpdateAsync doesn't work properly with InMemory provider for LikeCount updates. Use integration tests with real database.")]
        public async Task AddReaction_BothServices_ShouldProduceEquivalentResults()
        {
            // Arrange - Post
            var postRepoFactory = CreateTestRepositoryFactory("ParityReaction_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityReaction_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
            }

            // Act - Both services use AddOrUpdateReactionAsync
            var postResult = await postService.AddOrUpdateReactionAsync(post.Id, "user1", LikeType.Like);
            var groupPostResult = await groupPostService.AddOrUpdateReactionAsync(groupPost.Id, "user1", LikeType.Like);

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            // Verify like counts match
            using (var postContext = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            using (var groupContext = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                var updatedPost = await postContext.Posts.FindAsync(post.Id);
                var updatedGroupPost = await groupContext.GroupPosts.FindAsync(groupPost.Id);

                updatedPost!.LikeCount.Should().Be(1);
                updatedGroupPost!.LikeCount.Should().Be(1);
            }
        }

        [Fact(Skip = "ExecuteUpdateAsync doesn't work properly with InMemory provider for LikeCount updates. Use integration tests with real database.")]
        public async Task RemoveReaction_BothServices_ShouldProduceEquivalentCleanup()
        {
            // Arrange - Post with like
            var postRepoFactory = CreateTestRepositoryFactory("ParityRemoveReaction_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", likeCount: 1);
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                var like = new Like { PostId = post.Id, UserId = "user1", Type = LikeType.Like, CreatedAt = DateTime.UtcNow };
                context.Likes.Add(like);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost with like
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityRemoveReaction_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", likeCount: 1);
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                var groupLike = TestDataFactory.CreateTestGroupPostLike(groupPost.Id, "user1");
                context.GroupPostLikes.Add(groupLike);
                await context.SaveChangesAsync();
            }

            // Act
            var postResult = await postService.RemoveLikeAsync(post.Id, "user1");
            var groupPostResult = await groupPostService.RemoveReactionAsync(groupPost.Id, "user1");

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            // Verify like counts match
            using (var postContext = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            using (var groupContext = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                var updatedPost = await postContext.Posts.FindAsync(post.Id);
                var updatedGroupPost = await groupContext.GroupPosts.FindAsync(groupPost.Id);

                updatedPost!.LikeCount.Should().Be(0);
                updatedGroupPost!.LikeCount.Should().Be(0);
            }
        }

        [Fact(Skip = "ExecuteUpdateAsync doesn't work properly with InMemory provider for LikeCount updates. Use integration tests with real database.")]
        public async Task GetReactionBreakdown_BothServices_ShouldProduceEquivalentFormat()
        {
            // Arrange - Post with multiple reactions
            var postRepoFactory = CreateTestRepositoryFactory("ParityBreakdown_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser1 = TestDataFactory.CreateTestUser(id: "user1");
            var postUser2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1", likeCount: 2);
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(postUser1, postUser2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                context.Likes.Add(new Like { PostId = post.Id, UserId = "user1", Type = LikeType.Like, CreatedAt = DateTime.UtcNow });
                context.Likes.Add(new Like { PostId = post.Id, UserId = "user2", Type = LikeType.Love, CreatedAt = DateTime.UtcNow });
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost with multiple reactions
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityBreakdown_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "user1"));
                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "user2"));
                var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", likeCount: 2);
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                context.GroupPostLikes.Add(new GroupPostLike { PostId = groupPost.Id, UserId = "user1", Type = LikeType.Like, CreatedAt = DateTime.UtcNow });
                context.GroupPostLikes.Add(new GroupPostLike { PostId = groupPost.Id, UserId = "user2", Type = LikeType.Love, CreatedAt = DateTime.UtcNow });
                await context.SaveChangesAsync();
            }

            int groupPostId;
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                groupPostId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Act
            var postBreakdown = await postService.GetReactionBreakdownAsync(post.Id);
            var groupPostBreakdown = await groupPostService.GetReactionBreakdownAsync(groupPostId);

            // Assert - Both should have equivalent breakdown structure
            postBreakdown.Should().NotBeNull();
            groupPostBreakdown.Should().NotBeNull();

            postBreakdown.Should().ContainKey(LikeType.Like);
            groupPostBreakdown.Should().ContainKey(LikeType.Like);

            postBreakdown[LikeType.Like].Should().Be(groupPostBreakdown[LikeType.Like]);
            postBreakdown[LikeType.Love].Should().Be(groupPostBreakdown[LikeType.Love]);
        }

        [Fact(Skip = "ExecuteUpdateAsync doesn't work properly with InMemory provider for LikeCount updates. Use integration tests with real database.")]
        public async Task AddCommentReaction_BothServices_ShouldProduceEquivalentResults()
        {
            // Arrange - Post with comment
            var postRepoFactory = CreateTestRepositoryFactory("ParityCommentReaction_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 1);
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                var comment = TestDataFactory.CreateTestComment(post.Id, "user1");
                context.Comments.Add(comment);
                await context.SaveChangesAsync();
            }

            int postCommentId;
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                postCommentId = (await context.Comments.FirstAsync()).Id;
            }

            // Arrange - GroupPost with comment
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityCommentReaction_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", commentCount: 1);
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                var groupComment = TestDataFactory.CreateTestGroupPostComment(groupPost.Id, "user1");
                context.GroupPostComments.Add(groupComment);
                await context.SaveChangesAsync();
            }

            int groupCommentId;
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                groupCommentId = (await context.GroupPostComments.FirstAsync()).Id;
            }

            // Act
            var postResult = await postService.AddOrUpdateCommentReactionAsync(postCommentId, "user1", LikeType.Like);
            var groupPostResult = await groupPostService.AddOrUpdateCommentReactionAsync(groupCommentId, "user1", LikeType.Like);

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            // Verify comment like counts match
            var postLikeCount = await postService.GetCommentLikeCountAsync(postCommentId);
            var groupLikeCount = await groupPostService.GetCommentLikeCountAsync(groupCommentId);

            postLikeCount.Should().Be(groupLikeCount);
        }

        #endregion

        #region Comment Retrieval Parity Tests

        [Fact]
        public async Task GetCommentsPagedAsync_BothServices_ShouldProduceEquivalentResults()
        {
            // Arrange - Post with multiple comments
            var postRepoFactory = CreateTestRepositoryFactory("ParityGetComments_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 3);
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                for (int i = 0; i < 3; i++)
                {
                    var comment = TestDataFactory.CreateTestComment(post.Id, "user1", $"Comment {i + 1}");
                    context.Comments.Add(comment);
                }
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost with multiple comments
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityGetComments_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", commentCount: 3);
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                for (int i = 0; i < 3; i++)
                {
                    var comment = TestDataFactory.CreateTestGroupPostComment(groupPost.Id, "user1", $"Comment {i + 1}");
                    context.GroupPostComments.Add(comment);
                }
                await context.SaveChangesAsync();
            }

            // Act
            var postComments = await postService.GetCommentsPagedAsync(post.Id, 10, 1);
            var groupPostComments = await groupPostService.GetCommentsPagedAsync(groupPost.Id, 10, 1);

            // Assert - Both should return same count
            postComments.Should().HaveCount(3);
            groupPostComments.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetDirectCommentCountAsync_BothServices_ShouldProduceEquivalentResults()
        {
            // Arrange - Post with direct comment and reply
            var postRepoFactory = CreateTestRepositoryFactory("ParityDirectCount_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 2);
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                var directComment = TestDataFactory.CreateTestComment(post.Id, "user1", "Direct comment");
                context.Comments.Add(directComment);
                await context.SaveChangesAsync();
                var replyComment = TestDataFactory.CreateTestComment(post.Id, "user1", "Reply", directComment.Id);
                context.Comments.Add(replyComment);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost with direct comment and reply
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityDirectCount_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", commentCount: 2);
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                var directComment = TestDataFactory.CreateTestGroupPostComment(groupPost.Id, "user1", "Direct comment");
                context.GroupPostComments.Add(directComment);
                await context.SaveChangesAsync();
                var replyComment = TestDataFactory.CreateTestGroupPostComment(groupPost.Id, "user1", "Reply", directComment.Id);
                context.GroupPostComments.Add(replyComment);
                await context.SaveChangesAsync();
            }

            // Act
            var postDirectCount = await postService.GetDirectCommentCountAsync(post.Id);
            var groupDirectCount = await groupPostService.GetDirectCommentCountAsync(groupPost.Id);

            // Assert - Both should return only direct comments (not replies)
            postDirectCount.Should().Be(1);
            groupDirectCount.Should().Be(1);
        }

        [Fact]
        public async Task GetRepliesAsync_BothServices_ShouldProduceEquivalentResults()
        {
            // Arrange - Post with comment and replies
            var postRepoFactory = CreateTestRepositoryFactory("ParityReplies_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 3);
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                var parentComment = TestDataFactory.CreateTestComment(post.Id, "user1", "Parent");
                context.Comments.Add(parentComment);
                await context.SaveChangesAsync();
                context.Comments.Add(TestDataFactory.CreateTestComment(post.Id, "user1", "Reply 1", parentComment.Id));
                context.Comments.Add(TestDataFactory.CreateTestComment(post.Id, "user1", "Reply 2", parentComment.Id));
                await context.SaveChangesAsync();
            }

            int postParentId;
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                postParentId = (await context.Comments.FirstAsync(c => c.ParentCommentId == null)).Id;
            }

            // Arrange - GroupPost with comment and replies
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityReplies_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1", commentCount: 3);
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                var parentComment = TestDataFactory.CreateTestGroupPostComment(groupPost.Id, "user1", "Parent");
                context.GroupPostComments.Add(parentComment);
                await context.SaveChangesAsync();
                context.GroupPostComments.Add(TestDataFactory.CreateTestGroupPostComment(groupPost.Id, "user1", "Reply 1", parentComment.Id));
                context.GroupPostComments.Add(TestDataFactory.CreateTestGroupPostComment(groupPost.Id, "user1", "Reply 2", parentComment.Id));
                await context.SaveChangesAsync();
            }

            int groupParentId;
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                groupParentId = (await context.GroupPostComments.FirstAsync(c => c.ParentCommentId == null)).Id;
            }

            // Act
            var postReplies = await postService.GetRepliesAsync(postParentId);
            var groupReplies = await groupPostService.GetRepliesAsync(groupParentId);

            // Assert - Both should return same number of replies
            postReplies.Should().HaveCount(2);
            groupReplies.Should().HaveCount(2);
        }

        #endregion

        #region Notification Mute Parity Tests

        [Fact]
        public async Task MutePostNotifications_BothServices_ShouldProduceEquivalentResults()
        {
            // Arrange - Post
            var postRepoFactory = CreateTestRepositoryFactory("ParityMute_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityMute_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
            }

            // Act
            var postResult = await postService.MutePostNotificationsAsync(post.Id, "user1");
            var groupPostResult = await groupPostService.MutePostNotificationsAsync(groupPost.Id, "user1");

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            // Verify mute status
            var postMuted = await postService.IsPostNotificationMutedAsync(post.Id, "user1");
            var groupMuted = await groupPostService.IsPostNotificationMutedAsync(groupPost.Id, "user1");

            postMuted.Should().BeTrue();
            groupMuted.Should().BeTrue();
        }

        [Fact]
        public async Task UnmutePostNotifications_BothServices_ShouldProduceEquivalentResults()
        {
            // Arrange - Post with mute
            var postRepoFactory = CreateTestRepositoryFactory("ParityUnmute_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                context.PostNotificationMutes.Add(new PostNotificationMute { PostId = post.Id, UserId = "user1", MutedAt = DateTime.UtcNow });
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost with mute
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityUnmute_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                context.GroupPostNotificationMutes.Add(TestDataFactory.CreateTestGroupPostNotificationMute(groupPost.Id, "user1"));
                await context.SaveChangesAsync();
            }

            // Act
            var postResult = await postService.UnmutePostNotificationsAsync(post.Id, "user1");
            var groupPostResult = await groupPostService.UnmutePostNotificationsAsync(groupPost.Id, "user1");

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            // Verify unmute status
            var postMuted = await postService.IsPostNotificationMutedAsync(post.Id, "user1");
            var groupMuted = await groupPostService.IsPostNotificationMutedAsync(groupPost.Id, "user1");

            postMuted.Should().BeFalse();
            groupMuted.Should().BeFalse();
        }

        #endregion

        #region Image Operation Parity Tests

        [Fact]
        public async Task AddImageToPost_BothServices_ShouldProduceEquivalentResults()
        {
            // Arrange - Post
            var postRepoFactory = CreateTestRepositoryFactory("ParityAddImage_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityAddImage_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
            }

            // Act
            var postResult = await postService.AddImageToPostAsync(post.Id, "image.jpg", "user1");
            var groupPostResult = await groupPostService.AddImageToPostAsync(groupPost.Id, "image.jpg", "user1");

            // Assert - Both should succeed
            postResult.Success.Should().BeTrue();
            groupPostResult.Success.Should().BeTrue();

            // Verify image counts match
            using (var postContext = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            using (var groupContext = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                var postImages = await postContext.PostImages.Where(i => i.PostId == post.Id).ToListAsync();
                var groupImages = await groupContext.GroupPostImages.Where(i => i.PostId == groupPost.Id).ToListAsync();

                postImages.Should().HaveCount(1);
                groupImages.Should().HaveCount(1);
            }
        }

        [Fact]
        public async Task GetPostImages_BothServices_ShouldProduceEquivalentOrdering()
        {
            // Arrange - Post with images
            var postRepoFactory = CreateTestRepositoryFactory("ParityGetImages_Post");
            var postService = CreatePostService(postRepoFactory);
            var postUser = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            using (var context = await postRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(postUser);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                context.PostImages.Add(new PostImage { PostId = post.Id, ImageUrl = "img1.jpg", DisplayOrder = 0, UploadedAt = DateTime.UtcNow });
                context.PostImages.Add(new PostImage { PostId = post.Id, ImageUrl = "img2.jpg", DisplayOrder = 1, UploadedAt = DateTime.UtcNow });
                await context.SaveChangesAsync();
            }

            // Arrange - GroupPost with images
            var groupPostRepoFactory = CreateTestRepositoryFactory("ParityGetImages_GroupPost");
            var groupPostService = CreateGroupPostService(groupPostRepoFactory);
            var (_, group) = await SetupUserWithGroup(groupPostRepoFactory.ContextFactory, "user1");
            var groupPost = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
            using (var context = await groupPostRepoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.GroupPosts.Add(groupPost);
                await context.SaveChangesAsync();
                context.GroupPostImages.Add(new GroupPostImage { PostId = groupPost.Id, ImageUrl = "img1.jpg", DisplayOrder = 0, UploadedAt = DateTime.UtcNow });
                context.GroupPostImages.Add(new GroupPostImage { PostId = groupPost.Id, ImageUrl = "img2.jpg", DisplayOrder = 1, UploadedAt = DateTime.UtcNow });
                await context.SaveChangesAsync();
            }

            // Act
            var postImages = await postService.GetPostImagesAsync(post.Id);
            var groupImages = await groupPostService.GetPostImagesAsync(groupPost.Id);

            // Assert - Both should return images in display order
            postImages.Should().HaveCount(2);
            groupImages.Should().HaveCount(2);

            postImages[0].DisplayOrder.Should().Be(0);
            groupImages[0].DisplayOrder.Should().Be(0);
            postImages[1].DisplayOrder.Should().Be(1);
            groupImages[1].DisplayOrder.Should().Be(1);
        }

        #endregion
    }
}

