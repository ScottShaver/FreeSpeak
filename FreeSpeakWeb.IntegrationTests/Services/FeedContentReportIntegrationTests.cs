using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.IntegrationTests.Infrastructure;
using FreeSpeakWeb.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreeSpeakWeb.IntegrationTests.Services
{
    /// <summary>
    /// Integration tests for feed content reports against PostgreSQL: constraints, indexes, queue queries and cascades.
    /// </summary>
    public class FeedContentReportIntegrationTests : IntegrationTestBase
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

        private async Task<(string Author, string Reporter, int PostId, int CommentId)> SeedAsync()
        {
            var author = "author-" + Guid.NewGuid().ToString("N");
            var reporter = "reporter-" + Guid.NewGuid().ToString("N");
            await using var context = CreateDbContext();
            context.Users.AddRange(NewUser(author), NewUser(reporter));
            var post = new Post { AuthorId = author, Content = "post" };
            context.Posts.Add(post);
            await context.SaveChangesAsync();
            var comment = new Comment { PostId = post.Id, AuthorId = author, Content = "comment" };
            context.Comments.Add(comment);
            await context.SaveChangesAsync();
            return (author, reporter, post.Id, comment.Id);
        }

        private static FeedContentReport NewReport(string author, string reporter, int? postId, int? commentId) => new()
        {
            PostId = postId,
            CommentId = commentId,
            ContentAuthorId = author,
            ReporterId = reporter,
            Reason = ReportReason.Spam,
            CreatedAt = DateTime.UtcNow
        };

        /// <summary>A second report of the same post by the same user is rejected by the unique index.</summary>
        [Fact]
        public async Task AddAsync_DuplicatePostReport_ReturnsFalse()
        {
            var (author, reporter, postId, _) = await SeedAsync();
            var repo = CreateRepository();

            (await repo.AddAsync(NewReport(author, reporter, postId, null))).Should().BeTrue();
            (await repo.AddAsync(NewReport(author, reporter, postId, null))).Should().BeFalse();
            (await repo.HasReportedAsync(reporter, postId, null)).Should().BeTrue();
        }

        /// <summary>A second report of the same comment by the same user is rejected by the unique index.</summary>
        [Fact]
        public async Task AddAsync_DuplicateCommentReport_ReturnsFalse()
        {
            var (author, reporter, _, commentId) = await SeedAsync();
            var repo = CreateRepository();

            (await repo.AddAsync(NewReport(author, reporter, null, commentId))).Should().BeTrue();
            (await repo.AddAsync(NewReport(author, reporter, null, commentId))).Should().BeFalse();
        }

        /// <summary>The check constraint rejects a report that targets both a post and a comment.</summary>
        [Fact]
        public async Task Insert_BothTargets_ViolatesCheckConstraint()
        {
            var (author, reporter, postId, commentId) = await SeedAsync();
            await using var context = CreateDbContext();
            context.Set<FeedContentReport>().Add(NewReport(author, reporter, postId, commentId));

            var act = () => context.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateException>();
        }

        /// <summary>The check constraint rejects a report that targets nothing.</summary>
        [Fact]
        public async Task Insert_NoTarget_ViolatesCheckConstraint()
        {
            var (author, reporter, _, _) = await SeedAsync();
            await using var context = CreateDbContext();
            context.Set<FeedContentReport>().Add(NewReport(author, reporter, null, null));

            var act = () => context.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateException>();
        }

        /// <summary>The rate-limit count only includes reports inside the window.</summary>
        [Fact]
        public async Task CountReportsSinceAsync_CountsOnlyRecentReports()
        {
            var (author, reporter, postId, commentId) = await SeedAsync();
            var repo = CreateRepository();
            var old = NewReport(author, reporter, postId, null);
            old.CreatedAt = DateTime.UtcNow.AddHours(-2);
            await repo.AddAsync(old);
            await repo.AddAsync(NewReport(author, reporter, null, commentId));

            var count = await repo.CountReportsSinceAsync(reporter, DateTime.UtcNow.AddHours(-1));

            count.Should().Be(1);
        }

        /// <summary>The pending queue lists only unreviewed reports, oldest first, and drops reviewed ones.</summary>
        [Fact]
        public async Task PendingQueue_ExcludesReviewedReports()
        {
            var (author, reporter, postId, commentId) = await SeedAsync();
            var repo = CreateRepository();
            var before = await repo.GetPendingCountAsync();
            await repo.AddAsync(NewReport(author, reporter, postId, null));
            await repo.AddAsync(NewReport(author, reporter, null, commentId));
            var (items, _) = await repo.GetPendingPagedAsync(1, 1000);
            var mine = items.First(r => r.PostId == postId);

            await repo.UpdateReviewAsync(mine.Id, ReportStatus.Dismissed, author, DateTime.UtcNow, "ok");

            (await repo.GetPendingCountAsync()).Should().Be(before + 1);
        }

        /// <summary>Deleting a post cascades to its reports and its comments' reports.</summary>
        [Fact]
        public async Task DeletingPost_CascadesToReports()
        {
            var (author, reporter, postId, commentId) = await SeedAsync();
            var repo = CreateRepository();
            await repo.AddAsync(NewReport(author, reporter, postId, null));
            await repo.AddAsync(NewReport(author, reporter, null, commentId));

            await using (var context = CreateDbContext())
            {
                var post = await context.Posts.SingleAsync(p => p.Id == postId);
                context.Posts.Remove(post);
                await context.SaveChangesAsync();
            }

            await using var verify = CreateDbContext();
            (await verify.Set<FeedContentReport>().CountAsync(r => r.ReporterId == reporter)).Should().Be(0);
        }

        /// <summary>Deleting a comment cascades to its reports only.</summary>
        [Fact]
        public async Task DeletingComment_CascadesToItsReports()
        {
            var (author, reporter, postId, commentId) = await SeedAsync();
            var repo = CreateRepository();
            await repo.AddAsync(NewReport(author, reporter, postId, null));
            await repo.AddAsync(NewReport(author, reporter, null, commentId));

            await using (var context = CreateDbContext())
            {
                var comment = await context.Comments.SingleAsync(c => c.Id == commentId);
                context.Comments.Remove(comment);
                await context.SaveChangesAsync();
            }

            await using var verify = CreateDbContext();
            var remaining = await verify.Set<FeedContentReport>().Where(r => r.ReporterId == reporter).ToListAsync();
            remaining.Should().ContainSingle().Which.PostId.Should().Be(postId);
        }
    }
}
