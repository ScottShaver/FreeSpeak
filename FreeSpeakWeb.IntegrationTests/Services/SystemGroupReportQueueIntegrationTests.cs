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
    /// Integration tests for the cross-group report queue, atomic transitions and the group Mod Log system flag against PostgreSQL.
    /// </summary>
    public class SystemGroupReportQueueIntegrationTests : IntegrationTestBase
    {
        private GroupReportModerationRepository CreateRepository() => new(CreateDbContextFactory());

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

        /// <summary>Seeds one group with a post and one report per given age and status, returning identifiers.</summary>
        private async Task<(int GroupId, int PostId)> SeedGroupReportsAsync(params (ReportStatus Status, int AgeDays)[] reports)
        {
            var author = "author-" + Guid.NewGuid().ToString("N");
            await using var context = CreateDbContext();
            context.Users.Add(NewUser(author));
            await context.SaveChangesAsync();

            var group = new Group { Name = "g-" + Guid.NewGuid().ToString("N"), Description = "d", CreatorId = author, IsPublic = true };
            context.Groups.Add(group);
            await context.SaveChangesAsync();
            var post = new GroupPost { GroupId = group.Id, AuthorId = author, Content = "post", Status = PostStatus.Posted };
            context.GroupPosts.Add(post);
            await context.SaveChangesAsync();

            foreach (var (status, age) in reports)
            {
                var reporter = "reporter-" + Guid.NewGuid().ToString("N");
                context.Users.Add(NewUser(reporter));
                await context.SaveChangesAsync();
                context.GroupContentReports.Add(new GroupContentReport
                {
                    GroupId = group.Id,
                    PostId = post.Id,
                    ReporterId = reporter,
                    Reason = ReportReason.Spam,
                    Status = status,
                    CreatedAt = DateTime.UtcNow.AddDays(-age)
                });
            }

            await context.SaveChangesAsync();
            return (group.Id, post.Id);
        }

        /// <summary>The queue spans groups, lists only pending reports and sorts oldest first.</summary>
        [Fact]
        public async Task GetQueueAsync_ListsPendingOldestFirst()
        {
            var (groupId, _) = await SeedGroupReportsAsync((ReportStatus.NotReviewed, 1), (ReportStatus.UnderReview, 5), (ReportStatus.Dismissed, 8));
            var repo = CreateRepository();

            var (rows, _) = await repo.GetQueueAsync(new GroupReportQueueFilter(GroupId: groupId), DateTime.UtcNow.AddDays(-3), 1, 100);

            rows.Should().HaveCount(2);
            rows.Select(r => r.CreatedAt).Should().BeInAscendingOrder();
        }

        /// <summary>The stale filter returns only reports older than the cutoff.</summary>
        [Fact]
        public async Task GetQueueAsync_StaleOnly_ReturnsOnlyOldReports()
        {
            var (groupId, _) = await SeedGroupReportsAsync((ReportStatus.NotReviewed, 1), (ReportStatus.NotReviewed, 6));
            var repo = CreateRepository();

            var (rows, _) = await repo.GetQueueAsync(new GroupReportQueueFilter(GroupId: groupId, StaleOnly: true), DateTime.UtcNow.AddDays(-3), 1, 100);

            rows.Should().HaveCount(1);
        }

        /// <summary>A transition only succeeds from an allowed status, so a second attempt returns null.</summary>
        [Fact]
        public async Task TryTransitionAsync_SecondAttempt_ReturnsNull()
        {
            var (_, postId) = await SeedGroupReportsAsync((ReportStatus.NotReviewed, 1));
            var repo = CreateRepository();
            int reportId;
            await using (var context = CreateDbContext())
            {
                reportId = (await context.GroupContentReports.FirstAsync(r => r.PostId == postId)).Id;
            }

            var pending = new[] { ReportStatus.NotReviewed, ReportStatus.UnderReview };
            var first = await repo.TryTransitionAsync(reportId, pending, ReportStatus.Dismissed, null, DateTime.UtcNow, "n");
            var second = await repo.TryTransitionAsync(reportId, pending, ReportStatus.Resolved, null, DateTime.UtcNow, "n");

            first.Should().NotBeNull();
            second.Should().BeNull();
        }

        /// <summary>The IsSystemAction flag on the group Mod Log persists and defaults to false.</summary>
        [Fact]
        public async Task ModerationLog_IsSystemAction_PersistsAndDefaultsFalse()
        {
            var (groupId, _) = await SeedGroupReportsAsync();
            await using var context = CreateDbContext();
            var actor = (await context.Groups.FirstAsync(g => g.Id == groupId)).CreatorId;
            context.GroupModerationLogEntries.AddRange(
                new GroupModerationLogEntry { GroupId = groupId, ActorUserId = actor, ActorDisplayName = "a", Action = GroupModerationAction.PostRemoved, CreatedAt = DateTime.UtcNow },
                new GroupModerationLogEntry { GroupId = groupId, ActorUserId = actor, ActorDisplayName = "a", Action = GroupModerationAction.PostRemoved, CreatedAt = DateTime.UtcNow, IsSystemAction = true });
            await context.SaveChangesAsync();

            var rows = await context.GroupModerationLogEntries.Where(l => l.GroupId == groupId).OrderBy(l => l.Id).ToListAsync();

            rows.Select(r => r.IsSystemAction).Should().Equal(false, true);
        }
    }
}
