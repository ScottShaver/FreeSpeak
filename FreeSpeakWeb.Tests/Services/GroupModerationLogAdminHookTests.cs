using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Verifies that group admin actions (join requests, roles, member removal, settings and rules)
    /// write a group moderation log entry, and that rejected actions write none.
    /// </summary>
    public class GroupModerationLogAdminHookTests : TestBase
    {
        private const string AdminId = "admin1";
        private const string MemberId = "member1";
        private const string RequesterId = "requester1";

        /// <summary>
        /// Seeds an admin (group creator), a member and a prospective member; returns the group id.
        /// </summary>
        private static async Task<int> SeedAsync(TestRepositoryFactory repoFactory)
        {
            using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
            context.Users.AddRange(
                TestDataFactory.CreateTestUser(id: AdminId),
                TestDataFactory.CreateTestUser(id: MemberId),
                TestDataFactory.CreateTestUser(id: RequesterId));
            var group = TestDataFactory.CreateTestGroup(AdminId);
            context.Groups.Add(group);
            await context.SaveChangesAsync();
            context.GroupUsers.AddRange(
                TestDataFactory.CreateTestGroupUser(group.Id, AdminId, isAdmin: true),
                TestDataFactory.CreateTestGroupUser(group.Id, MemberId));
            await context.SaveChangesAsync();
            return group.Id;
        }

        private GroupMemberService CreateMemberService(TestRepositoryFactory repoFactory, IGroupModerationLogService log)
        {
            var auditRepo = MockRepositories.CreateMockAuditLogRepository();
            var notificationService = new NotificationService(
                repoFactory.CreateNotificationRepository(), repoFactory.ContextFactory,
                new Mock<ILogger<NotificationService>>().Object,
                new Mock<IServiceScopeFactory>().Object, auditRepo.Object);
            return new GroupMemberService(
                repoFactory.ContextFactory,
                repoFactory.CreateGroupMemberRepository(),
                repoFactory.CreateGroupRepository(),
                new Mock<ILogger<GroupMemberService>>().Object,
                auditRepo.Object,
                notificationService,
                CreateMockRoleService(),
                log);
        }

        private static async Task<int> SeedJoinRequestAsync(TestRepositoryFactory repoFactory, int groupId)
        {
            using var context = await repoFactory.ContextFactory.CreateDbContextAsync();
            var request = new GroupJoinRequest { GroupId = groupId, UserId = RequesterId };
            context.GroupJoinRequests.Add(request);
            await context.SaveChangesAsync();
            return request.Id;
        }

        private static bool Matches(GroupModerationLogRequest r, int groupId, string actor, GroupModerationAction action, string? target)
            => r.GroupId == groupId && r.ActorUserId == actor && r.Action == action && r.TargetUserId == target;

        [Fact]
        public async Task ApproveJoinRequest_WritesJoinRequestApprovedEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_JoinApprove");
            var groupId = await SeedAsync(repoFactory);
            var requestId = await SeedJoinRequestAsync(repoFactory, groupId);
            var log = new Mock<IGroupModerationLogService>();

            var (success, _) = await CreateMemberService(repoFactory, log.Object).ApproveJoinRequestAsync(requestId, AdminId);

            success.Should().BeTrue();
            log.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r =>
                Matches(r, groupId, AdminId, GroupModerationAction.JoinRequestApproved, RequesterId)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RejectJoinRequest_WritesJoinRequestDeniedEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_JoinDeny");
            var groupId = await SeedAsync(repoFactory);
            var requestId = await SeedJoinRequestAsync(repoFactory, groupId);
            var log = new Mock<IGroupModerationLogService>();

            var (success, _) = await CreateMemberService(repoFactory, log.Object).RejectJoinRequestAsync(requestId, AdminId);

            success.Should().BeTrue();
            log.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r =>
                Matches(r, groupId, AdminId, GroupModerationAction.JoinRequestDenied, RequesterId)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RejectJoinRequest_ByNonAdmin_WritesNoEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_JoinDenyForbidden");
            var groupId = await SeedAsync(repoFactory);
            var requestId = await SeedJoinRequestAsync(repoFactory, groupId);
            var log = new Mock<IGroupModerationLogService>();

            var (success, _) = await CreateMemberService(repoFactory, log.Object).RejectJoinRequestAsync(requestId, MemberId);

            success.Should().BeFalse();
            log.Verify(m => m.LogAsync(It.IsAny<GroupModerationLogRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetModerator_WritesRoleChangedEntryWithOldAndNewRole()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_RoleChange");
            var groupId = await SeedAsync(repoFactory);
            var log = new Mock<IGroupModerationLogService>();

            var (success, _) = await CreateMemberService(repoFactory, log.Object).SetModeratorAsync(groupId, MemberId, true, AdminId);

            success.Should().BeTrue();
            log.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r =>
                Matches(r, groupId, AdminId, GroupModerationAction.MemberRoleChanged, MemberId) && r.Summary == "Member -> Moderator"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetModerator_WhenRoleUnchanged_WritesNoEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_RoleUnchanged");
            var groupId = await SeedAsync(repoFactory);
            var log = new Mock<IGroupModerationLogService>();

            var (success, _) = await CreateMemberService(repoFactory, log.Object).SetModeratorAsync(groupId, MemberId, false, AdminId);

            success.Should().BeTrue();
            log.Verify(m => m.LogAsync(It.IsAny<GroupModerationLogRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RemoveMember_WritesMemberRemovedEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_RemoveMember");
            var groupId = await SeedAsync(repoFactory);
            var log = new Mock<IGroupModerationLogService>();

            var (success, _) = await CreateMemberService(repoFactory, log.Object).RemoveMemberAsync(groupId, MemberId, AdminId);

            success.Should().BeTrue();
            log.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r =>
                Matches(r, groupId, AdminId, GroupModerationAction.MemberRemoved, MemberId)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveMember_ByNonAdmin_WritesNoEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_RemoveMemberForbidden");
            var groupId = await SeedAsync(repoFactory);
            var log = new Mock<IGroupModerationLogService>();

            var (success, _) = await CreateMemberService(repoFactory, log.Object).RemoveMemberAsync(groupId, AdminId, MemberId);

            success.Should().BeFalse();
            log.Verify(m => m.LogAsync(It.IsAny<GroupModerationLogRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateGroup_WithChanges_WritesSettingsChangedEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_Settings");
            var groupId = await SeedAsync(repoFactory);
            var log = new Mock<IGroupModerationLogService>();
            var service = new GroupService(repoFactory.CreateGroupRepository(), repoFactory.CreateUserRepository(),
                repoFactory.ContextFactory, new Mock<ILogger<GroupService>>().Object,
                MockRepositories.CreateMockAuditLogRepository().Object, CreateMemberService(repoFactory, log.Object),
                CreateMockRoleService(), log.Object);

            var (success, _) = await service.UpdateGroupAsync(groupId, AdminId, requiresPostApproval: true);

            success.Should().BeTrue();
            log.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r =>
                r.GroupId == groupId && r.ActorUserId == AdminId && r.Action == GroupModerationAction.GroupSettingsChanged &&
                r.TargetType == GroupModerationTargetType.Group && r.Summary == "RequiresPostApproval"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateGroup_WithoutChanges_WritesNoEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_SettingsNoChange");
            var groupId = await SeedAsync(repoFactory);
            var log = new Mock<IGroupModerationLogService>();
            var service = new GroupService(repoFactory.CreateGroupRepository(), repoFactory.CreateUserRepository(),
                repoFactory.ContextFactory, new Mock<ILogger<GroupService>>().Object,
                MockRepositories.CreateMockAuditLogRepository().Object, CreateMemberService(repoFactory, log.Object),
                CreateMockRoleService(), log.Object);
            bool current;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                current = (await context.Groups.FirstAsync(g => g.Id == groupId)).RequiresPostApproval;
            }

            var (success, _) = await service.UpdateGroupAsync(groupId, AdminId, requiresPostApproval: current);

            success.Should().BeTrue();
            log.Verify(m => m.LogAsync(It.IsAny<GroupModerationLogRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RuleAddEditDelete_WriteMatchingEntries()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_Rules");
            var groupId = await SeedAsync(repoFactory);
            var log = new Mock<IGroupModerationLogService>();
            var service = new GroupRuleService(repoFactory.ContextFactory, new Mock<ILogger<GroupRuleService>>().Object, log.Object);

            var (created, _, rule) = await service.CreateRuleAsync(groupId, AdminId, "Be kind", "No insults");
            var (edited, _) = await service.UpdateRuleAsync(rule!.Id, AdminId, title: "Be very kind");
            var (deleted, _) = await service.DeleteRuleAsync(rule.Id, AdminId);

            created.Should().BeTrue();
            edited.Should().BeTrue();
            deleted.Should().BeTrue();
            log.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r =>
                r.GroupId == groupId && r.ActorUserId == AdminId && r.Action == GroupModerationAction.RuleAdded && r.Summary == "Be kind"),
                It.IsAny<CancellationToken>()), Times.Once);
            log.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r =>
                r.Action == GroupModerationAction.RuleEdited && r.Summary == "Be very kind"), It.IsAny<CancellationToken>()), Times.Once);
            log.Verify(m => m.LogAsync(It.Is<GroupModerationLogRequest>(r =>
                r.Action == GroupModerationAction.RuleDeleted && r.Summary == "Be very kind"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateRule_ByNonAdmin_WritesNoEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("AdminLog_RuleForbidden");
            var groupId = await SeedAsync(repoFactory);
            var log = new Mock<IGroupModerationLogService>();
            var service = new GroupRuleService(repoFactory.ContextFactory, new Mock<ILogger<GroupRuleService>>().Object, log.Object);

            var (created, _, _) = await service.CreateRuleAsync(groupId, MemberId, "Rule", "Desc");

            created.Should().BeFalse();
            log.Verify(m => m.LogAsync(It.IsAny<GroupModerationLogRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
