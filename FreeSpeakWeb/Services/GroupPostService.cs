using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    public class GroupPostService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupPostService> _logger;
        private readonly NotificationService _notificationService;

        public GroupPostService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupPostService> logger,
            NotificationService notificationService)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _notificationService = notificationService;
        }

        #region Post Operations

        /// <summary>
        /// Create a new group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, GroupPost? Post)> CreateGroupPostAsync(
            int groupId,
            string authorId,
            string content,
            List<string>? imageUrls = null)
        {
            if (string.IsNullOrWhiteSpace(content) && (imageUrls == null || !imageUrls.Any()))
            {
                return (false, "Post must contain either text or images.", null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify the user is a member of the group
                var isMember = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == groupId && gu.UserId == authorId);

                if (!isMember)
                {
                    return (false, "You must be a member of the group to post.", null);
                }

                // Check if user is banned
                var isBanned = await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == groupId && gbm.UserId == authorId);

                if (isBanned)
                {
                    return (false, "You are banned from this group.", null);
                }

                var post = new GroupPost
                {
                    GroupId = groupId,
                    AuthorId = authorId,
                    Content = string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Add images if provided
                if (imageUrls != null && imageUrls.Any())
                {
                    for (int i = 0; i < imageUrls.Count; i++)
                    {
                        var postImage = new GroupPostImage
                        {
                            PostId = post.Id,
                            ImageUrl = imageUrls[i],
                            DisplayOrder = i,
                            UploadedAt = DateTime.UtcNow
                        };
                        context.GroupPostImages.Add(postImage);
                    }
                    await context.SaveChangesAsync();
                }

                // Update group's last active timestamp
                var group = await context.Groups.FindAsync(groupId);
                if (group != null)
                {
                    group.LastActiveAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }

                _logger.LogInformation("Group post created by user {AuthorId} in group {GroupId}: Post ID {PostId}", authorId, groupId, post.Id);
                return (true, null, post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group post for user {AuthorId} in group {GroupId}", authorId, groupId);
                return (false, "An error occurred while creating the post.", null);
            }
        }

        /// <summary>
        /// Update an existing group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, List<GroupPostImage>? UpdatedImages)> UpdateGroupPostAsync(
            int postId,
            string userId,
            string newContent,
            List<string>? newImageUrls = null,
            List<int>? removedImageIds = null)
        {
            var hasImages = newImageUrls != null && newImageUrls.Any();

            if (string.IsNullOrWhiteSpace(newContent) && !hasImages && (removedImageIds == null || !removedImageIds.Any()))
            {
                return (false, "Post must contain either text or images.", null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.GroupPosts
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                if (post == null)
                {
                    return (false, "Post not found.", null);
                }

                if (post.AuthorId != userId)
                {
                    return (false, "You are not authorized to edit this post.", null);
                }

                // Update content
                post.Content = string.IsNullOrWhiteSpace(newContent) ? string.Empty : newContent.Trim();
                post.UpdatedAt = DateTime.UtcNow;

                // Remove specified images
                if (removedImageIds != null && removedImageIds.Any())
                {
                    var imagesToRemove = post.Images
                        .Where(img => removedImageIds.Contains(img.Id))
                        .ToList();

                    foreach (var image in imagesToRemove)
                    {
                        context.GroupPostImages.Remove(image);
                    }
                }

                // Add new images
                if (newImageUrls != null && newImageUrls.Any())
                {
                    var currentMaxOrder = post.Images.Any() ? post.Images.Max(img => img.DisplayOrder) : -1;

                    for (int i = 0; i < newImageUrls.Count; i++)
                    {
                        var postImage = new GroupPostImage
                        {
                            PostId = post.Id,
                            ImageUrl = newImageUrls[i],
                            DisplayOrder = currentMaxOrder + 1 + i,
                            UploadedAt = DateTime.UtcNow
                        };
                        context.GroupPostImages.Add(postImage);
                    }
                }

                await context.SaveChangesAsync();

                // Reload images to get the updated collection
                await context.Entry(post).Collection(p => p.Images).LoadAsync();

                // Verify post still has content or images
                if (string.IsNullOrWhiteSpace(post.Content) && !post.Images.Any())
                {
                    return (false, "Post must contain either text or images.", null);
                }

                _logger.LogInformation("Group post {PostId} updated by user {UserId}", postId, userId);
                return (true, null, post.Images.OrderBy(i => i.DisplayOrder).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while updating the post.", null);
            }
        }

        /// <summary>
        /// Delete a group post and all related data
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> DeleteGroupPostAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.GroupPosts
                    .Include(p => p.Images)
                    .Include(p => p.Group)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                if (post == null)
                {
                    return (false, "Post not found.");
                }

                // Check if user is the author or a group admin/moderator
                var isAuthor = post.AuthorId == userId;
                var isAdminOrModerator = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == post.GroupId && 
                                   gu.UserId == userId && 
                                   (gu.IsAdmin || gu.IsModerator));

                if (!isAuthor && !isAdminOrModerator)
                {
                    return (false, "You are not authorized to delete this post.");
                }

                // Delete all pinned group post records
                var pinnedPosts = await context.PinnedGroupPosts
                    .Where(pp => pp.PostId == postId)
                    .ToListAsync();

                if (pinnedPosts.Any())
                {
                    context.PinnedGroupPosts.RemoveRange(pinnedPosts);
                    _logger.LogInformation("Deleted {Count} pinned group post record(s) for post {PostId}", pinnedPosts.Count, postId);
                }

                // Delete the post (images will cascade)
                context.GroupPosts.Remove(post);
                await context.SaveChangesAsync();

                _logger.LogInformation("Group post {PostId} deleted by user {UserId}", postId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while deleting the post.");
            }
        }

        #endregion

        #region Retrieval Operations

        /// <summary>
        /// Get a specific group post by ID
        /// </summary>
        public async Task<GroupPost?> GetGroupPostByIdAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPosts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Include(p => p.Group)
                    .FirstOrDefaultAsync(p => p.Id == postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group post {PostId}", postId);
                return null;
            }
        }

        /// <summary>
        /// Get all posts for a specific group
        /// </summary>
        public async Task<List<GroupPost>> GetGroupPostsAsync(int groupId, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPosts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Where(p => p.GroupId == groupId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts for group {GroupId}", groupId);
                return new List<GroupPost>();
            }
        }

        /// <summary>
        /// Get posts by a specific user in a group
        /// </summary>
        public async Task<List<GroupPost>> GetUserGroupPostsAsync(int groupId, string userId, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPosts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Where(p => p.GroupId == groupId && p.AuthorId == userId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts for user {UserId} in group {GroupId}", userId, groupId);
                return new List<GroupPost>();
            }
        }

        /// <summary>
        /// Get the total count of posts in a group
        /// </summary>
        public async Task<int> GetGroupPostCountAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPosts.CountAsync(p => p.GroupId == groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting posts for group {GroupId}", groupId);
                return 0;
            }
        }

        #endregion
    }
}
