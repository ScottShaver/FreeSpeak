using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    public class FeedCommentLikeRepository : IFeedCommentLikeRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<FeedCommentLikeRepository> _logger;

        public FeedCommentLikeRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FeedCommentLikeRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

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
    }
}
