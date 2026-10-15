using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
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
    /// Verifies that moderation actions (ban, unban, report decisions, file decisions) write a
    /// group moderation log entry with the correct actor, target and a server-set UTC timestamp.
    /// </summary>
    public class GroupModerationLogHookTests : TestBase
    {
        private const string AdminId = "admin1";
        private const string MemberId = "member1";

        /// <summary>
        /// Builds a real moderation log service over the given context factory.
        /// </summary>
        private IGroupModerationLogService CreateLogService(TestRepositoryFactory repoFactory)
        {
            return new GroupModerationLogService(
                new GroupModerationLogRepository(repoFactory.ContextFactory),
                repoFactory.CreateGroupAccessValidator(),
                MockRepositories.CreateMockAuditLogRepository().Object,
                Options.Create(new GroupModerationLogSettings()),
                CreateMockLogger<GroupModerationLogService>());
        }

        /// <summary>
        /// Seeds an admin, a member and a group and returns the group id.
        /// </summary>
        private static async Task<int> SeedGroupAsync(IDbContextFactory<ApplicationDbContext> factory)
        {
            using var context = await factory.CreateDbContextAsync();
            context.Users.AddRange(TestDataFactory.CreateTestUser(id: AdminId), TestDataFactory.CreateTestUser(id: MemberId));
            var group = TestDataFactory.CreateTestGroup(AdminId);
            context.Groups.Add(group);
            await context.SaveChangesAsync();
            context.GroupUsers.AddRange(
                TestDataFactory.CreateTestGroupUser(group.Id, AdminId, isAdmin: true),
                TestDataFactory.CreateTestGroupUser(group.Id, MemberId));
            await context.SaveChangesAsync();
            return group.Id;
        }

        /// <summary>
        /// Reads all moderation log entries of a group.
        /// </summary>
        private static async Task<List<GroupModerationLogEntry>> GetEntriesAsync(IDbContextFactory<ApplicationDbContext> factory, int groupId)
        {
            using var context = await factory.CreateDbContextAsync();
            return await context.GroupModerationLogEntries.Where(e => e.GroupId == groupId).ToListAsync();
        }

        [Fact]
        public async Task BanUser_WritesLogEntry_WithActorTargetAndUtcTimestamp()
        {
            var repoFactory = CreateTestRepositoryFactory("ModLogHook_Ban");
            var groupId = await SeedGroupAsync(repoFactory.ContextFactory);
            var service = new GroupBannedMemberService(repoFactory.ContextFactory, CreateMockLogger<GroupBannedMemberService>(),
                CreateMockAuditLogRepository(), CreateMockRoleService(), CreateLogService(repoFactory));
            var before = DateTime.UtcNow.AddSeconds(-5);

            var (success, _) = await service.BanUserAsync(groupId, MemberId, AdminId);

            success.Should().BeTrue();
            var entry = (await GetEntriesAsync(repoFactory.ContextFactory, groupId)).Should().ContainSingle().Subject;
            entry.Action.Should().Be(GroupModerationAction.MemberBanned);
            entry.ActorUserId.Should().Be(AdminId);
            entry.TargetUserId.Should().Be(MemberId);
            entry.CreatedAt.Should().BeAfter(before).And.BeOnOrBefore(DateTime.UtcNow.AddSeconds(5));
        }

        [Fact]
        public async Task UnbanUser_WritesLogEntryAndAuditEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("ModLogHook_Unban");
            var groupId = await SeedGroupAsync(repoFactory.ContextFactory);
            var audit = MockRepositories.CreateMockAuditLogRepository();
            var service = new GroupBannedMemberService(repoFactory.ContextFactory, CreateMockLogger<GroupBannedMemberService>(),
                audit.Object, CreateMockRoleService(), CreateLogService(repoFactory));
            await service.BanUserAsync(groupId, MemberId, AdminId);

            var (success, _) = await service.UnbanUserAsync(groupId, MemberId, AdminId);

            success.Should().BeTrue();
            var entries = await GetEntriesAsync(repoFactory.ContextFactory, groupId);
            entries.Should().Contain(e => e.Action == GroupModerationAction.MemberUnbanned && e.TargetUserId == MemberId
                                          && e.CreatedAt != default);
            audit.Verify(a => a.LogActionAsync(AdminId, ActionCategory.GroupAdminUnbanUser, It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task BanUser_WhenNotAuthorized_WritesNoLogEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("ModLogHook_BanDenied");
            var groupId = await SeedGroupAsync(repoFactory.ContextFactory);
            var service = new GroupBannedMemberService(repoFactory.ContextFactory, CreateMockLogger<GroupBannedMemberService>(),
                CreateMockAuditLogRepository(), CreateMockRoleService(), CreateLogService(repoFactory));

            var (success, _) = await service.BanUserAsync(groupId, AdminId, MemberId);

            success.Should().BeFalse();
            (await GetEntriesAsync(repoFactory.ContextFactory, groupId)).Should().BeEmpty();
        }

        [Theory]
        [InlineData(ReportStatus.Dismissed, GroupModerationAction.ReportDismissed)]
        [InlineData(ReportStatus.Resolved, GroupModerationAction.ReportResolved)]
        [InlineData(ReportStatus.ActionTaken, GroupModerationAction.ReportResolved)]
        public async Task ReportDecision_WritesLogEntry(ReportStatus status, GroupModerationAction expected)
        {
            var repoFactory = CreateTestRepositoryFactory("ModLogHook_Report_" + status);
            var groupId = await SeedGroupAsync(repoFactory.ContextFactory);
            int reportId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var post = TestDataFactory.CreateTestGroupPost(groupId, MemberId, "Reported content");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                var report = new GroupContentReport { GroupId = groupId, PostId = post.Id, ReporterId = AdminId, Reason = ReportReason.Spam };
                context.GroupContentReports.Add(report);
                await context.SaveChangesAsync();
                reportId = report.Id;
            }
            var service = new GroupContentReportService(repoFactory.ContextFactory, CreateMockLogger<GroupContentReportService>(),
                repoFactory.CreateGroupAccessValidator(), CreateLogService(repoFactory));

            var (success, _) = await service.UpdateReportStatusAsync(reportId, AdminId, status, "Reviewed");

            success.Should().BeTrue();
            var entry = (await GetEntriesAsync(repoFactory.ContextFactory, groupId)).Should().ContainSingle().Subject;
            entry.Action.Should().Be(expected);
            entry.ActorUserId.Should().Be(AdminId);
            entry.TargetUserId.Should().Be(MemberId);
            entry.TargetId.Should().Be(reportId.ToString());
            entry.Reason.Should().Be("Reviewed");
            entry.CreatedAt.Should().NotBe(default);
        }

        [Fact]
        public async Task ReportDecision_ByNonModerator_WritesNoLogEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("ModLogHook_ReportDenied");
            var groupId = await SeedGroupAsync(repoFactory.ContextFactory);
            int reportId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var report = new GroupContentReport { GroupId = groupId, ReporterId = AdminId, Reason = ReportReason.Spam };
                context.GroupContentReports.Add(report);
                await context.SaveChangesAsync();
                reportId = report.Id;
            }
            var service = new GroupContentReportService(repoFactory.ContextFactory, CreateMockLogger<GroupContentReportService>(),
                repoFactory.CreateGroupAccessValidator(), CreateLogService(repoFactory));

            var (success, _) = await service.DismissReportAsync(reportId, MemberId);

            success.Should().BeFalse();
            (await GetEntriesAsync(repoFactory.ContextFactory, groupId)).Should().BeEmpty();
        }

        /// <summary>
        /// Builds a file service over the in-memory database with the given moderation log.
        /// </summary>
        private GroupFileService CreateFileService(TestRepositoryFactory repoFactory, IGroupModerationLogService log)
        {
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(m => m.ContentRootPath).Returns(Path.GetTempPath());
            var auditRepo = MockRepositories.CreateMockAuditLogRepository();
            var notificationService = new NotificationService(repoFactory.CreateNotificationRepository(), repoFactory.ContextFactory,
                new Mock<ILogger<NotificationService>>().Object, new Mock<IServiceScopeFactory>().Object, auditRepo.Object);
            var memberService = new GroupMemberService(repoFactory.ContextFactory, repoFactory.CreateGroupMemberRepository(),
                repoFactory.CreateGroupRepository(), new Mock<ILogger<GroupMemberService>>().Object, auditRepo.Object,
                notificationService, CreateMockRoleService());

            return new GroupFileService(
                repoFactory.CreateGroupFileRepository(),
                repoFactory.CreateGroupRepository(),
                repoFactory.ContextFactory,
                CreateMockLogger<GroupFileService>(),
                env.Object,
                new Mock<IFileSignatureValidator>().Object,
                new Mock<IVirusScanService>().Object,
                new UserPreferenceService(repoFactory.ContextFactory, new Mock<ILogger<UserPreferenceService>>().Object),
                memberService,
                log);
        }

        /// <summary>
        /// Seeds a pending file uploaded by the member and returns its id.
        /// </summary>
        private static async Task<int> SeedPendingFileAsync(IDbContextFactory<ApplicationDbContext> factory, int groupId)
        {
            using var context = await factory.CreateDbContextAsync();
            var file = new GroupFile
            {
                GroupId = groupId,
                UploaderId = MemberId,
                OriginalFileName = "rules.pdf",
                StoredFileName = Guid.NewGuid() + ".pdf",
                ContentType = "application/pdf",
                Status = GroupFileStatus.Pending
            };
            context.GroupFiles.Add(file);
            await context.SaveChangesAsync();
            return file.Id;
        }

        [Fact]
        public async Task ApproveFile_WritesLogEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("ModLogHook_FileApprove");
            var groupId = await SeedGroupAsync(repoFactory.ContextFactory);
            var fileId = await SeedPendingFileAsync(repoFactory.ContextFactory, groupId);
            var service = CreateFileService(repoFactory, CreateLogService(repoFactory));

            var (success, _) = await service.ApproveFileAsync(fileId, AdminId);

            success.Should().BeTrue();
            var entry = (await GetEntriesAsync(repoFactory.ContextFactory, groupId)).Should().ContainSingle().Subject;
            entry.Action.Should().Be(GroupModerationAction.FileApproved);
            entry.TargetType.Should().Be(GroupModerationTargetType.File);
            entry.TargetUserId.Should().Be(MemberId);
            entry.Summary.Should().Be("rules.pdf");
            entry.CreatedAt.Should().NotBe(default);
        }

        [Fact]
        public async Task DeclineFile_WritesLogEntryWithReason()
        {
            var repoFactory = CreateTestRepositoryFactory("ModLogHook_FileDecline");
            var groupId = await SeedGroupAsync(repoFactory.ContextFactory);
            var fileId = await SeedPendingFileAsync(repoFactory.ContextFactory, groupId);
            var service = CreateFileService(repoFactory, CreateLogService(repoFactory));

            var (success, _) = await service.DeclineFileAsync(fileId, AdminId, "Not allowed");

            success.Should().BeTrue();
            var entry = (await GetEntriesAsync(repoFactory.ContextFactory, groupId)).Should().ContainSingle().Subject;
            entry.Action.Should().Be(GroupModerationAction.FileDeclined);
            entry.Reason.Should().Be("Not allowed");
        }

        [Fact]
        public async Task ApproveFile_ByMember_WritesNoLogEntry()
        {
            var repoFactory = CreateTestRepositoryFactory("ModLogHook_FileDenied");
            var groupId = await SeedGroupAsync(repoFactory.ContextFactory);
            var fileId = await SeedPendingFileAsync(repoFactory.ContextFactory, groupId);
            var service = CreateFileService(repoFactory, CreateLogService(repoFactory));

            var (success, _) = await service.ApproveFileAsync(fileId, MemberId);

            success.Should().BeFalse();
            (await GetEntriesAsync(repoFactory.ContextFactory, groupId)).Should().BeEmpty();
        }
    }
}
