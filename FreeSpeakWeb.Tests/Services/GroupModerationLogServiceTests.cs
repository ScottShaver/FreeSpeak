using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Unit tests for the group moderation log service, CSV builder and cleanup service.
    /// </summary>
    public class GroupModerationLogServiceTests : TestBase
    {
        private const string AdminId = "admin1";
        private const string ModId = "mod1";
        private const string MemberId = "member1";
        private const string OutsiderId = "outsider1";

        private Mock<IAuditLogRepository> _audit = null!;

        /// <summary>
        /// Creates the service under test plus a seeded group with an admin, moderator and member.
        /// </summary>
        private async Task<(GroupModerationLogService Service, IDbContextFactory<ApplicationDbContext> Factory, int GroupId)> SetupAsync(
            string dbName, GroupModerationLogSettings? settings = null)
        {
            var repoFactory = CreateTestRepositoryFactory(dbName);
            var factory = repoFactory.ContextFactory;
            using (var context = await factory.CreateDbContextAsync())
            {
                context.Users.AddRange(
                    TestDataFactory.CreateTestUser(id: AdminId),
                    TestDataFactory.CreateTestUser(id: ModId),
                    TestDataFactory.CreateTestUser(id: MemberId),
                    TestDataFactory.CreateTestUser(id: OutsiderId));
                var group = TestDataFactory.CreateTestGroup(AdminId);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                context.GroupUsers.AddRange(
                    TestDataFactory.CreateTestGroupUser(group.Id, AdminId, isAdmin: true),
                    TestDataFactory.CreateTestGroupUser(group.Id, ModId, isModerator: true),
                    TestDataFactory.CreateTestGroupUser(group.Id, MemberId));
                await context.SaveChangesAsync();
            }

            _audit = MockRepositories.CreateMockAuditLogRepository();
            var service = new GroupModerationLogService(
                new GroupModerationLogRepository(factory),
                repoFactory.CreateGroupAccessValidator(),
                _audit.Object,
                Options.Create(settings ?? new GroupModerationLogSettings()),
                CreateMockLogger<GroupModerationLogService>());

            int groupId;
            using (var context = await factory.CreateDbContextAsync())
            {
                groupId = await context.Groups.Select(g => g.Id).FirstAsync();
            }

            return (service, factory, groupId);
        }

        private static GroupModerationLogRequest Request(int groupId, GroupModerationAction action = GroupModerationAction.PostApproved,
            string? summary = "Some text", string? reason = null) => new()
            {
                GroupId = groupId,
                ActorUserId = ModId,
                Action = action,
                TargetType = GroupModerationTargetType.Post,
                TargetId = "42",
                TargetUserId = MemberId,
                Summary = summary,
                Reason = reason
            };

        [Fact]
        public async Task LogAsync_PersistsEntryWithServerUtcTimestampAndDisplayNames()
        {
            var (service, factory, groupId) = await SetupAsync("ModLogSvc_Persist");
            var before = DateTime.UtcNow.AddSeconds(-5);

            await service.LogAsync(Request(groupId));

            using var context = await factory.CreateDbContextAsync();
            var entry = await context.GroupModerationLogEntries.SingleAsync();
            entry.GroupId.Should().Be(groupId);
            entry.ActorUserId.Should().Be(ModId);
            entry.ActorDisplayName.Should().NotBeNullOrWhiteSpace();
            entry.TargetUserId.Should().Be(MemberId);
            entry.CreatedAt.Should().BeAfter(before).And.BeOnOrBefore(DateTime.UtcNow.AddSeconds(5));
        }

        [Fact]
        public async Task LogAsync_StripsHtmlFromSummary_AndTruncates()
        {
            var (service, factory, groupId) = await SetupAsync("ModLogSvc_Sanitize");

            await service.LogAsync(Request(groupId, summary: "<script>alert(1)</script><b>Hello</b>" + new string('x', 2000)));

            using var context = await factory.CreateDbContextAsync();
            var entry = await context.GroupModerationLogEntries.SingleAsync();
            entry.Summary.Should().NotContain("<").And.Contain("Hello");
            entry.Summary!.Length.Should().BeLessThanOrEqualTo(GroupModerationLogEntry.MaxSummaryLength);
        }

        [Fact]
        public async Task LogAsync_WhenRepositoryFails_DoesNotThrow()
        {
            var repository = new Mock<IGroupModerationLogRepository>();
            repository.Setup(r => r.AddAsync(It.IsAny<GroupModerationLogEntry>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("db down"));
            repository.Setup(r => r.GetDisplayNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, string>());
            var repoFactory = CreateTestRepositoryFactory("ModLogSvc_Fail");
            var service = new GroupModerationLogService(repository.Object, repoFactory.CreateGroupAccessValidator(),
                MockRepositories.CreateMockAuditLogRepository().Object, Options.Create(new GroupModerationLogSettings()),
                CreateMockLogger<GroupModerationLogService>());

            var act = () => service.LogAsync(Request(1));

            await act.Should().NotThrowAsync();
        }

        [Theory]
        [InlineData(MemberId)]
        [InlineData(OutsiderId)]
        public async Task GetLogAsync_ForMemberOrNonMember_IsUnauthorized(string userId)
        {
            var (service, _, groupId) = await SetupAsync("ModLogSvc_Deny_" + userId);
            await service.LogAsync(Request(groupId));

            var page = await service.GetLogAsync(groupId, userId, new GroupModerationLogFilter(), 1, 25);

            page.IsAuthorized.Should().BeFalse();
            page.Items.Should().BeEmpty();
            page.TotalCount.Should().Be(0);
        }

        [Theory]
        [InlineData(AdminId)]
        [InlineData(ModId)]
        public async Task GetLogAsync_ForAdminOrModerator_ReturnsEntriesNewestFirst(string userId)
        {
            var (service, factory, groupId) = await SetupAsync("ModLogSvc_Allow_" + userId);
            await service.LogAsync(Request(groupId, GroupModerationAction.PostApproved));
            await Task.Delay(20);
            await service.LogAsync(Request(groupId, GroupModerationAction.MemberBanned));

            var page = await service.GetLogAsync(groupId, userId, new GroupModerationLogFilter(), 1, 25);

            page.IsAuthorized.Should().BeTrue();
            page.TotalCount.Should().Be(2);
            page.Items.Select(i => i.Action).Should().ContainInOrder(GroupModerationAction.MemberBanned, GroupModerationAction.PostApproved);
        }

        [Fact]
        public async Task GetLogAsync_FiltersByActionAndDate_AndPages()
        {
            var (service, factory, groupId) = await SetupAsync("ModLogSvc_Filter");
            using (var context = await factory.CreateDbContextAsync())
            {
                for (var i = 0; i < 5; i++)
                {
                    context.GroupModerationLogEntries.Add(new GroupModerationLogEntry
                    {
                        GroupId = groupId, ActorUserId = ModId, ActorDisplayName = "Mod", Action = GroupModerationAction.PostApproved,
                        TargetType = GroupModerationTargetType.Post, CreatedAt = new DateTime(2026, 1, 1 + i, 0, 0, 0, DateTimeKind.Utc)
                    });
                }
                context.GroupModerationLogEntries.Add(new GroupModerationLogEntry
                {
                    GroupId = groupId, ActorUserId = ModId, ActorDisplayName = "Mod", Action = GroupModerationAction.MemberBanned,
                    TargetType = GroupModerationTargetType.Member, CreatedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)
                });
                await context.SaveChangesAsync();
            }

            var byAction = await service.GetLogAsync(groupId, AdminId, new GroupModerationLogFilter { Action = GroupModerationAction.MemberBanned }, 1, 25);
            var byDate = await service.GetLogAsync(groupId, AdminId, new GroupModerationLogFilter
            {
                FromDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                ToDate = new DateTime(2026, 1, 3, 23, 59, 59, DateTimeKind.Utc)
            }, 1, 25);
            var page2 = await service.GetLogAsync(groupId, AdminId, new GroupModerationLogFilter(), 2, 4);

            byAction.TotalCount.Should().Be(1);
            byDate.TotalCount.Should().Be(2);
            page2.TotalCount.Should().Be(6);
            page2.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetLogAsync_OnlyReturnsEntriesOfRequestedGroup()
        {
            var (service, factory, groupId) = await SetupAsync("ModLogSvc_Isolation");
            await service.LogAsync(Request(groupId));
            await service.LogAsync(Request(groupId + 100));

            var page = await service.GetLogAsync(groupId, AdminId, new GroupModerationLogFilter(), 1, 25);

            page.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task ExportCsvAsync_ForModerator_ReturnsCsvAndWritesAuditEntry()
        {
            var (service, _, groupId) = await SetupAsync("ModLogSvc_Export");
            await service.LogAsync(Request(groupId, summary: "Hello, \"world\""));

            var result = await service.ExportCsvAsync(groupId, ModId, new GroupModerationLogFilter());

            result.IsAuthorized.Should().BeTrue();
            result.RowCount.Should().Be(1);
            result.Truncated.Should().BeFalse();
            result.Csv.Should().StartWith("\"Timestamp (UTC)\"".Replace("\"", string.Empty)).And.Contain("\"Hello, \"\"world\"\"\"");
            _audit.Verify(a => a.LogActionAsync(ModId, ActionCategory.GroupModerationLogExport, It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task ExportCsvAsync_ForMember_IsUnauthorizedAndNotAudited()
        {
            var (service, _, groupId) = await SetupAsync("ModLogSvc_ExportDeny");
            await service.LogAsync(Request(groupId));

            var result = await service.ExportCsvAsync(groupId, MemberId, new GroupModerationLogFilter());

            result.IsAuthorized.Should().BeFalse();
            result.Csv.Should().BeEmpty();
            _audit.Verify(a => a.LogActionAsync(It.IsAny<string>(), ActionCategory.GroupModerationLogExport, It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task ExportCsvAsync_AboveRowCap_IsTruncated()
        {
            var (service, _, groupId) = await SetupAsync("ModLogSvc_ExportCap", new GroupModerationLogSettings { ExportMaxRows = 2 });
            for (var i = 0; i < 3; i++)
            {
                await service.LogAsync(Request(groupId));
            }

            var result = await service.ExportCsvAsync(groupId, AdminId, new GroupModerationLogFilter());

            result.RowCount.Should().Be(2);
            result.Truncated.Should().BeTrue();
        }

        [Theory]
        [InlineData("=SUM(A1)", "'=SUM(A1)")]
        [InlineData("+1", "'+1")]
        [InlineData("-1", "'-1")]
        [InlineData("@cmd", "'@cmd")]
        [InlineData("plain", "plain")]
        [InlineData("a,b", "\"a,b\"")]
        [InlineData("line\nbreak", "\"line\nbreak\"")]
        [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
        [InlineData(null, "")]
        public void Csv_Escape_NeutralisesFormulasAndQuotes(string? input, string expected)
        {
            GroupModerationLogCsv.Escape(input).Should().Be(expected);
        }

        [Fact]
        public void Csv_Build_WritesHeaderAndIsoUtcTimestamp()
        {
            var csv = GroupModerationLogCsv.Build(new[]
            {
                new GroupModerationLogEntry
                {
                    GroupId = 1, ActorUserId = "a", ActorDisplayName = "Alice", Action = GroupModerationAction.PostApproved,
                    TargetType = GroupModerationTargetType.Post, CreatedAt = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc)
                }
            });

            var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            lines.Should().HaveCount(2);
            lines[0].Split(',').Should().HaveCount(GroupModerationLogCsv.Columns.Length);
            lines[1].Should().StartWith("2026-03-04T05:06:07").And.Contain("Alice");
        }

        [Fact]
        public async Task Cleanup_DeletesEntriesOlderThanRetention_AndKeepsRecent()
        {
            var repoFactory = CreateTestRepositoryFactory("ModLogSvc_Cleanup");
            var factory = repoFactory.ContextFactory;
            var now = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            using (var context = await factory.CreateDbContextAsync())
            {
                var group = TestDataFactory.CreateTestGroup("creator");
                context.Users.Add(TestDataFactory.CreateTestUser(id: "creator"));
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                foreach (var age in new[] { 7, 8, 1 })
                {
                    context.GroupModerationLogEntries.Add(new GroupModerationLogEntry
                    {
                        GroupId = group.Id, ActorUserId = "creator", ActorDisplayName = "C", Action = GroupModerationAction.PostApproved,
                        TargetType = GroupModerationTargetType.Post, CreatedAt = now.AddMonths(-age)
                    });
                }
                await context.SaveChangesAsync();
            }

            var services = new ServiceCollection()
                .AddSingleton<IGroupModerationLogRepository>(new GroupModerationLogRepository(factory))
                .BuildServiceProvider();
            var cleanup = new GroupModerationLogCleanupService(services,
                Options.Create(new GroupModerationLogSettings { CleanupBatchSize = 1 }),
                CreateMockLogger<GroupModerationLogCleanupService>());

            var deleted = await cleanup.RunCleanupAsync(now);

            deleted.Should().Be(2);
            using var verify = await factory.CreateDbContextAsync();
            (await verify.GroupModerationLogEntries.CountAsync()).Should().Be(1);
        }
    }
}
