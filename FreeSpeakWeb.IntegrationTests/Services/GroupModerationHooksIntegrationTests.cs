using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.IntegrationTests.Infrastructure;
using FreeSpeakWeb.Repositories;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeSpeakWeb.IntegrationTests.Services
{
    /// <summary>
    /// Integration tests proving that moderation actions write group Mod Log entries against PostgreSQL:
    /// join approve/deny, role change, member removal, settings change, rule add/edit/delete,
    /// post removal and comment removal, plus that unauthorized actions write nothing.
    /// </summary>
    public class GroupModerationHooksIntegrationTests : IntegrationTestBase
    {
        private const string AdminId = "hook-admin";
        private const string ModId = "hook-mod";
        private const string MemberId = "hook-member";
        private const string OtherId = "hook-other";

        private static ProfilerHelper CreateProfiler()
        {
            var options = new Mock<IOptions<ProfilingSettings>>();
            options.Setup(o => o.Value).Returns(new ProfilingSettings { Enabled = false });
            return new ProfilerHelper(options.Object);
        }

        private static ApplicationUser NewUser(string id) => new()
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpper(),
            Email = $"{id}@example.com",
            NormalizedEmail = $"{id}@example.com".ToUpper(),
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = id,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        private IGroupModerationLogService CreateLogService(IDbContextFactory<ApplicationDbContext> factory)
        {
            var validator = new GroupAccessValidator(factory, NullLogger<GroupAccessValidator>.Instance, new Mock<IRoleService>().Object);
            return new GroupModerationLogService(
                new GroupModerationLogRepository(factory),
                validator,
                MockRepositories.CreateMockAuditLogRepository().Object,
                Options.Create(new GroupModerationLogSettings()),
                NullLogger<GroupModerationLogService>.Instance);
        }

        private NotificationService CreateNotificationService(IDbContextFactory<ApplicationDbContext> factory)
        {
            var repo = new NotificationRepository(factory, NullLogger<NotificationRepository>.Instance, CreateProfiler());
            return new NotificationService(repo, factory, NullLogger<NotificationService>.Instance,
                new Mock<IServiceScopeFactory>().Object, MockRepositories.CreateMockAuditLogRepository().Object);
        }

        private GroupMemberService CreateMemberService(IDbContextFactory<ApplicationDbContext> factory)
        {
            return new GroupMemberService(
                factory,
                new GroupMemberRepository(factory, NullLogger<GroupMemberRepository>.Instance, CreateProfiler()),
                new GroupRepository(factory, NullLogger<GroupRepository>.Instance, CreateProfiler()),
                NullLogger<GroupMemberService>.Instance,
                MockRepositories.CreateMockAuditLogRepository().Object,
                CreateNotificationService(factory),
                new Mock<IRoleService>().Object,
                CreateLogService(factory));
        }

        private GroupPostService CreatePostService(IDbContextFactory<ApplicationDbContext> factory)
        {
            var profiler = CreateProfiler();
            var audit = MockRepositories.CreateMockAuditLogRepository().Object;
            var validator = new GroupAccessValidator(factory, NullLogger<GroupAccessValidator>.Instance, new Mock<IRoleService>().Object);
            var notifications = CreateNotificationService(factory);
            var prefs = new UserPreferenceService(factory, NullLogger<UserPreferenceService>.Instance);
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

            return new GroupPostService(
                factory,
                new GroupPostRepository(factory, NullLogger<GroupPostRepository>.Instance, validator, profiler),
                new GroupCommentRepository(factory, NullLogger<GroupCommentRepository>.Instance, audit, profiler),
                new GroupPostLikeRepository(factory, NullLogger<GroupPostLikeRepository>.Instance, profiler),
                new GroupCommentLikeRepository(factory, NullLogger<GroupCommentLikeRepository>.Instance, profiler),
                new GroupRepository(factory, NullLogger<GroupRepository>.Instance, profiler),
                new NotificationRepository(factory, NullLogger<NotificationRepository>.Instance, profiler),
                NullLogger<GroupPostService>.Instance,
                notifications,
                prefs,
                env.Object,
                new PostNotificationHelper(factory, notifications, prefs, NullLogger<PostNotificationHelper>.Instance),
                validator,
                audit,
                new GroupPointsService(factory, NullLogger<GroupPointsService>.Instance),
                siteSettings: null,
                moderationLog: CreateLogService(factory));
        }

        /// <summary>
        /// Seeds an admin (creator), a moderator, a regular member and an outsider, and a group with a rule-free state.
        /// </summary>
        private async Task<int> SeedGroupAsync(bool requiresJoinApproval = false)
        {
            await using var context = CreateDbContext();
            context.Users.AddRange(NewUser(AdminId), NewUser(ModId), NewUser(MemberId), NewUser(OtherId));
            await context.SaveChangesAsync();
            var group = new Group
            {
                CreatorId = AdminId,
                Name = "Hooks " + Guid.NewGuid().ToString("N"),
                Description = "d",
                IsPublic = true,
                RequiresJoinApproval = requiresJoinApproval,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };
            context.Groups.Add(group);
            await context.SaveChangesAsync();
            context.GroupUsers.AddRange(
                new GroupUser { GroupId = group.Id, UserId = AdminId, IsAdmin = true },
                new GroupUser { GroupId = group.Id, UserId = ModId, IsModerator = true },
                new GroupUser { GroupId = group.Id, UserId = MemberId });
            await context.SaveChangesAsync();
            return group.Id;
        }

        private async Task<List<GroupModerationLogEntry>> GetEntriesAsync(int groupId)
        {
            await using var context = CreateDbContext();
            return await context.GroupModerationLogEntries.Where(e => e.GroupId == groupId).ToListAsync();
        }

        /// <summary>Approving and denying join requests each write one entry with the approver as actor.</summary>
        [Fact]
        public async Task JoinRequestApproveAndDeny_WriteEntries()
        {
            var groupId = await SeedGroupAsync(requiresJoinApproval: true);
            int approveId, denyId;
            await using (var context = CreateDbContext())
            {
                context.Users.Add(NewUser("joiner-1"));
                context.Users.Add(NewUser("joiner-2"));
                await context.SaveChangesAsync();
                var r1 = new GroupJoinRequest { GroupId = groupId, UserId = "joiner-1" };
                var r2 = new GroupJoinRequest { GroupId = groupId, UserId = "joiner-2" };
                context.GroupJoinRequests.AddRange(r1, r2);
                await context.SaveChangesAsync();
                approveId = r1.Id;
                denyId = r2.Id;
            }
            var service = CreateMemberService(CreateDbContextFactory());

            (await service.ApproveJoinRequestAsync(approveId, AdminId)).Success.Should().BeTrue();
            (await service.RejectJoinRequestAsync(denyId, AdminId)).Success.Should().BeTrue();

            var entries = await GetEntriesAsync(groupId);
            entries.Should().Contain(e => e.Action == GroupModerationAction.JoinRequestApproved && e.ActorUserId == AdminId && e.TargetUserId == "joiner-1");
            entries.Should().Contain(e => e.Action == GroupModerationAction.JoinRequestDenied && e.ActorUserId == AdminId && e.TargetUserId == "joiner-2");
        }

        /// <summary>Role changes and member removal write entries; a non-admin attempting them writes nothing.</summary>
        [Fact]
        public async Task RoleChangeAndMemberRemoval_WriteEntries_AndUnauthorizedWritesNothing()
        {
            var groupId = await SeedGroupAsync();
            var service = CreateMemberService(CreateDbContextFactory());

            (await service.SetModeratorAsync(groupId, MemberId, true, MemberId)).Success.Should().BeFalse();
            (await service.RemoveMemberAsync(groupId, ModId, MemberId)).Success.Should().BeFalse();
            (await GetEntriesAsync(groupId)).Should().BeEmpty();

            (await service.SetModeratorAsync(groupId, MemberId, true, AdminId)).Success.Should().BeTrue();
            (await service.RemoveMemberAsync(groupId, ModId, AdminId)).Success.Should().BeTrue();

            var entries = await GetEntriesAsync(groupId);
            entries.Should().Contain(e => e.Action == GroupModerationAction.MemberRoleChanged && e.TargetUserId == MemberId && e.ActorUserId == AdminId);
            entries.Should().Contain(e => e.Action == GroupModerationAction.MemberRemoved && e.TargetUserId == ModId && e.ActorUserId == AdminId);
        }

        /// <summary>Changing group settings writes a GroupSettingsChanged entry.</summary>
        [Fact]
        public async Task SettingsChange_WritesEntry()
        {
            var groupId = await SeedGroupAsync();
            var factory = CreateDbContextFactory();
            var profiler = CreateProfiler();
            var service = new GroupService(
                new GroupRepository(factory, NullLogger<GroupRepository>.Instance, profiler),
                new UserRepository(factory, NullLogger<UserRepository>.Instance, profiler),
                factory,
                NullLogger<GroupService>.Instance,
                MockRepositories.CreateMockAuditLogRepository().Object,
                CreateMemberService(factory),
                new Mock<IRoleService>().Object,
                CreateLogService(factory));

            (await service.UpdateGroupAsync(groupId, MemberId, description: "nope")).Success.Should().BeFalse();
            (await GetEntriesAsync(groupId)).Should().BeEmpty();

            (await service.UpdateGroupAsync(groupId, AdminId, description: "Changed description")).Success.Should().BeTrue();

            var entry = (await GetEntriesAsync(groupId)).Should().ContainSingle().Subject;
            entry.Action.Should().Be(GroupModerationAction.GroupSettingsChanged);
            entry.ActorUserId.Should().Be(AdminId);
        }

        /// <summary>Adding, editing and deleting a rule each write an entry; a member cannot add one.</summary>
        [Fact]
        public async Task RuleAddEditDelete_WriteEntries()
        {
            var groupId = await SeedGroupAsync();
            var factory = CreateDbContextFactory();
            var service = new GroupRuleService(factory, NullLogger<GroupRuleService>.Instance, CreateLogService(factory));

            (await service.CreateRuleAsync(groupId, MemberId, "Nope", "Not allowed")).Success.Should().BeFalse();
            (await GetEntriesAsync(groupId)).Should().BeEmpty();

            var (created, _, rule) = await service.CreateRuleAsync(groupId, AdminId, "Be kind", "No insults");
            created.Should().BeTrue();
            (await service.UpdateRuleAsync(rule!.Id, AdminId, title: "Be very kind")).Success.Should().BeTrue();
            (await service.DeleteRuleAsync(rule.Id, AdminId)).Success.Should().BeTrue();

            var actions = (await GetEntriesAsync(groupId)).Select(e => e.Action).ToList();
            actions.Should().BeEquivalentTo(new[]
            {
                GroupModerationAction.RuleAdded, GroupModerationAction.RuleEdited, GroupModerationAction.RuleDeleted
            });
        }

        /// <summary>Moderator removal of another member's post and comment writes PostRemoved and CommentRemoved once each, as the moderator.</summary>
        [Fact]
        public async Task ModeratorRemovesPostAndComment_WritesEntries_AndMemberCannot()
        {
            var groupId = await SeedGroupAsync();
            int postId, commentId, otherPostId;
            await using (var context = CreateDbContext())
            {
                var post = new GroupPost { GroupId = groupId, AuthorId = MemberId, Content = "post", Status = PostStatus.Posted };
                var otherPost = new GroupPost { GroupId = groupId, AuthorId = AdminId, Content = "admin post", Status = PostStatus.Posted };
                context.GroupPosts.AddRange(post, otherPost);
                await context.SaveChangesAsync();
                var comment = new GroupPostComment { PostId = otherPost.Id, AuthorId = MemberId, Content = "comment" };
                context.GroupPostComments.Add(comment);
                otherPost.CommentCount = 1;
                await context.SaveChangesAsync();
                postId = post.Id;
                otherPostId = otherPost.Id;
                commentId = comment.Id;
            }
            var service = CreatePostService(CreateDbContextFactory());

            (await service.DeleteCommentAsync(commentId, OtherId)).Success.Should().BeFalse();
            (await service.DeleteGroupPostAsync(postId, OtherId)).Success.Should().BeFalse();
            (await GetEntriesAsync(groupId)).Should().BeEmpty();

            (await service.DeleteCommentAsync(commentId, ModId)).Success.Should().BeTrue();
            (await service.DeleteGroupPostAsync(postId, ModId)).Success.Should().BeTrue();

            var entries = await GetEntriesAsync(groupId);
            entries.Should().HaveCount(2);
            entries.Should().ContainSingle(e => e.Action == GroupModerationAction.CommentRemoved && e.ActorUserId == ModId
                                                && e.TargetUserId == MemberId && e.TargetId == commentId.ToString());
            entries.Should().ContainSingle(e => e.Action == GroupModerationAction.PostRemoved && e.ActorUserId == ModId
                                                && e.TargetUserId == MemberId && e.TargetId == postId.ToString());
            await using var verify = CreateDbContext();
            (await verify.GroupPosts.FindAsync(otherPostId))!.CommentCount.Should().Be(0);
        }
    }
}
