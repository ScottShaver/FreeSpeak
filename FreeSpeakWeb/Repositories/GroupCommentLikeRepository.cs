using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    public class GroupCommentLikeRepository : IGroupCommentLikeRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupCommentLikeRepository> _logger;

        public GroupCommentLikeRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupCommentLikeRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<(bool Success, string? ErrorMessage, GroupPostCommentLike? Like)> AddOrUpdateAsync(int commentId, string userId, LikeType likeType)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var existingLike = await context.GroupPostCommentLikes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);

                if (existingLike != null)
                {
                    existingLike.Type = likeType;
                    await context.SaveChangesAsync();
                    return (true, null, existingLike);
                }

                var newLike = new GroupPostCommentLike { CommentId = commentId, UserId = userId, Type = likeType, CreatedAt = DateTime.UtcNow };
                context.GroupPostCommentLikes.Add(newLike);
                await context.SaveChangesAsync();
                return (true, null, newLike);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating like on group comment {CommentId}", commentId);
                return (false, "An error occurred while adding the like.", null);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> RemoveAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var like = await context.GroupPostCommentLikes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);
                if (like != null)
                {
                    context.GroupPostCommentLikes.Remove(like);
                    await context.SaveChangesAsync();
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing like from group comment {CommentId}", commentId);
                return (false, "An error occurred while removing the like.");
            }
        }

        public async Task<GroupPostCommentLike?> GetUserLikeAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostCommentLikes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user like for group comment {CommentId}", commentId);
                return null;
            }
        }

        public async Task<bool> HasUserLikedAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostCommentLikes.AnyAsync(l => l.CommentId == commentId && l.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user liked group comment {CommentId}", commentId);
                return false;
            }
        }

        public async Task<List<GroupPostCommentLike>> GetByCommentAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostCommentLikes.Include(l => l.User).Where(l => l.CommentId == commentId).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving likes for group comment {CommentId}", commentId);
                return new List<GroupPostCommentLike>();
            }
        }

        public async Task<int> GetCountAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostCommentLikes.CountAsync(l => l.CommentId == commentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting likes for group comment {CommentId}", commentId);
                return 0;
            }
        }

        public async Task<Dictionary<LikeType, int>> GetCountsByTypeAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostCommentLikes.Where(l => l.CommentId == commentId).GroupBy(l => l.Type).ToDictionaryAsync(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting likes by type for group comment {CommentId}", commentId);
                return new Dictionary<LikeType, int>();
            }
        }

        public async Task<Dictionary<int, GroupPostCommentLike?>> GetUserLikesForCommentsAsync(string userId, IEnumerable<int> commentIds)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var commentIdList = commentIds.ToList();
                var likes = await context.GroupPostCommentLikes.Where(l => l.UserId == userId && commentIdList.Contains(l.CommentId)).ToListAsync();
                return commentIdList.ToDictionary(commentId => commentId, commentId => likes.FirstOrDefault(l => l.CommentId == commentId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user likes for multiple group comments");
                return commentIds.ToDictionary(id => id, _ => (GroupPostCommentLike?)null);
            }
        }

        /// <summary>
        /// Gets the total like count for multiple comments in a single query.
        /// Useful for batch loading like counts when displaying comment lists.
        /// </summary>
        /// <param name="commentIds">Collection of comment IDs to get counts for.</param>
        /// <returns>A dictionary mapping each comment ID to its like count.</returns>
        public async Task<Dictionary<int, int>> GetCountsForCommentsAsync(IEnumerable<int> commentIds)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var commentIdList = commentIds.ToList();
                var counts = await context.GroupPostCommentLikes
                    .Where(l => commentIdList.Contains(l.CommentId))
                    .GroupBy(l => l.CommentId)
                    .Select(g => new { CommentId = g.Key, Count = g.Count() })
                    .ToListAsync();

                return commentIdList.ToDictionary(
                    commentId => commentId,
                    commentId => counts.FirstOrDefault(c => c.CommentId == commentId)?.Count ?? 0
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving like counts for multiple group comments");
                return commentIds.ToDictionary(id => id, _ => 0);
            }
        }

        /// <summary>
        /// Gets the reaction breakdown (like counts by type) for multiple comments in a single query.
        /// Useful for batch loading reaction data when displaying comment lists.
        /// </summary>
        /// <param name="commentIds">Collection of comment IDs to get reaction breakdowns for.</param>
        /// <returns>A dictionary mapping each comment ID to its reaction breakdown (LikeType to count).</returns>
        public async Task<Dictionary<int, Dictionary<LikeType, int>>> GetReactionBreakdownsForCommentsAsync(IEnumerable<int> commentIds)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var commentIdList = commentIds.ToList();
                var reactions = await context.GroupPostCommentLikes
                    .Where(l => commentIdList.Contains(l.CommentId))
                    .GroupBy(l => new { l.CommentId, l.Type })
                    .Select(g => new { g.Key.CommentId, g.Key.Type, Count = g.Count() })
                    .ToListAsync();

                var result = commentIdList.ToDictionary(
                    commentId => commentId,
                    commentId => reactions
                        .Where(r => r.CommentId == commentId)
                        .ToDictionary(r => r.Type, r => r.Count)
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reaction breakdowns for multiple group comments");
                return commentIds.ToDictionary(id => id, _ => new Dictionary<LikeType, int>());
            }
        }
    }
}
