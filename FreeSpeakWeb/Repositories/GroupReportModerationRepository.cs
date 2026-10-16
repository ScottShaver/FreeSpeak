using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IGroupReportModerationRepository"/>.
    /// </summary>
    public class GroupReportModerationRepository : IGroupReportModerationRepository
    {
        private static readonly ReportStatus[] PendingStatuses = { ReportStatus.NotReviewed, ReportStatus.UnderReview };

        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupReportModerationRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        public GroupReportModerationRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <inheritdoc/>
        public async Task<GroupContentReport?> GetByIdAsync(int reportId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.GroupContentReports.AsNoTracking()
                .Include(r => r.Group)
                .Include(r => r.ViolatedRule)
                .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<(List<GroupReportQueueRow> Items, int TotalCount)> GetQueueAsync(GroupReportQueueFilter filter, DateTime staleBeforeUtc, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.GroupContentReports.AsNoTracking()
                .Where(r => PendingStatuses.Contains(r.Status) && (r.PostId != null || r.CommentId != null));

            if (filter.GroupId.HasValue)
            {
                var groupId = filter.GroupId.Value;
                query = query.Where(r => r.GroupId == groupId);
            }

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
                .Select(r => new GroupReportQueueRow(
                    r.Id,
                    r.GroupId,
                    r.Group.Name,
                    r.PostId,
                    r.CommentId,
                    r.Reason,
                    r.Status,
                    r.CreatedAt,
                    r.PostId != null ? r.Post!.AuthorId : r.Comment!.AuthorId,
                    r.PostId != null
                        ? r.Post!.Author.FirstName + " " + r.Post!.Author.LastName
                        : r.Comment!.Author.FirstName + " " + r.Comment!.Author.LastName,
                    r.ReporterId,
                    r.Reporter.FirstName + " " + r.Reporter.LastName,
                    r.PostId != null ? r.Post!.Content : r.Comment!.Content,
                    context.GroupContentReports.Count(o => o.Id != r.Id
                        && PendingStatuses.Contains(o.Status)
                        && ((r.PostId != null && o.PostId == r.PostId) || (r.CommentId != null && o.CommentId == r.CommentId)))))
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        /// <inheritdoc/>
        public async Task<(int NotReviewed, int UnderReview, int ClosedSince, int Stale)> GetOverviewCountsAsync(DateTime staleBeforeUtc, DateTime closedSinceUtc, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var reports = context.GroupContentReports.AsNoTracking();
            var live = reports.Where(r => r.PostId != null || r.CommentId != null);
            var notReviewed = await live.CountAsync(r => r.Status == ReportStatus.NotReviewed, cancellationToken);
            var underReview = await live.CountAsync(r => r.Status == ReportStatus.UnderReview, cancellationToken);
            var closed = await reports.CountAsync(r => !PendingStatuses.Contains(r.Status) && r.ReviewedAt != null && r.ReviewedAt >= closedSinceUtc, cancellationToken);
            var stale = await live.CountAsync(r => PendingStatuses.Contains(r.Status) && r.CreatedAt < staleBeforeUtc, cancellationToken);
            return (notReviewed, underReview, closed, stale);
        }

        /// <inheritdoc/>
        public async Task<List<GroupReportGroupOption>> GetGroupsWithPendingReportsAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.GroupContentReports.AsNoTracking()
                .Where(r => PendingStatuses.Contains(r.Status) && (r.PostId != null || r.CommentId != null))
                .Select(r => new { r.GroupId, r.Group.Name })
                .Distinct()
                .OrderBy(g => g.Name)
                .Select(g => new GroupReportGroupOption(g.GroupId, g.Name))
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<GroupReportContentSnapshot?> GetContentSnapshotAsync(int? postId, int? commentId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            if (commentId.HasValue)
            {
                return await context.GroupPostComments.AsNoTracking()
                    .Where(c => c.Id == commentId.Value)
                    .Select(c => new GroupReportContentSnapshot(c.Post.GroupId, c.Post.Group.Name, c.PostId, c.Id, c.AuthorId, c.Author.FirstName + " " + c.Author.LastName, c.Content, c.CreatedAt))
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (!postId.HasValue)
            {
                return null;
            }

            return await context.GroupPosts.AsNoTracking()
                .Where(p => p.Id == postId.Value)
                .Select(p => new GroupReportContentSnapshot(p.GroupId, p.Group.Name, p.Id, null, p.AuthorId, p.Author.FirstName + " " + p.Author.LastName, p.Content, p.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<FeedReportRelatedItem>> GetOtherReportsAsync(int? postId, int? commentId, int excludeReportId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            if (!postId.HasValue && !commentId.HasValue)
            {
                return new List<FeedReportRelatedItem>();
            }

            var query = context.GroupContentReports.AsNoTracking().Where(r => r.Id != excludeReportId);
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
            return await context.GroupContentReports.AsNoTracking()
                .CountAsync(r => r.Id != excludeReportId
                    && ((r.PostId != null && r.Post!.AuthorId == authorId) || (r.CommentId != null && r.Comment!.AuthorId == authorId)), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<List<GroupPendingReportRef>> GetPendingReportsAsync(int? postId, int? commentId, bool includeNested, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            if (!postId.HasValue && !commentId.HasValue)
            {
                return new List<GroupPendingReportRef>();
            }

            var commentIds = new List<int>();
            if (commentId.HasValue)
            {
                commentIds.Add(commentId.Value);
                if (includeNested)
                {
                    var frontier = new List<int> { commentId.Value };
                    while (frontier.Count > 0)
                    {
                        var current = frontier;
                        frontier = await context.GroupPostComments.AsNoTracking()
                            .Where(c => c.ParentCommentId != null && current.Contains(c.ParentCommentId.Value))
                            .Select(c => c.Id)
                            .ToListAsync(cancellationToken);
                        commentIds.AddRange(frontier);
                    }
                }
            }
            else if (includeNested)
            {
                commentIds = await context.GroupPostComments.AsNoTracking()
                    .Where(c => c.PostId == postId!.Value)
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
            }

            var query = context.GroupContentReports.AsNoTracking().Where(r => PendingStatuses.Contains(r.Status));
            query = commentId.HasValue
                ? query.Where(r => r.CommentId != null && commentIds.Contains(r.CommentId.Value))
                : query.Where(r => r.PostId == postId || (r.CommentId != null && commentIds.Contains(r.CommentId.Value)));

            return await query
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .Select(r => new GroupPendingReportRef(
                    r.Id,
                    r.ReporterId,
                    r.Status,
                    r.ReviewerId,
                    r.ReviewedAt,
                    r.ReviewerNotes,
                    r.PostId != null ? r.Post!.AuthorId : r.Comment!.AuthorId,
                    r.PostId,
                    r.CommentId))
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<GroupContentReport?> TryTransitionAsync(int reportId, IReadOnlyCollection<ReportStatus> allowedFrom, ReportStatus status, string? reviewerId, DateTime? reviewedAtUtc, string? notes, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var allowed = allowedFrom.ToArray();
            var affected = await context.GroupContentReports
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

            return await context.GroupContentReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);
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
