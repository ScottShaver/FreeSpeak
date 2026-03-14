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
    public class GroupPostServiceTests : TestBase
    {
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
            var notificationRepo = new NotificationRepository(contextFactory, new Mock<ILogger<NotificationRepository>>().Object);
            return new NotificationService(notificationRepo, contextFactory, logger.Object, scopeFactory.Object);
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
        /// Creates a GroupPostService with real repositories using an in-memory database.
        /// </summary>
        private GroupPostService CreateGroupPostService(TestRepositoryFactory repoFactory)
        {
            var logger = CreateMockLogger<GroupPostService>();
            return new GroupPostService(
                repoFactory.ContextFactory,
                repoFactory.CreateGroupPostRepository(),
                repoFactory.CreateGroupCommentRepository(),
                repoFactory.CreateGroupPostLikeRepository(),
                repoFactory.CreateGroupCommentLikeRepository(),
                repoFactory.CreateGroupRepository(),
                repoFactory.CreateNotificationRepository(),
                logger,
                CreateNotificationService(repoFactory.ContextFactory),
                CreateUserPreferenceService(repoFactory.ContextFactory),
                CreateMockWebHostEnvironment(),
                CreatePostNotificationHelper(repoFactory.ContextFactory),
                repoFactory.CreateGroupAccessValidator(),
                MockRepositories.CreateMockAuditLogRepository().Object);
        }

        #region Post Operations Tests

        [Fact]
        public async Task CreateGroupPostAsync_WithValidContent_ShouldCreatePost()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest1");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage, post) = await service.CreateGroupPostAsync(group.Id, "user1", "Test group post content");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            post.Should().NotBeNull();
            post!.Content.Should().Be("Test group post content");
            post.AuthorId.Should().Be("user1");
            post.GroupId.Should().Be(group.Id);
        }

        [Fact]
        public async Task CreateGroupPostAsync_NonMember_ShouldReturnError()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest2");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user2");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage, post) = await service.CreateGroupPostAsync(group.Id, "user1", "Test content");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("must be a member");
            post.Should().BeNull();
        }

        [Fact]
        public async Task CreateGroupPostAsync_BannedUser_ShouldReturnError()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest3");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user2");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1"); // Add user as member first
            var bannedMember = TestDataFactory.CreateTestGroupBannedMember(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                bannedMember.GroupId = group.Id;
                context.GroupUsers.Add(groupUser); // User needs to be a member to be banned
                context.GroupBannedMembers.Add(bannedMember);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage, post) = await service.CreateGroupPostAsync(group.Id, "user1", "Test content");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("banned");
            post.Should().BeNull();
        }

        [Fact]
        public async Task UpdateGroupPostAsync_ByAuthor_ShouldUpdateContent()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest4");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1", "Original content");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage, images) = await service.UpdateGroupPostAsync(post.Id, "user1", "Updated content");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var updated = await context.GroupPosts.FindAsync(post.Id);
                updated!.Content.Should().Be("Updated content");
                updated.UpdatedAt.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ByAuthor_ShouldDeletePost()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest5");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var deleted = await context.GroupPosts.FindAsync(post.Id);
                deleted.Should().BeNull();
            }
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ByModerator_ShouldDeletePost()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest6");
            var service = CreateGroupPostService(repoFactory);

            var author = TestDataFactory.CreateTestUser(id: "user1");
            var moderator = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user3");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var modGroupUser = TestDataFactory.CreateTestGroupUser(1, "user2", isModerator: true);

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(author, moderator);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                modGroupUser.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.Add(modGroupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "user2");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        #endregion

        #region Comment Operations Tests

        [Fact]
        public async Task AddCommentAsync_WithValidContent_ShouldCreateComment()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest7");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                groupUser.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage, comment) = await service.AddCommentAsync(post.Id, "user1", "Test comment");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            comment.Should().NotBeNull();
            comment!.Content.Should().Be("Test comment");
            comment.PostId.Should().Be(post.Id);

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var updatedPost = await context.GroupPosts.FindAsync(post.Id);
                updatedPost!.CommentCount.Should().Be(1);
            }
        }

        [Fact]
        public async Task AddCommentAsync_NonMember_ShouldReturnError()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest8");
            var service = CreateGroupPostService(repoFactory);

            var author = TestDataFactory.CreateTestUser(id: "user2");
            var nonMember = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user2");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(author, nonMember);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                // Only user2 is a member
                var groupUser = TestDataFactory.CreateTestGroupUser(group.Id, "user2");
                context.GroupUsers.Add(groupUser);

                // Create a post in the group
                var post = TestDataFactory.CreateTestGroupPost(group.Id, "user2");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Act - user1 tries to comment but is not a member
                var (success, errorMessage, comment) = await service.AddCommentAsync(post.Id, "user1", "Test comment");

                // Assert
                success.Should().BeFalse();
                errorMessage.Should().Contain("must be a member");
                comment.Should().BeNull();
            }
        }

        [Fact]
        public async Task DeleteCommentAsync_ByAuthor_ShouldDeleteComment()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest9");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1", commentCount: 1);
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                comment.PostId = post.Id;
                context.GroupPostComments.Add(comment);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteCommentAsync(comment.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var deleted = await context.GroupPostComments.FindAsync(comment.Id);
                deleted.Should().BeNull();
                var updatedPost = await context.GroupPosts.FindAsync(post.Id);
                updatedPost!.CommentCount.Should().Be(0);
            }
        }

        #endregion

        #region Like Operations Tests

        [Fact(Skip = "ExecuteUpdateAsync doesn't work properly with InMemory provider for LikeCount updates. Use integration tests with real database.")]
        public async Task AddOrUpdateReactionAsync_ValidUser_ShouldCreateLike()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest10");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                groupUser.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.AddOrUpdateReactionAsync(post.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var like = await context.GroupPostLikes.FirstOrDefaultAsync(l => l.PostId == post.Id && l.UserId == "user1");
                like.Should().NotBeNull();
                var updatedPost = await context.GroupPosts.FindAsync(post.Id);
                updatedPost!.LikeCount.Should().Be(1);
            }
        }

        [Fact(Skip = "ExecuteUpdateAsync doesn't work properly with InMemory provider for LikeCount updates. Use integration tests with real database.")]
        public async Task RemoveReactionAsync_ExistingLike_ShouldRemoveLike()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest11");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1", likeCount: 1);
            var like = TestDataFactory.CreateTestGroupPostLike(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                like.PostId = post.Id;
                context.GroupPostLikes.Add(like);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.RemoveReactionAsync(post.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var removedLike = await context.GroupPostLikes.FirstOrDefaultAsync(l => l.PostId == post.Id && l.UserId == "user1");
                removedLike.Should().BeNull();
                var updatedPost = await context.GroupPosts.FindAsync(post.Id);
                updatedPost!.LikeCount.Should().Be(0);
            }
        }

        #endregion

        #region Comment Like Operations Tests

        [Fact]
        public async Task AddOrUpdateCommentReactionAsync_ValidUser_ShouldCreateLike()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest12");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                groupUser.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
                comment.PostId = post.Id;
                context.GroupPostComments.Add(comment);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.AddOrUpdateCommentReactionAsync(comment.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var like = await context.GroupPostCommentLikes.FirstOrDefaultAsync(l => l.CommentId == comment.Id && l.UserId == "user1");
                like.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task RemoveCommentReactionAsync_ExistingLike_ShouldRemoveLike()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest13");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "user1");
            var like = TestDataFactory.CreateTestGroupPostCommentLike(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                comment.PostId = post.Id;
                context.GroupPostComments.Add(comment);
                await context.SaveChangesAsync();
                like.CommentId = comment.Id;
                context.GroupPostCommentLikes.Add(like);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.RemoveCommentReactionAsync(comment.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var removedLike = await context.GroupPostCommentLikes.FirstOrDefaultAsync(l => l.CommentId == comment.Id && l.UserId == "user1");
                removedLike.Should().BeNull();
            }
        }

        #endregion

        #region Notification Mute Operations Tests

        [Fact]
        public async Task MutePostNotificationsAsync_ShouldCreateMute()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest14");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.MutePostNotificationsAsync(post.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var mute = await context.GroupPostNotificationMutes.FirstOrDefaultAsync(m => m.PostId == post.Id && m.UserId == "user1");
                mute.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task UnmutePostNotificationsAsync_ShouldRemoveMute()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest15");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var mute = TestDataFactory.CreateTestGroupPostNotificationMute(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                mute.PostId = post.Id;
                context.GroupPostNotificationMutes.Add(mute);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.UnmutePostNotificationsAsync(post.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var removedMute = await context.GroupPostNotificationMutes.FirstOrDefaultAsync(m => m.PostId == post.Id && m.UserId == "user1");
                removedMute.Should().BeNull();
            }
        }

        [Fact]
        public async Task IsPostNotificationMutedAsync_MutedPost_ShouldReturnTrue()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest16");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var mute = TestDataFactory.CreateTestGroupPostNotificationMute(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                mute.PostId = post.Id;
                context.GroupPostNotificationMutes.Add(mute);
                await context.SaveChangesAsync();
            }

            // Act
            var isMuted = await service.IsPostNotificationMutedAsync(post.Id, "user1");

            // Assert
            isMuted.Should().BeTrue();
        }

        #endregion

        #region Retrieval Operations Tests

        [Fact]
        public async Task GetGroupPostsAsync_ShouldReturnPostsInDescendingOrder()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest17");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var post1 = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Post 1");
                var post2 = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Post 2");
                var post3 = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Post 3");

                context.GroupPosts.AddRange(post1, post2, post3);
                await context.SaveChangesAsync();
            }

            // Act
            var posts = await service.GetGroupPostsAsync(group.Id);

            // Assert
            posts.Should().HaveCount(3);
            posts[0].Content.Should().Be("Post 3"); // Most recent first
        }

        [Fact]
        public async Task GetLastCommentsAsync_ShouldReturnLastNCommentsInAscendingOrder()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest18");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Test post");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                postId = post.Id;

                // Add 5 comments with sequential timestamps
                for (int i = 1; i <= 5; i++)
                {
                    var comment = new GroupPostComment
                    {
                        PostId = postId,
                        AuthorId = "user1",
                        Content = $"Comment {i}",
                        CreatedAt = DateTime.UtcNow.AddMinutes(i)
                    };
                    context.GroupPostComments.Add(comment);
                }
                await context.SaveChangesAsync();
            }

            // Act - get last 3 comments
            var comments = await service.GetLastCommentsAsync(postId, 3);

            // Assert
            comments.Should().HaveCount(3);
            // Comments should be returned in ascending order (oldest to newest of the last 3)
            comments[0].Content.Should().Be("Comment 3");
            comments[1].Content.Should().Be("Comment 4");
            comments[2].Content.Should().Be("Comment 5");
        }

        #endregion

        #region Post Image Operations Tests

        [Fact]
        public async Task AddImageToPostAsync_ByAuthor_ShouldAddImage()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest19");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                groupUser.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage, postImage) = await service.AddImageToPostAsync(post.Id, "/images/test.jpg", "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            postImage.Should().NotBeNull();
            postImage!.ImageUrl.Should().Be("/images/test.jpg");
            postImage.DisplayOrder.Should().Be(0);

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var savedImage = await context.GroupPostImages.FirstOrDefaultAsync(i => i.PostId == post.Id);
                savedImage.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task AddImageToPostAsync_ByNonAuthor_ShouldReturnError()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest20");
            var service = CreateGroupPostService(repoFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser1 = TestDataFactory.CreateTestGroupUser(1, "user1");
            var groupUser2 = TestDataFactory.CreateTestGroupUser(1, "user2");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                groupUser1.GroupId = group.Id;
                groupUser2.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.AddRange(groupUser1, groupUser2);
                await context.SaveChangesAsync();
            }

            // Act - user2 tries to add image to user1's post
            var (success, errorMessage, postImage) = await service.AddImageToPostAsync(post.Id, "/images/test.jpg", "user2");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not authorized");
            postImage.Should().BeNull();
        }

        [Fact]
        public async Task AddImageToPostAsync_MultipleImages_ShouldIncrementDisplayOrder()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest21");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                groupUser.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act - add multiple images
            var (success1, _, image1) = await service.AddImageToPostAsync(post.Id, "/images/test1.jpg", "user1");
            var (success2, _, image2) = await service.AddImageToPostAsync(post.Id, "/images/test2.jpg", "user1");
            var (success3, _, image3) = await service.AddImageToPostAsync(post.Id, "/images/test3.jpg", "user1");

            // Assert
            success1.Should().BeTrue();
            success2.Should().BeTrue();
            success3.Should().BeTrue();
            image1!.DisplayOrder.Should().Be(0);
            image2!.DisplayOrder.Should().Be(1);
            image3!.DisplayOrder.Should().Be(2);
        }

        [Fact]
        public async Task RemoveImageFromPostAsync_ByAuthor_ShouldRemoveImage()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest22");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            int imageId;

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                groupUser.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();

                var image = new GroupPostImage
                {
                    PostId = post.Id,
                    ImageUrl = "/images/test.jpg",
                    DisplayOrder = 0
                };
                context.GroupPostImages.Add(image);
                await context.SaveChangesAsync();
                imageId = image.Id;
            }

            // Act
            var (success, errorMessage) = await service.RemoveImageFromPostAsync(imageId, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var removedImage = await context.GroupPostImages.FindAsync(imageId);
                removedImage.Should().BeNull();
            }
        }

        [Fact]
        public async Task RemoveImageFromPostAsync_ByNonAuthorNonAdmin_ShouldReturnError()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest23");
            var service = CreateGroupPostService(repoFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser1 = TestDataFactory.CreateTestGroupUser(1, "user1");
            var groupUser2 = TestDataFactory.CreateTestGroupUser(1, "user2");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            int imageId;

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                groupUser1.GroupId = group.Id;
                groupUser2.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.AddRange(groupUser1, groupUser2);
                await context.SaveChangesAsync();

                var image = new GroupPostImage
                {
                    PostId = post.Id,
                    ImageUrl = "/images/test.jpg",
                    DisplayOrder = 0
                };
                context.GroupPostImages.Add(image);
                await context.SaveChangesAsync();
                imageId = image.Id;
            }

            // Act - user2 (not author, not admin) tries to remove image
            var (success, errorMessage) = await service.RemoveImageFromPostAsync(imageId, "user2");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not authorized");
        }

        [Fact]
        public async Task GetPostImagesAsync_ShouldReturnImagesInDisplayOrder()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest24");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Add images in non-sequential display order
                var image1 = new GroupPostImage { PostId = post.Id, ImageUrl = "/images/third.jpg", DisplayOrder = 2 };
                var image2 = new GroupPostImage { PostId = post.Id, ImageUrl = "/images/first.jpg", DisplayOrder = 0 };
                var image3 = new GroupPostImage { PostId = post.Id, ImageUrl = "/images/second.jpg", DisplayOrder = 1 };
                context.GroupPostImages.AddRange(image1, image2, image3);
                await context.SaveChangesAsync();
            }

            // Act
            var images = await service.GetPostImagesAsync(post.Id);

            // Assert
            images.Should().HaveCount(3);
            images[0].ImageUrl.Should().Be("/images/first.jpg");
            images[1].ImageUrl.Should().Be("/images/second.jpg");
            images[2].ImageUrl.Should().Be("/images/third.jpg");
        }

        [Fact]
        public async Task GetPostImagesAsync_NoImages_ShouldReturnEmptyList()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPostTest25");
            var service = CreateGroupPostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var images = await service.GetPostImagesAsync(post.Id);

            // Assert
            images.Should().BeEmpty();
        }

        #endregion
    }
}

