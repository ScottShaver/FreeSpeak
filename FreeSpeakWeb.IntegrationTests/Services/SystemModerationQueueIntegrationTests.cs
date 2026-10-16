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
    /// Integration tests for the system moderator queue queries, atomic transitions and cascade behavior against PostgreSQL.
    /// </summary>
    public class SystemModerationQueueIntegrationTests : IntegrationTestBase
    {
        private FeedContentReportRepository CreateRepository() => new(CreateDbContextFactory());

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

        private async Task<(string Author, string Reporter, int PostId)> SeedReportsAsync(params (ReportStatus Status, int AgeDays)[] reports)
        {
            var author = "author-" + Guid.NewGuid().ToString("N");
            var reporter = "reporter-" + Guid.NewGuid().ToString("N");
            await using var context = CreateDbContext();
            context.Users.AddRange(NewUser(author), NewUser(reporter));
            var post = new Post { AuthorId = author, Content = "post" };
            context.Posts.Add(post);
            await context.SaveChangesAsync();

            foreach (var (status, age) in reports)
            {
                var other = "reporter-" + Guid.NewGuid().ToString("N");
                context.Users.Add(NewUser(other));
                await context.SaveChangesAsync();
                context.Set<FeedContentReport>().Add(new FeedContentReport
                {
                    PostId = post.Id,
                    ContentAuthorId = author,
                    ReporterId = other,
                    Reason = ReportReason.Spam,
                    Status = status,
                    CreatedAt = DateTime.UtcNow.AddDays(-age),
                    ReviewedAt = status is ReportStatus.Dismissed or ReportStatus.Resolved ? DateTime.UtcNow.AddDays(-1) : null
                });
            }

            await context.SaveChangesAsync();
            return (author, reporter, post.Id);
        }

        /// <summary>The queue lists only pending reports for the post, oldest first by default and newest first on request.</summary>
        [Fact]
        public async Task GetQueueAsync_ListsPendingOldestFirst()
        {
            var (_, _, postId) = await SeedReportsAsync((ReportStatus.NotReviewed, 1), (ReportStatus.UnderReview, 5), (ReportStatus.Dismissed, 8));
            var repo = CreateRepository();

            var (oldest, total) = await repo.GetQueueAsync(new FeedReportQueueFilter(), DateTime.UtcNow.AddDays(-3), 1, 100);
            var (newest, _) = await repo.GetQueueAsync(new FeedReportQueueFilter(NewestFirst: true), DateTime.UtcNow.AddDays(-3), 1, 100);

            var mine = oldest.Where(r => r.PostId == postId).ToList();
            mine.Should().HaveCount(2);
            mine.Select(r => r.CreatedAt).Should().BeInAscendingOrder();
            newest.Where(r => r.PostId == postId).Select(r => r.CreatedAt).Should().BeInDescendingOrder();
            total.Should().BeGreaterThanOrEqualTo(2);
        }

        /// <summary>The stale filter returns only reports older than the cutoff.</summary>
        [Fact]
        public async Task GetQueueAsync_StaleOnly_ReturnsOnlyOldReports()
        {
            var (_, _, postId) = await SeedReportsAsync((ReportStatus.NotReviewed, 1), (ReportStatus.NotReviewed, 6));
            var repo = CreateRepository();

            var (rows, _) = await repo.GetQueueAsync(new FeedReportQueueFilter(StaleOnly: true), DateTime.UtcNow.AddDays(-3), 1, 100);

            rows.Where(r => r.PostId == postId).Should().HaveCount(1);
        }

        /// <summary>A transition only succeeds from an allowed status, so a second attempt returns null.</summary>
        [Fact]
        public async Task TryTransitionAsync_SecondAttempt_ReturnsNull()
        {
            var (_, _, postId) = await SeedReportsAsync((ReportStatus.NotReviewed, 1));
            var repo = CreateRepository();
            int reportId;
            await using (var context = CreateDbContext())
            {
                reportId = (await context.Set<FeedContentReport>().FirstAsync(r => r.PostId == postId)).Id;
            }

            var pending = new[] { ReportStatus.NotReviewed, ReportStatus.UnderReview };
            var first = await repo.TryTransitionAsync(reportId, pending, ReportStatus.Dismissed, null, DateTime.UtcNow, "n");
            var second = await repo.TryTransitionAsync(reportId, pending, ReportStatus.Resolved, null, DateTime.UtcNow, "n");

            first.Should().NotBeNull();
            first!.Status.Should().Be(ReportStatus.Dismissed);
            second.Should().BeNull();
        }

        /// <summary>Deleting the reported post removes its reports.</summary>
        [Fact]
        public async Task DeletingPost_CascadesToReports()
        {
            var (_, _, postId) = await SeedReportsAsync((ReportStatus.NotReviewed, 1), (ReportStatus.NotReviewed, 2));

            await using (var context = CreateDbContext())
            {
                context.Posts.Remove(await context.Posts.FirstAsync(p => p.Id == postId));
                await context.SaveChangesAsync();
            }

            await using var verify = CreateDbContext();
            (await verify.Set<FeedContentReport>().CountAsync(r => r.PostId == postId)).Should().Be(0);
        }
    }
}
