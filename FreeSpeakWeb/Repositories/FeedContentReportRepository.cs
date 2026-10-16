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

        /// <inheritdoc/>
        public async Task<(List<FeedReportQueueRow> Items, int TotalCount)> GetQueueAsync(FeedReportQueueFilter filter, DateTime staleBeforeUtc, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.FeedContentReports.AsNoTracking().Where(r => PendingStatuses.Contains(r.Status));

            if (filter.Status.HasValue)
            {
                var status = filter.Status.Value;
                query = query.Where(r => r.Status == status);
            }

            if (filter.Reason.HasValue)
            {
                var reason = filter.Reason.Value;
                query = query.Where(r => r.Reason == reason);
            }

            if (filter.ContentType == FeedReportContentType.Post)
            {
                query = query.Where(r => r.PostId != null);
            }
            else if (filter.ContentType == FeedReportContentType.Comment)
            {
                query = query.Where(r => r.CommentId != null);
            }

            if (filter.FromUtc.HasValue)
            {
                var from = filter.FromUtc.Value;
                query = query.Where(r => r.CreatedAt >= from);
            }

            if (filter.ToUtc.HasValue)
            {
                var to = filter.ToUtc.Value;
                query = query.Where(r => r.CreatedAt < to);
            }

            if (filter.StaleOnly)
            {
                query = query.Where(r => r.CreatedAt < staleBeforeUtc);
            }

            var total = await query.CountAsync(cancellationToken);

            var ordered = filter.NewestFirst
                ? query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                : query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id);

            var items = await ordered
                .Skip((Math.Max(page, 1) - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new FeedReportQueueRow(
                    r.Id,
                    r.PostId,
                    r.CommentId,
                    r.Reason,
                    r.Status,
                    r.CreatedAt,
                    r.ContentAuthorId,
                    context.Users.Where(u => u.Id == r.ContentAuthorId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                    r.ReporterId,
                    r.Reporter.FirstName + " " + r.Reporter.LastName,
                    r.PostId != null ? r.Post!.Content : r.Comment!.Content,
                    context.FeedContentReports.Count(o => o.Id != r.Id
                        && PendingStatuses.Contains(o.Status)
                        && ((r.PostId != null && o.PostId == r.PostId) || (r.CommentId != null && o.CommentId == r.CommentId)))))
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        /// <inheritdoc/>
        public async Task<(int NotReviewed, int UnderReview, int ClosedSince, int Stale)> GetOverviewCountsAsync(DateTime staleBeforeUtc, DateTime closedSinceUtc, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var reports = context.FeedContentReports.AsNoTracking();
            var notReviewed = await reports.CountAsync(r => r.Status == ReportStatus.NotReviewed, cancellationToken);
            var underReview = await reports.CountAsync(r => r.Status == ReportStatus.UnderReview, cancellationToken);
            var closed = await reports.CountAsync(r => !PendingStatuses.Contains(r.Status) && r.ReviewedAt != null && r.ReviewedAt >= closedSinceUtc, cancellationToken);
            var stale = await reports.CountAsync(r => PendingStatuses.Contains(r.Status) && r.CreatedAt < staleBeforeUtc, cancellationToken);
            return (notReviewed, underReview, closed, stale);
        }

        /// <inheritdoc/>
        public async Task<FeedReportContentSnapshot?> GetContentSnapshotAsync(int? postId, int? commentId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            if (commentId.HasValue)
            {
                return await context.Comments.AsNoTracking()
                    .Where(c => c.Id == commentId.Value)
                    .Select(c => new FeedReportContentSnapshot(c.PostId, c.Id, c.AuthorId, c.Author.FirstName + " " + c.Author.LastName, c.Content, c.CreatedAt))
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return await context.Posts.AsNoTracking()
                .Where(p => p.Id == postId)
                .Select(p => new FeedReportContentSnapshot(p.Id, null, p.AuthorId, p.Author.FirstName + " " + p.Author.LastName, p.Content, p.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<FeedReportRelatedItem>> GetOtherReportsAsync(int? postId, int? commentId, int excludeReportId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.FeedContentReports.AsNoTracking().Where(r => r.Id != excludeReportId);
            query = commentId.HasValue ? query.Where(r => r.CommentId == commentId) : query.Where(r => r.PostId == postId);
            return await query
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .Select(r => new FeedReportRelatedItem(r.Id, r.Reason, r.Status, r.CreatedAt, r.ReporterId, r.Reporter.FirstName + " " + r.Reporter.LastName, r.Description))
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<int> CountReportsAgainstAuthorAsync(string authorId, int excludeReportId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.FeedContentReports.AsNoTracking()
                .CountAsync(r => r.ContentAuthorId == authorId && r.Id != excludeReportId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<FeedContentReport>> GetPendingReplyReportsAsync(int commentId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var descendantIds = new List<int>();
            var frontier = new List<int> { commentId };
            while (frontier.Count > 0)
            {
                var current = frontier;
                frontier = await context.Comments.AsNoTracking()
                    .Where(c => c.ParentCommentId != null && current.Contains(c.ParentCommentId.Value))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                descendantIds.AddRange(frontier);
            }

            if (descendantIds.Count == 0)
            {
                return new List<FeedContentReport>();
            }

            return await context.FeedContentReports.AsNoTracking()
                .Where(r => r.CommentId != null && descendantIds.Contains(r.CommentId.Value) && PendingStatuses.Contains(r.Status))
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<(int ReportId, string ReporterId)>> GetPendingReportersAsync(int? postId, int? commentId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.FeedContentReports.AsNoTracking().Where(r => PendingStatuses.Contains(r.Status));
            query = commentId.HasValue ? query.Where(r => r.CommentId == commentId) : query.Where(r => r.PostId == postId);
            var rows = await query.Select(r => new { r.Id, r.ReporterId }).ToListAsync(cancellationToken);
            return rows.Select(r => (r.Id, r.ReporterId)).ToList();
        }

        /// <inheritdoc/>
        public async Task<FeedContentReport?> TryTransitionAsync(int reportId, IReadOnlyCollection<ReportStatus> allowedFrom, ReportStatus status, string? reviewerId, DateTime? reviewedAtUtc, string? notes, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var allowed = allowedFrom.ToArray();
            var affected = await context.FeedContentReports
                .Where(r => r.Id == reportId && allowed.Contains(r.Status))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, status)
                    .SetProperty(r => r.ReviewerId, reviewerId)
                    .SetProperty(r => r.ReviewedAt, reviewedAtUtc)
                    .SetProperty(r => r.ReviewerNotes, notes), cancellationToken);

            if (affected == 0)
            {
                return null;
            }

            return await context.FeedContentReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<FeedContentReport>> CloseOtherPendingAsync(int? postId, int? commentId, int excludeReportId, ReportStatus status, string reviewerId, DateTime reviewedAtUtc, string? notes, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.FeedContentReports.Where(r => r.Id != excludeReportId && PendingStatuses.Contains(r.Status));
            query = commentId.HasValue ? query.Where(r => r.CommentId == commentId) : query.Where(r => r.PostId == postId);
            var reports = await query.ToListAsync(cancellationToken);
            foreach (var report in reports)
            {
                report.Status = status;
                report.ReviewerId = reviewerId;
                report.ReviewedAt = reviewedAtUtc;
                report.ReviewerNotes = notes;
            }

            await context.SaveChangesAsync(cancellationToken);
            return reports;
        }

        /// <inheritdoc/>
        public async Task<string?> GetUserDisplayNameAsync(string userId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
