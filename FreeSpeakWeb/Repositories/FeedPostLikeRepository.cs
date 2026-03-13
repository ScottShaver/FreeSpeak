using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for feed post likes.
    /// Provides operations for adding, removing, and querying likes on feed posts.
    /// </summary>
    public class FeedPostLikeRepository : IFeedPostLikeRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<FeedPostLikeRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedPostLikeRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        public FeedPostLikeRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FeedPostLikeRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Adds a new like or updates an existing like on a post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to like.</param>
        /// <param name="userId">The unique identifier of the user adding the like.</param>
        /// <param name="likeType">The type of reaction (e.g., Like, Love, Laugh).</param>
        /// <returns>A tuple containing success status, error message if any, and the like entity.</returns>
        public async Task<(bool Success, string? ErrorMessage, Like? Like)> AddOrUpdateAsync(int postId, string userId, LikeType likeType)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var existingLike = await context.Likes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

                if (existingLike != null)
                {
                    existingLike.Type = likeType;
                    await context.SaveChangesAsync();
                    return (true, null, existingLike);
                }

                var newLike = new Like
                {
                    PostId = postId,
                    UserId = userId,
                    Type = likeType,
                    CreatedAt = DateTime.UtcNow
                };

                context.Likes.Add(newLike);
                await context.SaveChangesAsync();

                return (true, null, newLike);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating like on post {PostId}", postId);
                return (false, "An error occurred while adding the like.", null);
            }
        }

        /// <summary>
        /// Removes a user's like from a post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user removing their like.</param>
        /// <returns>A tuple containing success status and error message if any.</returns>
        public async Task<(bool Success, string? ErrorMessage)> RemoveAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var like = await context.Likes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

                if (like != null)
                {
                    context.Likes.Remove(like);
                    await context.SaveChangesAsync();
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing like from post {PostId}", postId);
                return (false, "An error occurred while removing the like.");
            }
        }

        /// <summary>
        /// Retrieves a user's like on a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The like entity if found; otherwise, null.</returns>
        public async Task<Like?> GetUserLikeAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Likes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user like for post {PostId}", postId);
                return null;
            }
        }

        /// <summary>
        /// Checks whether a user has liked a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user has liked the post; otherwise, false.</returns>
        public async Task<bool> HasUserLikedAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Likes.AnyAsync(l => l.PostId == postId && l.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user liked post {PostId}", postId);
                return false;
            }
        }

        /// <summary>
        /// Retrieves all likes for a specific post, including user information.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of likes on the post.</returns>
        public async Task<List<Like>> GetByPostAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Likes
                    .Include(l => l.User)
                    .Where(l => l.PostId == postId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving likes for post {PostId}", postId);
                return new List<Like>();
            }
        }

        /// <summary>
        /// Gets the total count of likes for a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>The total number of likes on the post.</returns>
        public async Task<int> GetCountAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Likes.CountAsync(l => l.PostId == postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting likes for post {PostId}", postId);
                return 0;
            }
        }

        /// <summary>
        /// Gets the count of likes grouped by reaction type for a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A dictionary mapping each like type to its count.</returns>
        public async Task<Dictionary<LikeType, int>> GetCountsByTypeAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Likes
                    .Where(l => l.PostId == postId)
                    .GroupBy(l => l.Type)
                    .ToDictionaryAsync(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting likes by type for post {PostId}", postId);
                return new Dictionary<LikeType, int>();
            }
        }

        /// <summary>
        /// Retrieves users who have liked a specific post with pagination support.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="skip">Number of users to skip for pagination.</param>
        /// <param name="take">Number of users to return.</param>
        /// <returns>A list of users who liked the post, ordered by like creation date descending.</returns>
        public async Task<List<ApplicationUser>> GetLikedByUsersAsync(int postId, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Likes
                    .Where(l => l.PostId == postId)
                    .Include(l => l.User)
                    .OrderByDescending(l => l.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .Select(l => l.User)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users who liked post {PostId}", postId);
                return new List<ApplicationUser>();
            }
        }

        /// <summary>
        /// Retrieves the IDs of posts that a user has liked with pagination support.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="skip">Number of post IDs to skip for pagination.</param>
        /// <param name="take">Number of post IDs to return.</param>
        /// <returns>A list of post IDs liked by the user, ordered by like creation date descending.</returns>
        public async Task<List<int>> GetPostIdsLikedByUserAsync(string userId, int skip = 0, int take = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Likes
                    .Where(l => l.UserId == userId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .Select(l => l.PostId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts liked by user {UserId}", userId);
                return new List<int>();
            }
        }

        /// <summary>
        /// Retrieves a user's likes for multiple posts in a single query.
        /// Useful for batch loading like status when displaying a feed.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="postIds">Collection of post IDs to check for likes.</param>
        /// <returns>A dictionary mapping each post ID to the user's like (or null if not liked).</returns>
        public async Task<Dictionary<int, Like?>> GetUserLikesForPostsAsync(string userId, IEnumerable<int> postIds)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var postIdList = postIds.ToList();

                var likes = await context.Likes
                    .Where(l => l.UserId == userId && postIdList.Contains(l.PostId))
                    .ToListAsync();

                return postIdList.ToDictionary(
                    postId => postId,
                    postId => likes.FirstOrDefault(l => l.PostId == postId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user likes for multiple posts");
                return postIds.ToDictionary(id => id, _ => (Like?)null);
            }
        }
    }
}
