using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for group post likes
    /// </summary>
    public class GroupPostLikeRepository : IGroupPostLikeRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupPostLikeRepository> _logger;

        public GroupPostLikeRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupPostLikeRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<(bool Success, string? ErrorMessage, GroupPostLike? Like)> AddOrUpdateAsync(int postId, string userId, LikeType likeType)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var existingLike = await context.GroupPostLikes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

                if (existingLike != null)
                {
                    existingLike.Type = likeType;
                    await context.SaveChangesAsync();
                    return (true, null, existingLike);
                }

                var newLike = new GroupPostLike
                {
                    PostId = postId,
                    UserId = userId,
                    Type = likeType,
                    CreatedAt = DateTime.UtcNow
                };

                context.GroupPostLikes.Add(newLike);
                await context.SaveChangesAsync();

                return (true, null, newLike);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating like on group post {PostId}", postId);
                return (false, "An error occurred while adding the like.", null);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> RemoveAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var like = await context.GroupPostLikes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

                if (like != null)
                {
                    context.GroupPostLikes.Remove(like);
                    await context.SaveChangesAsync();
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing like from group post {PostId}", postId);
                return (false, "An error occurred while removing the like.");
            }
        }

        public async Task<GroupPostLike?> GetUserLikeAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostLikes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user like for group post {PostId}", postId);
                return null;
            }
        }

        public async Task<bool> HasUserLikedAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostLikes.AnyAsync(l => l.PostId == postId && l.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user liked group post {PostId}", postId);
                return false;
            }
        }

        public async Task<List<GroupPostLike>> GetByPostAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostLikes
                    .Include(l => l.User)
                    .Where(l => l.PostId == postId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving likes for group post {PostId}", postId);
                return new List<GroupPostLike>();
            }
        }

        public async Task<int> GetCountAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostLikes.CountAsync(l => l.PostId == postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting likes for group post {PostId}", postId);
                return 0;
            }
        }

        public async Task<Dictionary<LikeType, int>> GetCountsByTypeAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostLikes
                    .Where(l => l.PostId == postId)
                    .GroupBy(l => l.Type)
                    .ToDictionaryAsync(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting likes by type for group post {PostId}", postId);
                return new Dictionary<LikeType, int>();
            }
        }

        public async Task<List<ApplicationUser>> GetLikedByUsersAsync(int postId, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostLikes
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
                _logger.LogError(ex, "Error retrieving users who liked group post {PostId}", postId);
                return new List<ApplicationUser>();
            }
        }

        public async Task<List<int>> GetPostIdsLikedByUserAsync(string userId, int skip = 0, int take = 50)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostLikes
                    .Where(l => l.UserId == userId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .Select(l => l.PostId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group posts liked by user {UserId}", userId);
                return new List<int>();
            }
        }

        public async Task<Dictionary<int, GroupPostLike?>> GetUserLikesForPostsAsync(string userId, IEnumerable<int> postIds)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var postIdList = postIds.ToList();

                var likes = await context.GroupPostLikes
                    .Where(l => l.UserId == userId && postIdList.Contains(l.PostId))
                    .ToListAsync();

                return postIdList.ToDictionary(
                    postId => postId,
                    postId => likes.FirstOrDefault(l => l.PostId == postId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user likes for multiple group posts");
                return postIds.ToDictionary(id => id, _ => (GroupPostLike?)null);
            }
        }
    }
}
