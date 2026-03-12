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

        #region Comment Operations

        /// <summary>
        /// Add a comment to a group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, GroupPostComment? Comment)> AddCommentAsync(
            int postId,
            string authorId,
            string content,
            string? imageUrl = null,
            int? parentCommentId = null)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return (false, "Comment content cannot be empty.", null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify post exists and get group info
                var post = await context.GroupPosts
                    .Include(p => p.Author)
                    .Include(p => p.Group)
                    .FirstOrDefaultAsync(p => p.Id == postId);
                if (post == null)
                {
                    return (false, "Post not found.", null);
                }

                // Verify user is a member of the group
                var isMember = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == post.GroupId && gu.UserId == authorId);

                if (!isMember)
                {
                    return (false, "You must be a member of the group to comment.", null);
                }

                // Check if user is banned
                var isBanned = await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == post.GroupId && gbm.UserId == authorId);

                if (isBanned)
                {
                    return (false, "You are banned from this group.", null);
                }

                GroupPostComment? parentComment = null;
                // Verify parent comment exists if specified
                if (parentCommentId.HasValue)
                {
                    parentComment = await context.GroupPostComments
                        .Include(c => c.Author)
                        .FirstOrDefaultAsync(c => c.Id == parentCommentId.Value);
                    if (parentComment == null)
                    {
                        return (false, "Parent comment not found.", null);
                    }
                    if (parentComment.PostId != postId)
                    {
                        return (false, "Parent comment does not belong to this post.", null);
                    }
                }

                var comment = new GroupPostComment
                {
                    PostId = postId,
                    AuthorId = authorId,
                    Content = content.Trim(),
                    ImageUrl = imageUrl,
                    ParentCommentId = parentCommentId,
                    CreatedAt = DateTime.UtcNow
                };

                context.GroupPostComments.Add(comment);

                // Update post comment count
                post.CommentCount++;

                await context.SaveChangesAsync();

                _logger.LogInformation("Comment added to group post {PostId} by user {AuthorId}", postId, authorId);
                return (true, null, comment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment to group post {PostId} for user {AuthorId}", postId, authorId);
                return (false, "An error occurred while adding the comment.", null);
            }
        }

        /// <summary>
        /// Delete a comment from a group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> DeleteCommentAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.GroupPostComments
                    .Include(c => c.Post)
                    .Include(c => c.Replies)
                    .FirstOrDefaultAsync(c => c.Id == commentId);

                if (comment == null)
                {
                    return (false, "Comment not found.");
                }

                // Check if user is the author or a group admin/moderator
                var isAuthor = comment.AuthorId == userId;
                var isAdminOrModerator = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == comment.Post.GroupId && 
                                   gu.UserId == userId && 
                                   (gu.IsAdmin || gu.IsModerator));

                if (!isAuthor && !isAdminOrModerator)
                {
                    return (false, "You are not authorized to delete this comment.");
                }

                var post = comment.Post;
                var commentCount = 1 + comment.Replies.Count;

                context.GroupPostComments.Remove(comment);

                // Update post comment count
                post.CommentCount -= commentCount;

                await context.SaveChangesAsync();

                _logger.LogInformation("Group post comment {CommentId} deleted by user {UserId}", commentId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group post comment {CommentId} for user {UserId}", commentId, userId);
                return (false, "An error occurred while deleting the comment.");
            }
        }

        /// <summary>
        /// Get comments for a group post
        /// </summary>
        public async Task<List<GroupPostComment>> GetCommentsAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comments = await context.GroupPostComments
                    .Include(c => c.Author)
                    .Include(c => c.Replies)
                        .ThenInclude(r => r.Author)
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();

                return comments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comments for group post {PostId}", postId);
                return new List<GroupPostComment>();
            }
        }

        #endregion

        #region Like Operations

        /// <summary>
        /// Add or update a like on a group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> LikePostAsync(int postId, string userId, LikeType type = LikeType.Like)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify post exists
                var post = await context.GroupPosts
                    .Include(p => p.Author)
                    .FirstOrDefaultAsync(p => p.Id == postId);
                if (post == null)
                {
                    return (false, "Post not found.");
                }

                // Verify user is a member of the group
                var isMember = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == post.GroupId && gu.UserId == userId);

                if (!isMember)
                {
                    return (false, "You must be a member of the group to like posts.");
                }

                // Check if user is banned
                var isBanned = await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == post.GroupId && gbm.UserId == userId);

                if (isBanned)
                {
                    return (false, "You are banned from this group.");
                }

                // Check if like already exists
                var existingLike = await context.GroupPostLikes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

                if (existingLike != null)
                {
                    // Update existing like type
                    existingLike.Type = type;
                }
                else
                {
                    // Create new like
                    var like = new GroupPostLike
                    {
                        PostId = postId,
                        UserId = userId,
                        Type = type,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.GroupPostLikes.Add(like);

                    // Update post like count
                    post.LikeCount++;
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} liked group post {PostId}", userId, postId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error liking group post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while liking the post.");
            }
        }

        /// <summary>
        /// Remove a like from a group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> UnlikePostAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var like = await context.GroupPostLikes
                    .Include(l => l.Post)
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

                if (like == null)
                {
                    return (false, "Like not found.");
                }

                var post = like.Post;
                context.GroupPostLikes.Remove(like);

                // Update post like count
                post.LikeCount--;

                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} unliked group post {PostId}", userId, postId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unliking group post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while unliking the post.");
            }
        }

        /// <summary>
        /// Check if a user has liked a group post
        /// </summary>
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
                _logger.LogError(ex, "Error checking like for group post {PostId} and user {UserId}", postId, userId);
                return null;
            }
        }

        #endregion

        #region Comment Like Operations

        /// <summary>
        /// Add or update a like on a group post comment
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> LikeCommentAsync(int commentId, string userId, LikeType type = LikeType.Like)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify comment exists
                var comment = await context.GroupPostComments
                    .Include(c => c.Post)
                    .FirstOrDefaultAsync(c => c.Id == commentId);
                if (comment == null)
                {
                    return (false, "Comment not found.");
                }

                // Verify user is a member of the group
                var isMember = await context.GroupUsers
                    .AnyAsync(gu => gu.GroupId == comment.Post.GroupId && gu.UserId == userId);

                if (!isMember)
                {
                    return (false, "You must be a member of the group to like comments.");
                }

                // Check if user is banned
                var isBanned = await context.GroupBannedMembers
                    .AnyAsync(gbm => gbm.GroupId == comment.Post.GroupId && gbm.UserId == userId);

                if (isBanned)
                {
                    return (false, "You are banned from this group.");
                }

                // Check if like already exists
                var existingLike = await context.GroupPostCommentLikes
                    .FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);

                if (existingLike != null)
                {
                    // Update existing like type
                    existingLike.Type = type;
                }
                else
                {
                    // Create new like
                    var like = new GroupPostCommentLike
                    {
                        CommentId = commentId,
                        UserId = userId,
                        Type = type,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.GroupPostCommentLikes.Add(like);
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} liked group post comment {CommentId}", userId, commentId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error liking group post comment {CommentId} for user {UserId}", commentId, userId);
                return (false, "An error occurred while liking the comment.");
            }
        }

        /// <summary>
        /// Remove a like from a group post comment
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> UnlikeCommentAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var like = await context.GroupPostCommentLikes
                    .FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);

                if (like == null)
                {
                    return (false, "Like not found.");
                }

                context.GroupPostCommentLikes.Remove(like);
                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} unliked group post comment {CommentId}", userId, commentId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unliking group post comment {CommentId} for user {UserId}", commentId, userId);
                return (false, "An error occurred while unliking the comment.");
            }
        }

        /// <summary>
        /// Check if a user has liked a group post comment
        /// </summary>
        public async Task<GroupPostCommentLike?> GetUserCommentLikeAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostCommentLikes
                    .FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking like for group post comment {CommentId} and user {UserId}", commentId, userId);
                return null;
            }
        }

        #endregion

        #region Notification Mute Operations

        /// <summary>
        /// Mute notifications for a specific group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> MutePostNotificationsAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify post exists
                var post = await context.GroupPosts.FindAsync(postId);
                if (post == null)
                {
                    return (false, "Post not found.");
                }

                // Check if already muted
                var alreadyMuted = await context.GroupPostNotificationMutes
                    .AnyAsync(m => m.PostId == postId && m.UserId == userId);

                if (alreadyMuted)
                {
                    return (true, null);
                }

                // Create mute entry
                var mute = new GroupPostNotificationMute
                {
                    PostId = postId,
                    UserId = userId,
                    MutedAt = DateTime.UtcNow
                };

                context.GroupPostNotificationMutes.Add(mute);
                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} muted notifications for group post {PostId}", userId, postId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error muting notifications for group post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while muting notifications.");
            }
        }

        /// <summary>
        /// Unmute notifications for a specific group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> UnmutePostNotificationsAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var mute = await context.GroupPostNotificationMutes
                    .FirstOrDefaultAsync(m => m.PostId == postId && m.UserId == userId);

                if (mute == null)
                {
                    return (true, null);
                }

                context.GroupPostNotificationMutes.Remove(mute);
                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} unmuted notifications for group post {PostId}", userId, postId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unmuting notifications for group post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while unmuting notifications.");
            }
        }

        /// <summary>
        /// Check if a user has muted notifications for a specific group post
        /// </summary>
        public async Task<bool> IsPostNotificationMutedAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPostNotificationMutes
                    .AnyAsync(m => m.PostId == postId && m.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if group post {PostId} notifications are muted for user {UserId}", postId, userId);
                return false;
            }
        }

        #endregion
    }
}
