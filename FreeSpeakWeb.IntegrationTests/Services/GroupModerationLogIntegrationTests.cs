using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.IntegrationTests.Infrastructure;
using FreeSpeakWeb.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreeSpeakWeb.IntegrationTests.Services
{
    /// <summary>
    /// Integration tests for the group moderation log table and repository against PostgreSQL.
    /// </summary>
    public class GroupModerationLogIntegrationTests : IntegrationTestBase
    {
        private GroupModerationLogRepository CreateRepository() => new(CreateDbContextFactory());

        private async Task<int> SeedGroupAsync(string userId)
        {
            await using var context = CreateDbContext();
            context.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                NormalizedUserName = userId.ToUpper(),
                Email = $"{userId}@example.com",
                NormalizedEmail = $"{userId}@example.com".ToUpper(),
                EmailConfirmed = true,
                FirstName = "Mod",
                LastName = "Erator",
                SecurityStamp = Guid.NewGuid().ToString()
            });
            var group = new Group
            {
                CreatorId = userId,
                Name = "Log group " + Guid.NewGuid(),
                Description = "d",
                IsPublic = true,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };
            context.Groups.Add(group);
            await context.SaveChangesAsync();
            return group.Id;
        }

        private static GroupModerationLogEntry NewEntry(int groupId, string actorId, GroupModerationAction action, DateTime createdAt) => new()
        {
            GroupId = groupId,
            ActorUserId = actorId,
            ActorDisplayName = "Mod Erator",
            Action = action,
            TargetType = GroupModerationTargetType.Post,
            TargetId = "1",
            Summary = "summary",
            CreatedAt = createdAt
        };

        /// <summary>Verifies the migration creates the table and entries page newest first with correct totals.</summary>
        [Fact]
        public async Task AddAndPage_ReturnsNewestFirstWithTotals()
        {
            var actor = "modlog-actor-1";
            var groupId = await SeedGroupAsync(actor);
            var repo = CreateRepository();
            var now = DateTime.UtcNow;
            for (var i = 0; i < 5; i++)
            {
                await repo.AddAsync(NewEntry(groupId, actor, GroupModerationAction.PostApproved, now.AddMinutes(-i)));
            }

            var (page1, total) = await repo.GetPagedAsync(groupId, new GroupModerationLogFilter(), 1, 2);
            var (page3, _) = await repo.GetPagedAsync(groupId, new GroupModerationLogFilter(), 3, 2);

            total.Should().Be(5);
            page1.Should().HaveCount(2);
            page1[0].CreatedAt.Should().BeAfter(page1[1].CreatedAt);
            page3.Should().HaveCount(1);
        }

        /// <summary>Verifies action and date filters, and isolation between groups.</summary>
        [Fact]
        public async Task Filters_ApplyActionDateAndGroup()
        {
            var actor = "modlog-actor-2";
            var groupId = await SeedGroupAsync(actor);
            var otherGroupId = await SeedGroupAsync("modlog-actor-3");
            var repo = CreateRepository();
            var now = DateTime.UtcNow;
            await repo.AddAsync(NewEntry(groupId, actor, GroupModerationAction.PostApproved, now.AddDays(-10)));
            await repo.AddAsync(NewEntry(groupId, actor, GroupModerationAction.MemberBanned, now.AddDays(-1)));
            await repo.AddAsync(NewEntry(otherGroupId, "modlog-actor-3", GroupModerationAction.MemberBanned, now));

            var (byAction, _) = await repo.GetPagedAsync(groupId, new GroupModerationLogFilter { Action = GroupModerationAction.MemberBanned }, 1, 10);
            var (byDate, _) = await repo.GetPagedAsync(groupId, new GroupModerationLogFilter { FromDate = now.AddDays(-2) }, 1, 10);
            var (all, total) = await repo.GetPagedAsync(groupId, new GroupModerationLogFilter(), 1, 10);

            byAction.Should().ContainSingle().Which.Action.Should().Be(GroupModerationAction.MemberBanned);
            byDate.Should().ContainSingle();
            total.Should().Be(2);
            all.Should().OnlyContain(e => e.GroupId == groupId);
        }

        /// <summary>Verifies the purge deletes only entries older than the cutoff.</summary>
        [Fact]
        public async Task DeleteOlderThan_RemovesOnlyOldEntries()
        {
            var actor = "modlog-actor-4";
            var groupId = await SeedGroupAsync(actor);
            var repo = CreateRepository();
            var now = DateTime.UtcNow;
            await repo.AddAsync(NewEntry(groupId, actor, GroupModerationAction.PostApproved, now.AddMonths(-7)));
            await repo.AddAsync(NewEntry(groupId, actor, GroupModerationAction.PostDeclined, now.AddMonths(-7).AddDays(-1)));
            await repo.AddAsync(NewEntry(groupId, actor, GroupModerationAction.PostApproved, now.AddMonths(-1)));

            var deleted = await repo.DeleteOlderThanAsync(now.AddMonths(-6), 1);

            deleted.Should().Be(2);
            await using var context = CreateDbContext();
            (await context.Set<GroupModerationLogEntry>().CountAsync(e => e.GroupId == groupId)).Should().Be(1);
        }
    }
}
