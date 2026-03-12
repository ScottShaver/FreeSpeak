using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    public class PinnedGroupPostService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<PinnedGroupPostService> _logger;

        public PinnedGroupPostService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<PinnedGroupPostService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Pin a group post for a user
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> PinGroupPostAsync(string userId, int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify the post exists
                var post = await context.GroupPosts
                    .Include(p => p.Group)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                if (post == null)
                {
                    return (false, "Post not found.");
                }

                // Verify the user is a member of the group
                var isMember = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == post.GroupId && gu.UserId == userId);

                if (!isMember)
                {
                    return (false, "You must be a member of the group to pin posts.");
                }

                // Check if already pinned
                var existingPin = await context.PinnedGroupPosts
                    .FirstOrDefaultAsync(pp => pp.UserId == userId && pp.PostId == postId);

                if (existingPin != null)
                {
                    return (false, "Post is already pinned.");
                }

                var pinnedPost = new PinnedGroupPost
                {
                    UserId = userId,
                    PostId = postId,
                    PinnedAt = DateTime.UtcNow
                };

                context.PinnedGroupPosts.Add(pinnedPost);
                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} pinned group post {PostId}", userId, postId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning group post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while pinning the post.");
            }
        }

        /// <summary>
        /// Unpin a group post for a user
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> UnpinGroupPostAsync(string userId, int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var pinnedPost = await context.PinnedGroupPosts
                    .FirstOrDefaultAsync(pp => pp.UserId == userId && pp.PostId == postId);

                if (pinnedPost == null)
                {
                    return (false, "Post is not pinned.");
                }

                context.PinnedGroupPosts.Remove(pinnedPost);
                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} unpinned group post {PostId}", userId, postId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning group post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while unpinning the post.");
            }
        }

        /// <summary>
        /// Check if a user has pinned a specific group post
        /// </summary>
        public async Task<bool> IsGroupPostPinnedAsync(string userId, int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.PinnedGroupPosts
                    .AnyAsync(pp => pp.UserId == userId && pp.PostId == postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if group post {PostId} is pinned for user {UserId}", postId, userId);
                return false;
            }
        }

        /// <summary>
        /// Get all pinned group posts for a user
        /// </summary>
        public async Task<List<GroupPost>> GetPinnedGroupPostsAsync(string userId, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var pinnedPostIds = await context.PinnedGroupPosts
                    .Where(pp => pp.UserId == userId)
                    .OrderByDescending(pp => pp.PinnedAt)
                    .Skip(skip)
                    .Take(take)
                    .Select(pp => pp.PostId)
                    .ToListAsync();

                if (!pinnedPostIds.Any())
                {
                    return new List<GroupPost>();
                }

                var posts = await context.GroupPosts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Include(p => p.Group)
                    .Where(p => pinnedPostIds.Contains(p.Id))
                    .ToListAsync();

                // Maintain the order from pinnedPostIds
                return pinnedPostIds
                    .Select(id => posts.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .Cast<GroupPost>()
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pinned group posts for user {UserId}", userId);
                return new List<GroupPost>();
            }
        }

        /// <summary>
        /// Get all pinned group posts for a user in a specific group
        /// </summary>
        public async Task<List<GroupPost>> GetPinnedGroupPostsByGroupAsync(string userId, int groupId, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var pinnedPostIds = await context.PinnedGroupPosts
                    .Where(pp => pp.UserId == userId)
                    .Join(context.GroupPosts,
                        pp => pp.PostId,
                        gp => gp.Id,
                        (pp, gp) => new { pp, gp })
                    .Where(x => x.gp.GroupId == groupId)
                    .OrderByDescending(x => x.pp.PinnedAt)
                    .Skip(skip)
                    .Take(take)
                    .Select(x => x.pp.PostId)
                    .ToListAsync();

                if (!pinnedPostIds.Any())
                {
                    return new List<GroupPost>();
                }

                var posts = await context.GroupPosts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Include(p => p.Group)
                    .Where(p => pinnedPostIds.Contains(p.Id))
                    .ToListAsync();

                // Maintain the order from pinnedPostIds
                return pinnedPostIds
                    .Select(id => posts.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .Cast<GroupPost>()
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pinned group posts for user {UserId} in group {GroupId}", userId, groupId);
                return new List<GroupPost>();
            }
        }

        /// <summary>
        /// Get the count of pinned group posts for a user
        /// </summary>
        public async Task<int> GetPinnedGroupPostCountAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.PinnedGroupPosts
                    .CountAsync(pp => pp.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting pinned group posts for user {UserId}", userId);
                return 0;
            }
        }
    }
}
