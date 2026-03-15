using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service providing business logic for managing pinned group posts.
    /// Allows users to pin/unpin group posts and retrieve their pinned posts.
    /// </summary>
    public class PinnedGroupPostService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<PinnedGroupPostService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedGroupPostService"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        public PinnedGroupPostService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<PinnedGroupPostService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Pins a group post for a user so it appears in their pinned posts collection.
        /// User must be a member of the group to pin posts.
        /// </summary>
        /// <param name="userId">The unique identifier of the user pinning the post.</param>
        /// <param name="postId">The unique identifier of the post to pin.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Unpins a group post for a user, removing it from their pinned posts collection.
        /// </summary>
        /// <param name="userId">The unique identifier of the user unpinning the post.</param>
        /// <param name="postId">The unique identifier of the post to unpin.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Checks if a user has pinned a specific group post.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>True if the post is pinned by the user; otherwise, false.</returns>
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
        /// Retrieves all pinned group posts for a user across all groups with pagination.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of pinned group posts ordered by most recently pinned first.</returns>
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
        /// Retrieves all pinned group posts for a user in a specific group with pagination.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of pinned group posts in the specified group.</returns>
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
        /// Gets the total count of pinned group posts for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The number of pinned group posts.</returns>
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

        /// <summary>
        /// Retrieves all pinned group posts for a user as projection DTOs with pagination.
        /// Uses database-side projection to reduce data transfer.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of pinned group posts as GroupPostListDto ordered by most recently pinned first.</returns>
        public async Task<List<GroupPostListDto>> GetPinnedGroupPostsAsProjectionAsync(string userId, int skip = 0, int take = 20)
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
                    return new List<GroupPostListDto>();
                }

                var posts = await context.GroupPosts
                    .Where(p => pinnedPostIds.Contains(p.Id))
                    .Select(p => new GroupPostListDto(
                        p.Id,
                        p.GroupId,
                        p.Group != null ? p.Group.Name : "Unknown Group",
                        p.AuthorId,
                        p.Author != null ? $"{p.Author.FirstName} {p.Author.LastName}" : "Unknown",
                        p.Author != null ? p.Author.ProfilePictureUrl : null,
                        p.Content,
                        p.CreatedAt,
                        p.UpdatedAt,
                        p.LikeCount,
                        p.CommentCount,
                        p.ShareCount,
                        context.GroupUsers
                            .Where(gu => gu.GroupId == p.GroupId && gu.UserId == p.AuthorId)
                            .Select(gu => gu.GroupPoints)
                            .FirstOrDefault(),
                        p.Images.OrderBy(i => i.DisplayOrder).Select(i => new PostImageDto(
                            i.Id,
                            i.ImageUrl,
                            i.DisplayOrder
                        )).ToList()
                    ))
                    .ToListAsync();

                // Maintain the order from pinnedPostIds
                return pinnedPostIds
                    .Select(id => posts.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .Cast<GroupPostListDto>()
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pinned group posts as projection for user {UserId}", userId);
                return new List<GroupPostListDto>();
            }
        }
    }
}
