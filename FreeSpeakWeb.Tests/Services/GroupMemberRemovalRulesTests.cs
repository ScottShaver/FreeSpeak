using FluentAssertions;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Verifies who may remove administrators from a group via <see cref="GroupMemberService.RemoveMemberAsync"/>.
    /// </summary>
    public class GroupMemberRemovalRulesTests : TestBase
    {
        private GroupMemberService CreateService(TestRepositoryFactory repoFactory, bool requesterIsSystemAdmin = false)
        {
            var auditLogRepo = MockRepositories.CreateMockAuditLogRepository();
            var notificationService = new NotificationService(
                repoFactory.CreateNotificationRepository(), repoFactory.ContextFactory,
                new Mock<ILogger<NotificationService>>().Object,
                new Mock<IServiceScopeFactory>().Object, auditLogRepo.Object);
            var roleService = MockRepositories.CreateMockRoleService();
            roleService.Setup(r => r.IsSystemAdministratorAsync("sysadmin")).ReturnsAsync(requesterIsSystemAdmin);
            return new GroupMemberService(
                repoFactory.ContextFactory,
                repoFactory.CreateGroupMemberRepository(),
                repoFactory.CreateGroupRepository(),
                new Mock<ILogger<GroupMemberService>>().Object,
                auditLogRepo.Object,
                notificationService,
                roleService.Object);
        }

        private static async Task<int> SeedAsync(TestRepositoryFactory repoFactory)
        {
            using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
            context.Users.AddRange(
                TestDataFactory.CreateTestUser(id: "creator"),
                TestDataFactory.CreateTestUser(id: "admin1"),
                TestDataFactory.CreateTestUser(id: "admin2"),
                TestDataFactory.CreateTestUser(id: "member"),
                TestDataFactory.CreateTestUser(id: "sysadmin"));
            var group = TestDataFactory.CreateTestGroup("creator");
            context.Groups.Add(group);
            await context.SaveChangesAsync();
            context.GroupUsers.AddRange(
                TestDataFactory.CreateTestGroupUser(group.Id, "creator", isAdmin: true),
                TestDataFactory.CreateTestGroupUser(group.Id, "admin1", isAdmin: true),
                TestDataFactory.CreateTestGroupUser(group.Id, "admin2", isAdmin: true),
                TestDataFactory.CreateTestGroupUser(group.Id, "member"));
            await context.SaveChangesAsync();
            return group.Id;
        }

        [Fact]
        public async Task RemoveMemberAsync_AdminRemovingAdmin_IsRejected()
        {
            var repoFactory = CreateTestRepositoryFactory("RemoveAdminByAdmin");
            var groupId = await SeedAsync(repoFactory);

            var (success, error) = await CreateService(repoFactory).RemoveMemberAsync(groupId, "admin2", "admin1");

            success.Should().BeFalse();
            error.Should().Contain("creator");
        }

        [Fact]
        public async Task RemoveMemberAsync_AdminRemovingRegularMember_Succeeds()
        {
            var repoFactory = CreateTestRepositoryFactory("RemoveMemberByAdmin");
            var groupId = await SeedAsync(repoFactory);

            var (success, _) = await CreateService(repoFactory).RemoveMemberAsync(groupId, "member", "admin1");

            success.Should().BeTrue();
        }

        [Fact]
        public async Task RemoveMemberAsync_CreatorRemovingAdmin_Succeeds()
        {
            var repoFactory = CreateTestRepositoryFactory("RemoveAdminByCreator");
            var groupId = await SeedAsync(repoFactory);

            var (success, _) = await CreateService(repoFactory).RemoveMemberAsync(groupId, "admin1", "creator");

            success.Should().BeTrue();
        }

        [Fact]
        public async Task RemoveMemberAsync_SystemAdminRemovingAdmin_Succeeds()
        {
            var repoFactory = CreateTestRepositoryFactory("RemoveAdminBySysAdmin");
            var groupId = await SeedAsync(repoFactory);

            var (success, _) = await CreateService(repoFactory, requesterIsSystemAdmin: true)
                .RemoveMemberAsync(groupId, "admin1", "sysadmin");

            success.Should().BeTrue();
        }

        [Fact]
        public async Task RemoveMemberAsync_CreatorCanNeverBeRemoved()
        {
            var repoFactory = CreateTestRepositoryFactory("RemoveCreator");
            var groupId = await SeedAsync(repoFactory);

            var (success, _) = await CreateService(repoFactory, requesterIsSystemAdmin: true)
                .RemoveMemberAsync(groupId, "creator", "sysadmin");

            success.Should().BeFalse();
        }
    }
}
