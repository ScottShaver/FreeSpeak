using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Verifies the denormalized Group.MemberCount stays consistent across join, leave, remove, ban and unban.
    /// </summary>
    public class GroupMemberCountTests : TestBase
    {
        private GroupMemberService CreateMemberService(TestRepositoryFactory repoFactory)
        {
            var auditLogRepo = MockRepositories.CreateMockAuditLogRepository();
            var notificationService = new NotificationService(
                repoFactory.CreateNotificationRepository(), repoFactory.ContextFactory,
                new Mock<ILogger<NotificationService>>().Object,
                new Mock<IServiceScopeFactory>().Object, auditLogRepo.Object);
            return new GroupMemberService(
                repoFactory.ContextFactory,
                repoFactory.CreateGroupMemberRepository(),
                repoFactory.CreateGroupRepository(),
                new Mock<ILogger<GroupMemberService>>().Object,
                auditLogRepo.Object,
                notificationService,
                CreateMockRoleService());
        }

        private static async Task<int> SeedGroupAsync(TestRepositoryFactory repoFactory, int staleCount)
        {
            using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
            context.Users.AddRange(
                TestDataFactory.CreateTestUser(id: "creator"),
                TestDataFactory.CreateTestUser(id: "admin"),
                TestDataFactory.CreateTestUser(id: "m1"),
                TestDataFactory.CreateTestUser(id: "m2"),
                TestDataFactory.CreateTestUser(id: "joiner"));
            var group = TestDataFactory.CreateTestGroup("creator");
            context.Groups.Add(group);
            await context.SaveChangesAsync();
            group.MemberCount = staleCount;
            context.GroupUsers.AddRange(
                TestDataFactory.CreateTestGroupUser(group.Id, "creator", isAdmin: true),
                TestDataFactory.CreateTestGroupUser(group.Id, "admin", isAdmin: true),
                TestDataFactory.CreateTestGroupUser(group.Id, "m1"),
                TestDataFactory.CreateTestGroupUser(group.Id, "m2"));
            await context.SaveChangesAsync();
            return group.Id;
        }

        private async Task<int> GetCountAsync(TestRepositoryFactory repoFactory, int groupId)
        {
            using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
            return (await context.Groups.AsNoTracking().FirstAsync(g => g.Id == groupId)).MemberCount;
        }

        [Fact]
        public async Task BanUserAsync_DecrementsMemberCount()
        {
            var repoFactory = CreateTestRepositoryFactory("MemberCountBan");
            var groupId = await SeedGroupAsync(repoFactory, staleCount: 4);
            var bans = new GroupBannedMemberService(repoFactory.ContextFactory, CreateMockLogger<GroupBannedMemberService>(), CreateMockAuditLogRepository(), CreateMockRoleService());

            var (success, _) = await bans.BanUserAsync(groupId, "m1", "admin");

            success.Should().BeTrue();
            (await GetCountAsync(repoFactory, groupId)).Should().Be(3);
        }

        [Fact]
        public async Task BanUserAsync_HealsAlreadyStaleCount()
        {
            var repoFactory = CreateTestRepositoryFactory("MemberCountBanStale");
            var groupId = await SeedGroupAsync(repoFactory, staleCount: 9);
            var bans = new GroupBannedMemberService(repoFactory.ContextFactory, CreateMockLogger<GroupBannedMemberService>(), CreateMockAuditLogRepository(), CreateMockRoleService());

            await bans.BanUserAsync(groupId, "m1", "admin");

            (await GetCountAsync(repoFactory, groupId)).Should().Be(3);
        }

        [Fact]
        public async Task UnbanUserAsync_DoesNotChangeCountAndKeepsItConsistent()
        {
            var repoFactory = CreateTestRepositoryFactory("MemberCountUnban");
            var groupId = await SeedGroupAsync(repoFactory, staleCount: 4);
            var bans = new GroupBannedMemberService(repoFactory.ContextFactory, CreateMockLogger<GroupBannedMemberService>(), CreateMockAuditLogRepository(), CreateMockRoleService());
            await bans.BanUserAsync(groupId, "m1", "admin");

            var (success, _) = await bans.UnbanUserAsync(groupId, "m1", "admin");

            success.Should().BeTrue();
            (await GetCountAsync(repoFactory, groupId)).Should().Be(3);
        }

        [Fact]
        public async Task LeaveGroupAsync_DecrementsMemberCount()
        {
            var repoFactory = CreateTestRepositoryFactory("MemberCountLeave");
            var groupId = await SeedGroupAsync(repoFactory, staleCount: 4);

            var (success, _) = await CreateMemberService(repoFactory).LeaveGroupAsync(groupId, "m2");

            success.Should().BeTrue();
            (await GetCountAsync(repoFactory, groupId)).Should().Be(3);
        }

        [Fact]
        public async Task RemoveMemberAsync_DecrementsMemberCount()
        {
            var repoFactory = CreateTestRepositoryFactory("MemberCountRemove");
            var groupId = await SeedGroupAsync(repoFactory, staleCount: 4);

            var (success, _) = await CreateMemberService(repoFactory).RemoveMemberAsync(groupId, "m2", "creator");

            success.Should().BeTrue();
            (await GetCountAsync(repoFactory, groupId)).Should().Be(3);
        }

        [Fact]
        public async Task JoinGroupAsync_IncrementsMemberCount()
        {
            var repoFactory = CreateTestRepositoryFactory("MemberCountJoin");
            var groupId = await SeedGroupAsync(repoFactory, staleCount: 4);

            var (success, _) = await CreateMemberService(repoFactory).JoinGroupAsync(groupId, "joiner", true);

            success.Should().BeTrue();
            (await GetCountAsync(repoFactory, groupId)).Should().Be(5);
        }
    }
}
