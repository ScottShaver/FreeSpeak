using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for feed comment likes.
    /// Provides operations for adding, removing, and querying likes on comments.
    /// </summary>
    public class FeedCommentLikeRepository : IFeedCommentLikeRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<FeedCommentLikeRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedCommentLikeRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        public FeedCommentLikeRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FeedCommentLikeRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Adds a new like or updates an existing like on a comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment to like.</param>
        /// <param name="userId">The unique identifier of the user adding the like.</param>
        /// <param name="likeType">The type of reaction (e.g., Like, Love, Laugh).</param>
        /// <returns>A tuple containing success status, error message if any, and the like entity.</returns>
        public async Task<(bool Success, string? ErrorMessage, CommentLike? Like)> AddOrUpdateAsync(int commentId, string userId, LikeType likeType)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var existingLike = await context.CommentLikes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);

                if (existingLike != null)
                {
                    existingLike.Type = likeType;
                    await context.SaveChangesAsync();
                    return (true, null, existingLike);
                }

                var newLike = new CommentLike { CommentId = commentId, UserId = userId, Type = likeType, CreatedAt = DateTime.UtcNow };
                context.CommentLikes.Add(newLike);
                await context.SaveChangesAsync();
                return (true, null, newLike);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating like on comment {CommentId}", commentId);
                return (false, "An error occurred while adding the like.", null);
            }
        }

        /// <summary>
        /// Removes a user's like from a comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The unique identifier of the user removing their like.</param>
        /// <returns>A tuple containing success status and error message if any.</returns>
        public async Task<(bool Success, string? ErrorMessage)> RemoveAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var like = await context.CommentLikes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);
                if (like != null)
                {
                    context.CommentLikes.Remove(like);
                    await context.SaveChangesAsync();
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing like from comment {CommentId}", commentId);
                return (false, "An error occurred while removing the like.");
            }
        }

        /// <summary>
        /// Retrieves a user's like on a specific comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The like entity if found; otherwise, null.</returns>
        public async Task<CommentLike?> GetUserLikeAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.CommentLikes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user like for comment {CommentId}", commentId);
                return null;
            }
        }

        /// <summary>
        /// Checks whether a user has liked a specific comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user has liked the comment; otherwise, false.</returns>
        public async Task<bool> HasUserLikedAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.CommentLikes.AnyAsync(l => l.CommentId == commentId && l.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user liked comment {CommentId}", commentId);
                return false;
            }
        }

        /// <summary>
        /// Retrieves all likes for a specific comment, including user information.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>A list of likes on the comment.</returns>
        public async Task<List<CommentLike>> GetByCommentAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.CommentLikes.Include(l => l.User).Where(l => l.CommentId == commentId).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving likes for comment {CommentId}", commentId);
                return new List<CommentLike>();
            }
        }

        /// <summary>
        /// Gets the total count of likes for a specific comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>The total number of likes on the comment.</returns>
        public async Task<int> GetCountAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.CommentLikes.CountAsync(l => l.CommentId == commentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting likes for comment {CommentId}", commentId);
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of likes grouped by reaction type for a specific comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>A dictionary mapping each like type to its count.</returns>
        public async Task<Dictionary<LikeType, int>> GetCountsByTypeAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.CommentLikes.Where(l => l.CommentId == commentId).GroupBy(l => l.Type).ToDictionaryAsync(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting likes by type for comment {CommentId}", commentId);
                return new Dictionary<LikeType, int>();
            }
        }

        /// <summary>
        /// Retrieves a user's likes for multiple comments in a single query.
        /// Useful for batch loading like status when displaying comments.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="commentIds">Collection of comment IDs to check for likes.</param>
        /// <returns>A dictionary mapping each comment ID to the user's like (or null if not liked).</returns>
        public async Task<Dictionary<int, CommentLike?>> GetUserLikesForCommentsAsync(string userId, IEnumerable<int> commentIds)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var commentIdList = commentIds.ToList();
                var likes = await context.CommentLikes.Where(l => l.UserId == userId && commentIdList.Contains(l.CommentId)).ToListAsync();
                return commentIdList.ToDictionary(commentId => commentId, commentId => likes.FirstOrDefault(l => l.CommentId == commentId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user likes for multiple comments");
                return commentIds.ToDictionary(id => id, _ => (CommentLike?)null);
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
                var counts = await context.CommentLikes
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
                _logger.LogError(ex, "Error retrieving like counts for multiple comments");
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
                var reactions = await context.CommentLikes
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
                _logger.LogError(ex, "Error retrieving reaction breakdowns for multiple comments");
                return commentIds.ToDictionary(id => id, _ => new Dictionary<LikeType, int>());
            }
        }
    }
}
