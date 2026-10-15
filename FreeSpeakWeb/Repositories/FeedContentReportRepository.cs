using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IFeedContentReportRepository"/>.
    /// </summary>
    public class FeedContentReportRepository : IFeedContentReportRepository
    {
        private static readonly ReportStatus[] PendingStatuses = { ReportStatus.NotReviewed, ReportStatus.UnderReview };

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedContentReportRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        public FeedContentReportRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <inheritdoc/>
        public async Task<FeedReportTarget?> GetPostTargetAsync(int postId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Posts.AsNoTracking()
                .Where(p => p.Id == postId)
                .Select(p => new FeedReportTarget(p.Id, null, p.AuthorId, p.AuthorId, p.FriendId, p.AudienceType))
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<FeedReportTarget?> GetCommentTargetAsync(int commentId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Comments.AsNoTracking()
                .Where(c => c.Id == commentId)
                .Select(c => new FeedReportTarget(c.PostId, c.Id, c.AuthorId, c.Post.AuthorId, c.Post.FriendId, c.Post.AudienceType))
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> HasReportedAsync(string reporterId, int? postId, int? commentId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.FeedContentReports.AsNoTracking().Where(r => r.ReporterId == reporterId);
            query = commentId.HasValue
                ? query.Where(r => r.CommentId == commentId)
                : query.Where(r => r.PostId == postId);
            return await query.AnyAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<int> CountReportsSinceAsync(string reporterId, DateTime sinceUtc, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.FeedContentReports.AsNoTracking()
                .CountAsync(r => r.ReporterId == reporterId && r.CreatedAt >= sinceUtc, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> AddAsync(FeedContentReport report, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            context.FeedContentReports.Add(report);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<FeedContentReport?> GetByIdAsync(int reportId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.FeedContentReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<(List<FeedContentReport> Items, int TotalCount)> GetPendingPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.FeedContentReports.AsNoTracking().Where(r => PendingStatuses.Contains(r.Status));
            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .Skip((Math.Max(page, 1) - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        /// <inheritdoc/>
        public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.FeedContentReports.AsNoTracking().CountAsync(r => PendingStatuses.Contains(r.Status), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<FeedContentReport?> UpdateReviewAsync(int reportId, ReportStatus status, string reviewerId, DateTime reviewedAtUtc, string? notes, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var report = await context.FeedContentReports.FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);
            if (report == null)
            {
                return null;
            }

            report.Status = status;
            report.ReviewerId = reviewerId;
            report.ReviewedAt = reviewedAtUtc;
            report.ReviewerNotes = notes;
            await context.SaveChangesAsync(cancellationToken);
            return report;
        }
    }
}
