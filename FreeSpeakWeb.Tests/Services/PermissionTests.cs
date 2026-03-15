using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// P2 Permission Tests - Verifies admin/moderator delete permissions and ban blocking
    /// for group posts, comments, and user actions.
    /// </summary>
    public class PermissionTests : TestBase
    {
        #region Test Infrastructure

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
            var notificationRepo = MockRepositories.CreateMockNotificationRepository();            return new NotificationService(notificationRepo.Object, dbFactory.Object, logger.Object, scopeFactory.Object);
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

        private static GroupAccessValidator CreateMockGroupAccessValidator(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            var logger = new Mock<ILogger<GroupAccessValidator>>();
            return new GroupAccessValidator(dbFactory, logger.Object);
        }

        private GroupPostService CreateGroupPostService(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            var pointsLogger = CreateMockLogger<GroupPointsService>();
            var groupPointsService = new GroupPointsService(dbFactory, pointsLogger);

            return new GroupPostService(
                dbFactory,
                MockRepositories.CreateMockGroupPostRepository().Object,
                MockRepositories.CreateMockGroupCommentRepository().Object,
                MockRepositories.CreateMockGroupPostLikeRepository().Object,
                MockRepositories.CreateMockGroupCommentLikeRepository().Object,
                MockRepositories.CreateMockGroupRepository().Object,
                MockRepositories.CreateMockNotificationRepository().Object,
                CreateMockLogger<GroupPostService>(),
                CreateMockNotificationService(),
                CreateMockUserPreferenceService(),
                CreateMockWebHostEnvironment(),
                CreateMockPostNotificationHelper(),
                CreateMockGroupAccessValidator(dbFactory),
                MockRepositories.CreateMockAuditLogRepository().Object,
                groupPointsService);
        }

        private GroupBannedMemberService CreateGroupBannedMemberService(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            return new GroupBannedMemberService(dbFactory, CreateMockLogger<GroupBannedMemberService>(), MockRepositories.CreateMockAuditLogRepository().Object);
        }

        #endregion

        #region Admin Delete Post Tests

        [Fact]
        public async Task AdminDelete_OtherUserPost_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_AdminDeletePost1");
            var service = CreateGroupPostService(dbFactory);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var author = TestDataFactory.CreateTestUser(id: "author1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var authorGroupUser = TestDataFactory.CreateTestGroupUser(1, "author1");
            var post = TestDataFactory.CreateTestGroupPost(1, "author1", "Post by regular user");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(admin, author);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                adminGroupUser.GroupId = group.Id;
                authorGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.AddRange(adminGroupUser, authorGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "admin1");

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
        public async Task AdminDelete_ModeratorPost_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_AdminDeleteModPost");
            var service = CreateGroupPostService(dbFactory);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var moderator = TestDataFactory.CreateTestUser(id: "mod1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var modGroupUser = TestDataFactory.CreateTestGroupUser(1, "mod1", isModerator: true);
            var post = TestDataFactory.CreateTestGroupPost(1, "mod1", "Post by moderator");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(admin, moderator);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                adminGroupUser.GroupId = group.Id;
                modGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.AddRange(adminGroupUser, modGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "admin1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task AdminDelete_OwnPost_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_AdminDeleteOwnPost");
            var service = CreateGroupPostService(dbFactory);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var post = TestDataFactory.CreateTestGroupPost(1, "admin1", "Post by admin");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(admin);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                adminGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.Add(adminGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "admin1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        #endregion

        #region Moderator Delete Post Tests

        [Fact]
        public async Task ModeratorDelete_OtherUserPost_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_ModDeletePost1");
            var service = CreateGroupPostService(dbFactory);

            var moderator = TestDataFactory.CreateTestUser(id: "mod1");
            var author = TestDataFactory.CreateTestUser(id: "author1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var modGroupUser = TestDataFactory.CreateTestGroupUser(1, "mod1", isModerator: true);
            var authorGroupUser = TestDataFactory.CreateTestGroupUser(1, "author1");
            var post = TestDataFactory.CreateTestGroupPost(1, "author1", "Post by regular user");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(moderator, author);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                modGroupUser.GroupId = group.Id;
                authorGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.AddRange(modGroupUser, authorGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "mod1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task ModeratorDelete_AdminPost_ShouldSucceed()
        {
            // Arrange - Moderators CAN delete admin posts (they have moderation permissions)
            var dbFactory = CreateDbContextFactory("PermTest_ModDeleteAdminPost");
            var service = CreateGroupPostService(dbFactory);

            var moderator = TestDataFactory.CreateTestUser(id: "mod1");
            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var modGroupUser = TestDataFactory.CreateTestGroupUser(1, "mod1", isModerator: true);
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var post = TestDataFactory.CreateTestGroupPost(1, "admin1", "Post by admin");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(moderator, admin);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                modGroupUser.GroupId = group.Id;
                adminGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.AddRange(modGroupUser, adminGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "mod1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task ModeratorDelete_OwnPost_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_ModDeleteOwnPost");
            var service = CreateGroupPostService(dbFactory);

            var moderator = TestDataFactory.CreateTestUser(id: "mod1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var modGroupUser = TestDataFactory.CreateTestGroupUser(1, "mod1", isModerator: true);
            var post = TestDataFactory.CreateTestGroupPost(1, "mod1", "Post by moderator");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(moderator);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                modGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.Add(modGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "mod1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        #endregion

        #region Regular User Delete Tests (Unauthorized)

        [Fact]
        public async Task RegularUserDelete_OtherUserPost_ShouldReturnUnauthorized()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_UserDeleteOther");
            var service = CreateGroupPostService(dbFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var user1GroupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var user2GroupUser = TestDataFactory.CreateTestGroupUser(1, "user2");
            var post = TestDataFactory.CreateTestGroupPost(1, "user2", "Post by user2");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                user1GroupUser.GroupId = group.Id;
                user2GroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.AddRange(user1GroupUser, user2GroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "user1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not authorized");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var notDeleted = await context.GroupPosts.FindAsync(post.Id);
                notDeleted.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task RegularUserDelete_OwnPost_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_UserDeleteOwn");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var userGroupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1", "My own post");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                userGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.Add(userGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task NonMemberDelete_AnyPost_ShouldReturnUnauthorized()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_NonMemberDelete");
            var service = CreateGroupPostService(dbFactory);

            var nonMember = TestDataFactory.CreateTestUser(id: "nonmember");
            var author = TestDataFactory.CreateTestUser(id: "author1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var authorGroupUser = TestDataFactory.CreateTestGroupUser(1, "author1");
            var post = TestDataFactory.CreateTestGroupPost(1, "author1", "Post by member");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(nonMember, author);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                authorGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.Add(authorGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteGroupPostAsync(post.Id, "nonmember");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not authorized");
        }

        #endregion

        #region Ban Blocking Action Tests

        [Fact]
        public async Task BannedUser_CannotCreatePost()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_BanBlockPost");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var userGroupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var ban = TestDataFactory.CreateTestGroupBannedMember(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                userGroupUser.GroupId = group.Id;
                ban.GroupId = group.Id;
                context.GroupUsers.Add(userGroupUser);
                context.GroupBannedMembers.Add(ban);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage, post) = await service.CreateGroupPostAsync(group.Id, "user1", "Trying to post while banned");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().NotBeNullOrEmpty();
            post.Should().BeNull();
        }

        [Fact]
        public async Task BannedUser_CannotComment()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_BanBlockComment");
            var service = CreateGroupPostService(dbFactory);

            var bannedUser = TestDataFactory.CreateTestUser(id: "banned1");
            var author = TestDataFactory.CreateTestUser(id: "author1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var bannedGroupUser = TestDataFactory.CreateTestGroupUser(1, "banned1");
            var authorGroupUser = TestDataFactory.CreateTestGroupUser(1, "author1");
            var ban = TestDataFactory.CreateTestGroupBannedMember(1, "banned1");
            var post = TestDataFactory.CreateTestGroupPost(1, "author1", "Valid post");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(bannedUser, author);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                bannedGroupUser.GroupId = group.Id;
                authorGroupUser.GroupId = group.Id;
                ban.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.AddRange(bannedGroupUser, authorGroupUser);
                context.GroupBannedMembers.Add(ban);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage, comment) = await service.AddCommentAsync(post.Id, "banned1", "Trying to comment while banned");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().NotBeNullOrEmpty();
            comment.Should().BeNull();
        }

        [Fact]
        public async Task BannedUser_CanStillEditExistingPost()
        {
            // Arrange - User creates post, then gets banned. Banned users can still edit their existing posts.
            var dbFactory = CreateDbContextFactory("PermTest_BanBlockEdit");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var userGroupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var post = TestDataFactory.CreateTestGroupPost(1, "user1", "Original content");
            var ban = TestDataFactory.CreateTestGroupBannedMember(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, admin);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                userGroupUser.GroupId = group.Id;
                adminGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                ban.GroupId = group.Id;
                context.GroupUsers.AddRange(userGroupUser, adminGroupUser);
                context.GroupPosts.Add(post);
                context.GroupBannedMembers.Add(ban);
                await context.SaveChangesAsync();
            }

            // Act - Banned users can still edit their existing posts (only creating new posts is blocked)
            var (success, errorMessage, images) = await service.UpdateGroupPostAsync(post.Id, "user1", "Edited while banned", null);

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task AdminBan_RegularUser_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_AdminBanUser");
            var service = CreateGroupBannedMemberService(dbFactory);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var userGroupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(admin, user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                adminGroupUser.GroupId = group.Id;
                userGroupUser.GroupId = group.Id;
                context.GroupUsers.AddRange(adminGroupUser, userGroupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.BanUserAsync(group.Id, "user1", "admin1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            // Verify user is actually banned
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var ban = await context.GroupBannedMembers
                    .FirstOrDefaultAsync(b => b.GroupId == group.Id && b.UserId == "user1");
                ban.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task ModeratorBan_RegularUser_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_ModBanUser");
            var service = CreateGroupBannedMemberService(dbFactory);

            var moderator = TestDataFactory.CreateTestUser(id: "mod1");
            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var modGroupUser = TestDataFactory.CreateTestGroupUser(1, "mod1", isModerator: true);
            var userGroupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(moderator, user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                modGroupUser.GroupId = group.Id;
                userGroupUser.GroupId = group.Id;
                context.GroupUsers.AddRange(modGroupUser, userGroupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.BanUserAsync(group.Id, "user1", "mod1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task ModeratorBan_Admin_ShouldReturnUnauthorized()
        {
            // Arrange - Moderators cannot ban admins
            var dbFactory = CreateDbContextFactory("PermTest_ModBanAdmin");
            var service = CreateGroupBannedMemberService(dbFactory);

            var moderator = TestDataFactory.CreateTestUser(id: "mod1");
            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var modGroupUser = TestDataFactory.CreateTestGroupUser(1, "mod1", isModerator: true);
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(moderator, admin);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                modGroupUser.GroupId = group.Id;
                adminGroupUser.GroupId = group.Id;
                context.GroupUsers.AddRange(modGroupUser, adminGroupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.BanUserAsync(group.Id, "admin1", "mod1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("Moderators cannot ban administrators");
        }

        [Fact]
        public async Task RegularUserBan_AnyUser_ShouldReturnUnauthorized()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_UserBanOther");
            var service = CreateGroupBannedMemberService(dbFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var user1GroupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var user2GroupUser = TestDataFactory.CreateTestGroupUser(1, "user2");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                user1GroupUser.GroupId = group.Id;
                user2GroupUser.GroupId = group.Id;
                context.GroupUsers.AddRange(user1GroupUser, user2GroupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.BanUserAsync(group.Id, "user2", "user1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("must be an admin or moderator");
        }

        [Fact]
        public async Task AdminBan_GroupCreator_ShouldReturnError()
        {
            // Arrange - Cannot ban the group creator
            var dbFactory = CreateDbContextFactory("PermTest_BanCreator");
            var service = CreateGroupBannedMemberService(dbFactory);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var creator = TestDataFactory.CreateTestUser(id: "creator1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var creatorGroupUser = TestDataFactory.CreateTestGroupUser(1, "creator1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(admin, creator);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                adminGroupUser.GroupId = group.Id;
                creatorGroupUser.GroupId = group.Id;
                context.GroupUsers.AddRange(adminGroupUser, creatorGroupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.BanUserAsync(group.Id, "creator1", "admin1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("Cannot ban the group creator");
        }

        #endregion

        #region Admin/Moderator Delete Comment Tests

        [Fact]
        public async Task AdminDelete_OtherUserComment_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_AdminDeleteComment");
            var service = CreateGroupPostService(dbFactory);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var author = TestDataFactory.CreateTestUser(id: "author1");
            var commenter = TestDataFactory.CreateTestUser(id: "commenter1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var authorGroupUser = TestDataFactory.CreateTestGroupUser(1, "author1");
            var commenterGroupUser = TestDataFactory.CreateTestGroupUser(1, "commenter1");
            var post = TestDataFactory.CreateTestGroupPost(1, "author1", "Test post");
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "commenter1", "Test comment");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(admin, author, commenter);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                adminGroupUser.GroupId = group.Id;
                authorGroupUser.GroupId = group.Id;
                commenterGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.AddRange(adminGroupUser, authorGroupUser, commenterGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                comment.PostId = post.Id;
                context.GroupPostComments.Add(comment);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteCommentAsync(comment.Id, "admin1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task ModeratorDelete_OtherUserComment_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_ModDeleteComment");
            var service = CreateGroupPostService(dbFactory);

            var moderator = TestDataFactory.CreateTestUser(id: "mod1");
            var author = TestDataFactory.CreateTestUser(id: "author1");
            var commenter = TestDataFactory.CreateTestUser(id: "commenter1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var modGroupUser = TestDataFactory.CreateTestGroupUser(1, "mod1", isModerator: true);
            var authorGroupUser = TestDataFactory.CreateTestGroupUser(1, "author1");
            var commenterGroupUser = TestDataFactory.CreateTestGroupUser(1, "commenter1");
            var post = TestDataFactory.CreateTestGroupPost(1, "author1", "Test post");
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "commenter1", "Test comment");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(moderator, author, commenter);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                modGroupUser.GroupId = group.Id;
                authorGroupUser.GroupId = group.Id;
                commenterGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.AddRange(modGroupUser, authorGroupUser, commenterGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                comment.PostId = post.Id;
                context.GroupPostComments.Add(comment);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteCommentAsync(comment.Id, "mod1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task RegularUserDelete_OtherUserComment_ShouldReturnUnauthorized()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_UserDeleteOtherComment");
            var service = CreateGroupPostService(dbFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var user1GroupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var user2GroupUser = TestDataFactory.CreateTestGroupUser(1, "user2");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1", "Test post");
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "user2", "Test comment");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                user1GroupUser.GroupId = group.Id;
                user2GroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.AddRange(user1GroupUser, user2GroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                comment.PostId = post.Id;
                context.GroupPostComments.Add(comment);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteCommentAsync(comment.Id, "user1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not authorized");
        }

        [Fact]
        public async Task RegularUserDelete_OwnComment_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PermTest_UserDeleteOwnComment");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var userGroupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1", "Test post");
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "user1", "My own comment");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                userGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.Add(userGroupUser);
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
        }

        #endregion

        #region Post Author Delete Comment Tests

        [Fact]
        public async Task PostAuthorDelete_OtherUserCommentOnOwnPost_ShouldReturnUnauthorized()
        {
            // Arrange - Post author CANNOT delete other users' comments on their post
            // Only admins/moderators or comment authors can delete comments
            var dbFactory = CreateDbContextFactory("PermTest_AuthorDeleteComment");
            var service = CreateGroupPostService(dbFactory);

            var author = TestDataFactory.CreateTestUser(id: "author1");
            var commenter = TestDataFactory.CreateTestUser(id: "commenter1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var authorGroupUser = TestDataFactory.CreateTestGroupUser(1, "author1");
            var commenterGroupUser = TestDataFactory.CreateTestGroupUser(1, "commenter1");
            var post = TestDataFactory.CreateTestGroupPost(1, "author1", "My post");
            var comment = TestDataFactory.CreateTestGroupPostComment(1, "commenter1", "Comment on author's post");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(author, commenter);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                authorGroupUser.GroupId = group.Id;
                commenterGroupUser.GroupId = group.Id;
                post.GroupId = group.Id;
                context.GroupUsers.AddRange(authorGroupUser, commenterGroupUser);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                comment.PostId = post.Id;
                context.GroupPostComments.Add(comment);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.DeleteCommentAsync(comment.Id, "author1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not authorized");
        }

        #endregion
    }
}

