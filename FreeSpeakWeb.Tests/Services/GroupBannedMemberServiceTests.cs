using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    public class GroupBannedMemberServiceTests : TestBase
    {
        private GroupBannedMemberService CreateService(string dbName)
        {
            var dbFactory = CreateDbContextFactory(dbName);
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            return new GroupBannedMemberService(dbFactory, logger, auditLogRepository);
        }

        [Fact]
        public async Task BanUserAsync_ByAdmin_ShouldBanUser()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest1");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

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

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var ban = await context.GroupBannedMembers.FirstOrDefaultAsync(b => b.GroupId == group.Id && b.UserId == "user1");
                ban.Should().NotBeNull();

                // User should be removed from group
                var membership = await context.GroupUsers.FirstOrDefaultAsync(gu => gu.GroupId == group.Id && gu.UserId == "user1");
                membership.Should().BeNull();
            }
        }

        [Fact]
        public async Task BanUserAsync_ByModerator_ShouldBanUser()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest2");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

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
        public async Task BanUserAsync_ByRegularUser_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest3");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

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
        public async Task BanUserAsync_GroupCreator_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest4");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

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

        [Fact]
        public async Task BanUserAsync_ModeratorBanningAdmin_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest5");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var moderator = TestDataFactory.CreateTestUser(id: "mod1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var modGroupUser = TestDataFactory.CreateTestGroupUser(1, "mod1", isModerator: true);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(admin, moderator);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                adminGroupUser.GroupId = group.Id;
                modGroupUser.GroupId = group.Id;
                context.GroupUsers.AddRange(adminGroupUser, modGroupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.BanUserAsync(group.Id, "admin1", "mod1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("Moderators cannot ban administrators");
        }

        [Fact]
        public async Task BanUserAsync_AlreadyBanned_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest6");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var userGroupUser = TestDataFactory.CreateTestGroupUser(1, "user1"); // Add as member first
            var ban = TestDataFactory.CreateTestGroupBannedMember(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(admin, user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                adminGroupUser.GroupId = group.Id;
                userGroupUser.GroupId = group.Id;
                ban.GroupId = group.Id;
                context.GroupUsers.AddRange(adminGroupUser, userGroupUser);
                await context.SaveChangesAsync();
                // Ban the user after they're a member
                context.GroupBannedMembers.Add(ban);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.BanUserAsync(group.Id, "user1", "admin1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("already banned");
        }

        [Fact]
        public async Task UnbanUserAsync_ByAdmin_ShouldUnbanUser()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest7");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);
            var ban = TestDataFactory.CreateTestGroupBannedMember(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(admin, user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                adminGroupUser.GroupId = group.Id;
                ban.GroupId = group.Id;
                context.GroupUsers.Add(adminGroupUser);
                context.GroupBannedMembers.Add(ban);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.UnbanUserAsync(group.Id, "user1", "admin1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var removedBan = await context.GroupBannedMembers.FirstOrDefaultAsync(b => b.GroupId == group.Id && b.UserId == "user1");
                removedBan.Should().BeNull();
            }
        }

        [Fact]
        public async Task UnbanUserAsync_NotBanned_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest8");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var adminGroupUser = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(admin, user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                adminGroupUser.GroupId = group.Id;
                context.GroupUsers.Add(adminGroupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.UnbanUserAsync(group.Id, "user1", "admin1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not banned");
        }

        [Fact]
        public async Task IsUserBannedAsync_BannedUser_ShouldReturnTrue()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest9");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var ban = TestDataFactory.CreateTestGroupBannedMember(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                ban.GroupId = group.Id;
                context.GroupBannedMembers.Add(ban);
                await context.SaveChangesAsync();
            }

            // Act
            var isBanned = await service.IsUserBannedAsync(group.Id, "user1");

            // Assert
            isBanned.Should().BeTrue();
        }

        [Fact]
        public async Task IsUserBannedAsync_NotBanned_ShouldReturnFalse()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest10");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
            }

            // Act
            var isBanned = await service.IsUserBannedAsync(group.Id, "user1");

            // Assert
            isBanned.Should().BeFalse();
        }

        [Fact]
        public async Task GetBannedMembersAsync_ShouldReturnBannedMembers()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest11");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("creator1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var ban1 = TestDataFactory.CreateTestGroupBannedMember(group.Id, "user1");
                var ban2 = TestDataFactory.CreateTestGroupBannedMember(group.Id, "user2");
                context.GroupBannedMembers.AddRange(ban1, ban2);
                await context.SaveChangesAsync();
            }

            // Act
            var bannedMembers = await service.GetBannedMembersAsync(group.Id);

            // Assert
            bannedMembers.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetBannedMemberCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest12");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var user3 = TestDataFactory.CreateTestUser(id: "user3");
            var group = TestDataFactory.CreateTestGroup("creator1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2, user3);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var ban1 = TestDataFactory.CreateTestGroupBannedMember(group.Id, "user1");
                var ban2 = TestDataFactory.CreateTestGroupBannedMember(group.Id, "user2");
                var ban3 = TestDataFactory.CreateTestGroupBannedMember(group.Id, "user3");
                context.GroupBannedMembers.AddRange(ban1, ban2, ban3);
                await context.SaveChangesAsync();
            }

            // Act
            var count = await service.GetBannedMemberCountAsync(group.Id);

            // Assert
            count.Should().Be(3);
        }

        [Fact]
        public async Task GetUserBansAsync_ShouldReturnAllGroupsUserIsBannedFrom()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("BanTest13");
            var logger = CreateMockLogger<GroupBannedMemberService>();
            var auditLogRepository = CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(dbFactory, logger, auditLogRepository);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group1 = TestDataFactory.CreateTestGroup("creator1", "Group 1");
            var group2 = TestDataFactory.CreateTestGroup("creator1", "Group 2");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.AddRange(group1, group2);
                await context.SaveChangesAsync();

                var ban1 = TestDataFactory.CreateTestGroupBannedMember(group1.Id, "user1");
                var ban2 = TestDataFactory.CreateTestGroupBannedMember(group2.Id, "user1");
                context.GroupBannedMembers.AddRange(ban1, ban2);
                await context.SaveChangesAsync();
            }

            // Act
            var bans = await service.GetUserBansAsync("user1");

            // Assert
            bans.Should().HaveCount(2);
        }
    }
}
