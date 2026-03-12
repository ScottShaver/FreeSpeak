using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
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

        #region Post Operations Tests

        [Fact]
        public async Task CreateGroupPostAsync_WithValidContent_ShouldCreatePost()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("GroupPostTest1");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("GroupPostTest2");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user2");

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("GroupPostTest3");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user2");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1"); // Add user as member first
            var bannedMember = TestDataFactory.CreateTestGroupBannedMember(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("GroupPostTest4");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1", "Original content");

            using (var context = await dbFactory.CreateDbContextAsync())
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

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("GroupPostTest5");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var deleted = await context.GroupPosts.FindAsync(post.Id);
                deleted.Should().BeNull();
            }
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ByModerator_ShouldDeletePost()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("GroupPostTest6");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var author = TestDataFactory.CreateTestUser(id: "user1");
            var moderator = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user3");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var modGroupUser = TestDataFactory.CreateTestGroupUser(1, "user2", isModerator: true);

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("GroupPostTest7");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var updatedPost = await context.GroupPosts.FindAsync(post.Id);
                updatedPost!.CommentCount.Should().Be(1);
            }
        }

        [Fact]
        public async Task AddCommentAsync_NonMember_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("GroupPostTest8");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var author = TestDataFactory.CreateTestUser(id: "user2");
            var nonMember = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user2");

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("GroupPostTest9");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1", commentCount: 1);
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var deleted = await context.GroupPostComments.FindAsync(comment.Id);
                deleted.Should().BeNull();
                var updatedPost = await context.GroupPosts.FindAsync(post.Id);
                updatedPost!.CommentCount.Should().Be(0);
            }
        }

        #endregion

        #region Like Operations Tests

        [Fact]
        public async Task LikePostAsync_ValidUser_ShouldCreateLike()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("GroupPostTest10");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var (success, errorMessage) = await service.LikePostAsync(post.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var like = await context.GroupPostLikes.FirstOrDefaultAsync(l => l.PostId == post.Id && l.UserId == "user1");
                like.Should().NotBeNull();
                var updatedPost = await context.GroupPosts.FindAsync(post.Id);
                updatedPost!.LikeCount.Should().Be(1);
            }
        }

        [Fact]
        public async Task UnlikePostAsync_ExistingLike_ShouldRemoveLike()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("GroupPostTest11");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1", likeCount: 1);
            var like = TestDataFactory.CreateTestGroupPostLike(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var (success, errorMessage) = await service.UnlikePostAsync(post.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
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
        public async Task LikeCommentAsync_ValidUser_ShouldCreateLike()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("GroupPostTest12");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var (success, errorMessage) = await service.LikeCommentAsync(comment.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var like = await context.GroupPostCommentLikes.FirstOrDefaultAsync(l => l.CommentId == comment.Id && l.UserId == "user1");
                like.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task UnlikeCommentAsync_ExistingLike_ShouldRemoveLike()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("GroupPostTest13");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "user1");
            var like = TestDataFactory.CreateTestGroupPostCommentLike(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var (success, errorMessage) = await service.UnlikeCommentAsync(comment.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("GroupPostTest14");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var mute = await context.GroupPostNotificationMutes.FirstOrDefaultAsync(m => m.PostId == post.Id && m.UserId == "user1");
                mute.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task UnmutePostNotificationsAsync_ShouldRemoveMute()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("GroupPostTest15");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var mute = TestDataFactory.CreateTestGroupPostNotificationMute(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var removedMute = await context.GroupPostNotificationMutes.FirstOrDefaultAsync(m => m.PostId == post.Id && m.UserId == "user1");
                removedMute.Should().BeNull();
            }
        }

        [Fact]
        public async Task IsPostNotificationMutedAsync_MutedPost_ShouldReturnTrue()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("GroupPostTest16");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var mute = TestDataFactory.CreateTestGroupPostNotificationMute(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("GroupPostTest17");
            var logger = CreateMockLogger<GroupPostService>();
            var service = new GroupPostService(dbFactory, logger, CreateMockNotificationService(), CreateMockUserPreferenceService());

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
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

        #endregion
    }
}
