using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    public class GroupPostService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IGroupPostRepository<GroupPost, GroupPostImage> _postRepository;
        private readonly IGroupCommentRepository _commentRepository;
        private readonly IGroupPostLikeRepository _likeRepository;
        private readonly IGroupCommentLikeRepository _commentLikeRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly ILogger<GroupPostService> _logger;
        private readonly NotificationService _notificationService;
        private readonly UserPreferenceService _userPreferenceService;
        private readonly IWebHostEnvironment _environment;
        private readonly PostNotificationHelper _notificationHelper;
        private readonly GroupAccessValidator _accessValidator;

        public GroupPostService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IGroupPostRepository<GroupPost, GroupPostImage> postRepository,
            IGroupCommentRepository commentRepository,
            IGroupPostLikeRepository likeRepository,
            IGroupCommentLikeRepository commentLikeRepository,
            IGroupRepository groupRepository,
            INotificationRepository notificationRepository,
            ILogger<GroupPostService> logger,
            NotificationService notificationService,
            UserPreferenceService userPreferenceService,
            IWebHostEnvironment environment,
            PostNotificationHelper notificationHelper,
            GroupAccessValidator accessValidator)
        {
            _contextFactory = contextFactory;
            _postRepository = postRepository;
            _commentRepository = commentRepository;
            _likeRepository = likeRepository;
            _commentLikeRepository = commentLikeRepository;
            _groupRepository = groupRepository;
            _notificationRepository = notificationRepository;
            _logger = logger;
            _notificationService = notificationService;
            _userPreferenceService = userPreferenceService;
            _environment = environment;
            _notificationHelper = notificationHelper;
            _accessValidator = accessValidator;
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

            // Validate user can post in this group
            var (canPost, postError) = await _accessValidator.ValidateUserCanPostAsync(groupId, authorId);
            if (!canPost)
            {
                return (false, postError, null);
            }

            try
            {
                var post = new GroupPost
                {
                    GroupId = groupId,
                    AuthorId = authorId,
                    Content = string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _postRepository.CreateAsync(post);
                if (!result.Success || result.Post == null)
                {
                    return (false, result.ErrorMessage ?? "Failed to create post.", null);
                }

                // Add images if provided
                if (imageUrls != null && imageUrls.Any())
                {
                    var imageResult = await _postRepository.AddImagesAsync(result.Post.Id, authorId, imageUrls);
                    if (!imageResult.Success)
                    {
                        _logger.LogWarning("Group post created but failed to add images: {ErrorMessage}", imageResult.ErrorMessage);
                    }
                }

                // Update group's last active timestamp
                await _groupRepository.UpdateLastActiveAsync(groupId);

                _logger.LogInformation("Group post created by user {AuthorId} in group {GroupId}: Post ID {PostId}", authorId, groupId, result.Post.Id);
                return (true, null, result.Post);
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

                // Delete all post notification mute records
                var mutedNotifications = await context.GroupPostNotificationMutes
                    .Where(m => m.PostId == postId)
                    .ToListAsync();

                if (mutedNotifications.Any())
                {
                    context.GroupPostNotificationMutes.RemoveRange(mutedNotifications);
                    _logger.LogInformation("Deleted {Count} notification mute record(s) for group post {PostId}", mutedNotifications.Count, postId);
                }

                // Delete all notifications related to this group post
                var relatedNotifications = await context.UserNotifications
                    .Where(n => n.Data != null && n.Data.Contains($"\"GroupPostId\":{postId}"))
                    .ToListAsync();

                if (relatedNotifications.Any())
                {
                    context.UserNotifications.RemoveRange(relatedNotifications);
                    _logger.LogInformation("Deleted {Count} notification(s) related to group post {PostId}", relatedNotifications.Count, postId);
                }

                // Load comments for the post to delete comment likes
                var comments = await context.GroupPostComments
                    .Where(c => c.PostId == postId)
                    .ToListAsync();

                // Delete all comment likes
                var commentIds = comments.Select(c => c.Id).ToList();
                if (commentIds.Any())
                {
                    var commentLikes = await context.GroupPostCommentLikes
                        .Where(cl => commentIds.Contains(cl.CommentId))
                        .ToListAsync();

                    if (commentLikes.Any())
                    {
                        context.GroupPostCommentLikes.RemoveRange(commentLikes);
                        _logger.LogInformation("Deleted {Count} comment like(s) for group post {PostId}", commentLikes.Count, postId);
                    }
                }

                // Delete all comments (including replies)
                if (comments.Any())
                {
                    context.GroupPostComments.RemoveRange(comments);
                    _logger.LogInformation("Deleted {Count} comment(s) for group post {PostId}", comments.Count, postId);
                }

                // Delete all post likes
                var postLikes = await context.GroupPostLikes
                    .Where(l => l.PostId == postId)
                    .ToListAsync();

                if (postLikes.Any())
                {
                    context.GroupPostLikes.RemoveRange(postLikes);
                    _logger.LogInformation("Deleted {Count} like(s) for group post {PostId}", postLikes.Count, postId);
                }

                // Delete all post images and their cached thumbnails
                if (post.Images.Any())
                {
                    var cacheBasePath = Path.Combine(_environment.ContentRootPath, "AppData", "cache", "resized-images");
                    var deletedImageFiles = 0;
                    var deletedThumbnails = 0;

                    foreach (var postImage in post.Images)
                    {
                        // Extract the file path from the ImageUrl
                        // ImageUrl format: /api/secure-files/group-post-image/{groupId}/{postId}/{imageId}/{filename}
                        var urlParts = postImage.ImageUrl.Split('/');
                        if (urlParts.Length >= 3)
                        {
                            var filename = urlParts[^1];

                            // Delete the original image file
                            var originalImagePath = Path.Combine(
                                _environment.ContentRootPath,
                                "AppData",
                                "uploads",
                                "groups",
                                post.GroupId.ToString(),
                                "posts",
                                postId.ToString(),
                                "images",
                                filename
                            );

                            if (File.Exists(originalImagePath))
                            {
                                try
                                {
                                    File.Delete(originalImagePath);
                                    deletedImageFiles++;

                                    // Delete cached thumbnails (thumbnail and medium sizes)
                                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
                                    var thumbnailPath = Path.Combine(cacheBasePath, $"{fileNameWithoutExtension}_thumbnail.jpg");
                                    var mediumPath = Path.Combine(cacheBasePath, $"{fileNameWithoutExtension}_medium.jpg");

                                    if (File.Exists(thumbnailPath))
                                    {
                                        File.Delete(thumbnailPath);
                                        deletedThumbnails++;
                                    }

                                    if (File.Exists(mediumPath))
                                    {
                                        File.Delete(mediumPath);
                                        deletedThumbnails++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to delete image file: {FilePath}", originalImagePath);
                                }
                            }
                        }
                    }

                    // Remove database records
                    context.GroupPostImages.RemoveRange(post.Images);
                    _logger.LogInformation("Deleted {Count} image record(s), {FileCount} original file(s), and {ThumbCount} cached thumbnail(s) for group post {PostId}",
                        post.Images.Count, deletedImageFiles, deletedThumbnails, postId);
                }

                // Finally, delete the post itself
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
            return await _postRepository.GetByIdAsync(postId);
        }

        /// <summary>
        /// Get all posts for a specific group
        /// </summary>
        public async Task<List<GroupPost>> GetGroupPostsAsync(int groupId, int skip = 0, int take = 20)
        {
            return await _postRepository.GetByGroupAsync(groupId, skip, take);
        }

        /// <summary>
        /// Get posts by a specific user in a group
        /// </summary>
        public async Task<List<GroupPost>> GetUserGroupPostsAsync(int groupId, string userId, int skip = 0, int take = 20)
        {
            return await _postRepository.GetByGroupAndAuthorAsync(groupId, userId, skip, take);
        }

        /// <summary>
        /// Get the total count of posts in a group
        /// </summary>
        public async Task<int> GetGroupPostCountAsync(int groupId)
        {
            return await _postRepository.GetCountByGroupAsync(groupId);
        }

        /// <summary>
        /// Get all posts from groups the user is a member of (combined feed)
        /// </summary>
        public async Task<List<GroupPost>> GetAllGroupPostsForUserAsync(string userId, int skip = 0, int take = 20)
        {
            return await _postRepository.GetAllGroupPostsForUserAsync(userId, skip, take);
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

                // Validate user can act in this group
                var (canAct, actError) = await _accessValidator.ValidateUserCanActAsync(post.GroupId, authorId);
                if (!canAct)
                {
                    return (false, actError, null);
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

                // Send notifications using helper
                if (parentCommentId.HasValue && parentComment != null)
                {
                    // Reply to a comment - notify the parent comment author
                    await _notificationHelper.NotifyCommentReplyAsync(
                        parentComment.AuthorId,
                        authorId,
                        postId,
                        comment.Id,
                        NotificationType.GroupCommentReply,
                        groupId: post.GroupId
                    );
                }
                else
                {
                    // Direct comment on post - notify the post author
                    await _notificationHelper.NotifyPostCommentAsync(
                        post.AuthorId,
                        authorId,
                        postId,
                        comment.Id,
                        NotificationType.GroupPostComment,
                        groupId: post.GroupId
                    );
                }

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
        /// Get top-level comments for a group post (replies loaded separately via GetRepliesAsync)
        /// </summary>
        public async Task<List<GroupPostComment>> GetCommentsAsync(int postId)
        {
            return await _commentRepository.GetTopLevelCommentsAsync(postId);
        }

        /// <summary>
        /// Get a comment by its ID
        /// </summary>
        public async Task<GroupPostComment?> GetCommentByIdAsync(int commentId)
        {
            return await _commentRepository.GetByIdAsync(commentId, includeAuthor: true, includeReplies: false);
        }

        /// <summary>
        /// Get the last N direct comments for a group post (for feed display)
        /// </summary>
        public async Task<List<GroupPostComment>> GetLastCommentsAsync(int postId, int count)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comments = await context.GroupPostComments
                    .Include(c => c.Author)
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(count)
                    .ToListAsync();

                // Reverse to show oldest first (ascending order)
                comments.Reverse();

                return comments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving last comments for group post {PostId}", postId);
                return new List<GroupPostComment>();
            }
        }

        #endregion

        #region Like Operations

        /// <summary>
        /// Add or update a reaction on a group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> AddOrUpdateReactionAsync(int postId, string userId, LikeType type = LikeType.Like)
        {
            try
            {
                // Verify post exists
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null)
                {
                    return (false, "Post not found.");
                }

                // Validate user can act in this group
                var (canAct, actError) = await _accessValidator.ValidateUserCanActAsync(post.GroupId, userId);
                if (!canAct)
                {
                    return (false, actError);
                }

                // Check if like already exists
                var existingLike = await _likeRepository.GetUserLikeAsync(postId, userId);
                bool isNewReaction = existingLike == null;

                // Add or update the reaction
                var result = await _likeRepository.AddOrUpdateAsync(postId, userId, type);
                if (!result.Success)
                {
                    return (false, result.ErrorMessage);
                }

                // Update post like count for new reactions
                if (isNewReaction)
                {
                    await _postRepository.IncrementLikeCountAsync(postId);

                    // Send notification for new reactions only
                    await _notificationHelper.NotifyPostReactionAsync(
                        post.AuthorId,
                        userId,
                        postId,
                        type,
                        NotificationType.GroupPostLiked,
                        groupId: post.GroupId,
                        checkMute: false
                    );
                }

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
        /// Remove a reaction from a group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> RemoveReactionAsync(int postId, string userId)
        {
            try
            {
                // Check if like exists before removing
                var existingLike = await _likeRepository.GetUserLikeAsync(postId, userId);
                if (existingLike == null)
                {
                    return (false, "Like not found.");
                }

                var result = await _likeRepository.RemoveAsync(postId, userId);
                if (!result.Success)
                {
                    return (false, result.ErrorMessage);
                }

                // Update post like count
                await _postRepository.DecrementLikeCountAsync(postId);

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
            return await _likeRepository.GetUserLikeAsync(postId, userId);
        }

        #endregion

        #region Comment Like Operations

        /// <summary>
        /// Add or update a reaction on a group post comment
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> AddOrUpdateCommentReactionAsync(int commentId, string userId, LikeType type = LikeType.Like)
        {
            try
            {
                // Verify comment exists and get post info
                var comment = await _commentRepository.GetByIdAsync(commentId, includeAuthor: true, includeReplies: false);
                if (comment == null)
                {
                    return (false, "Comment not found.");
                }

                // Get post for group ID
                var post = await _postRepository.GetByIdAsync(comment.PostId);
                if (post == null)
                {
                    return (false, "Post not found.");
                }

                // Validate user can act in this group
                var (canAct, actError) = await _accessValidator.ValidateUserCanActAsync(post.GroupId, userId);
                if (!canAct)
                {
                    return (false, actError);
                }

                // Check if like already exists
                var existingLike = await _commentLikeRepository.GetUserLikeAsync(commentId, userId);
                bool isNewReaction = existingLike == null;

                // Add or update the reaction
                var result = await _commentLikeRepository.AddOrUpdateAsync(commentId, userId, type);
                if (!result.Success)
                {
                    return (false, result.ErrorMessage);
                }

                // Send notification for new reactions only
                if (isNewReaction)
                {
                    await _notificationHelper.NotifyCommentReactionAsync(
                        comment.AuthorId,
                        userId,
                        comment.PostId,
                        commentId,
                        type,
                        NotificationType.GroupCommentLiked,
                        groupId: post.GroupId,
                        checkMute: false
                    );
                }

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
        /// Remove a reaction from a group post comment
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> RemoveCommentReactionAsync(int commentId, string userId)
        {
            try
            {
                // Check if like exists before removing
                var existingLike = await _commentLikeRepository.GetUserLikeAsync(commentId, userId);
                if (existingLike == null)
                {
                    return (false, "Like not found.");
                }

                var result = await _commentLikeRepository.RemoveAsync(commentId, userId);
                if (!result.Success)
                {
                    return (false, result.ErrorMessage);
                }

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
            return await _commentLikeRepository.GetUserLikeAsync(commentId, userId);
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

        #region Like Details Operations

        /// <summary>
        /// Get detailed likes for a group post including user info and reaction type
        /// </summary>
        public async Task<List<(ApplicationUser User, LikeType Type, DateTime CreatedAt)>> GetGroupPostLikesWithDetailsAsync(int postId, int maxCount = 100)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var likes = await context.GroupPostLikes
                    .Include(l => l.User)
                    .Where(l => l.PostId == postId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(maxCount)
                    .Select(l => new { l.User, l.Type, l.CreatedAt })
                    .ToListAsync();

                return likes.Select(l => (l.User, l.Type, l.CreatedAt)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed likes for group post {PostId}", postId);
                return new List<(ApplicationUser, LikeType, DateTime)>();
            }
        }

        /// <summary>
        /// Get the breakdown of reaction types for a group post
        /// </summary>
        public async Task<Dictionary<LikeType, int>> GetReactionBreakdownAsync(int postId)
        {
            return await _likeRepository.GetCountsByTypeAsync(postId);
        }

        /// <summary>
        /// Get user's reaction type for a group post (null if not reacted)
        /// </summary>
        public async Task<LikeType?> GetUserReactionAsync(int postId, string userId)
        {
            var like = await _likeRepository.GetUserLikeAsync(postId, userId);
            return like?.Type;
        }

        #endregion

        #region Comment Retrieval Operations

        /// <summary>
        /// Get paginated direct comments for a group post (no nested replies)
        /// </summary>
        public async Task<List<GroupPostComment>> GetCommentsPagedAsync(int postId, int pageSize, int pageNumber)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comments = await context.GroupPostComments
                    .Include(c => c.Author)
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .OrderBy(c => c.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return comments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged comments for group post {PostId}", postId);
                return new List<GroupPostComment>();
            }
        }

        /// <summary>
        /// Get the total count of direct comments for a group post (excluding replies)
        /// </summary>
        public async Task<int> GetDirectCommentCountAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPostComments
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving direct comment count for group post {PostId}", postId);
                return 0;
            }
        }

        /// <summary>
        /// Get replies for a specific comment
        /// </summary>
        public async Task<List<GroupPostComment>> GetRepliesAsync(int commentId)
        {
            return await _commentRepository.GetRepliesAsync(commentId);
        }

        /// <summary>
        /// Get like count for a group post comment
        /// </summary>
        public async Task<int> GetCommentLikeCountAsync(int commentId)
        {
            return await _commentLikeRepository.GetCountAsync(commentId);
        }

        /// <summary>
        /// Get the breakdown of reaction types for a group post comment
        /// </summary>
        public async Task<Dictionary<LikeType, int>> GetCommentReactionBreakdownAsync(int commentId)
        {
            return await _commentLikeRepository.GetCountsByTypeAsync(commentId);
        }

        /// <summary>
        /// Get user's reaction type for a group post comment (null if not reacted)
        /// </summary>
        public async Task<LikeType?> GetUserCommentReactionAsync(int commentId, string userId)
        {
            var like = await _commentLikeRepository.GetUserLikeAsync(commentId, userId);
            return like?.Type;
        }

        #endregion

        #region Post Image Operations

        /// <summary>
        /// Add an image to a group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, GroupPostImage? PostImage)> AddImageToPostAsync(int postId, string imageUrl, string userId)
        {
            try
            {
                var post = await _postRepository.GetByIdAsync(postId, includeAuthor: false, includeImages: false);
                if (post == null)
                {
                    return (false, "Post not found.", null);
                }

                // Validate user can modify this post (must be author)
                if (post.AuthorId != userId)
                {
                    return (false, "You are not authorized to add images to this post.", null);
                }

                // Validate user can still act in this group
                var (canAct, actError) = await _accessValidator.ValidateUserCanActAsync(post.GroupId, userId);
                if (!canAct)
                {
                    return (false, actError, null);
                }

                // Use repository to add image
                var result = await _postRepository.AddImagesAsync(postId, userId, [imageUrl]);
                if (!result.Success || result.Images == null || !result.Images.Any())
                {
                    return (false, result.ErrorMessage ?? "Failed to add image.", null);
                }

                _logger.LogInformation("Image added to group post {PostId} by user {UserId}", postId, userId);
                return (true, null, result.Images.First());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding image to group post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while adding the image.", null);
            }
        }

        /// <summary>
        /// Remove an image from a group post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> RemoveImageFromPostAsync(int imageId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var postImage = await context.GroupPostImages
                    .Include(pi => pi.Post)
                    .FirstOrDefaultAsync(pi => pi.Id == imageId);

                if (postImage == null)
                {
                    return (false, "Image not found.");
                }

                // Check if user is author or group admin/moderator
                var isAuthor = postImage.Post.AuthorId == userId;
                var isAdminOrMod = await _accessValidator.IsGroupAdminOrModeratorAsync(postImage.Post.GroupId, userId);

                if (!isAuthor && !isAdminOrMod)
                {
                    return (false, "You are not authorized to remove this image.");
                }

                // Validate user can still act in this group
                var (canAct, actError) = await _accessValidator.ValidateUserCanActAsync(postImage.Post.GroupId, userId);
                if (!canAct)
                {
                    return (false, actError);
                }

                context.GroupPostImages.Remove(postImage);
                await context.SaveChangesAsync();

                _logger.LogInformation("Image {ImageId} removed from group post {PostId} by user {UserId}", imageId, postImage.PostId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing image {ImageId} for user {UserId}", imageId, userId);
                return (false, "An error occurred while removing the image.");
            }
        }

        /// <summary>
        /// Get images for a group post
        /// </summary>
        public async Task<List<GroupPostImage>> GetPostImagesAsync(int postId)
        {
            return await _postRepository.GetImagesAsync(postId);
        }

        #endregion

        #region Group Statistics

        /// <summary>
        /// Get total number of posts in a group
        /// </summary>
        public async Task<int> GetTotalPostCountAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPosts.CountAsync(p => p.GroupId == groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total post count for group {GroupId}", groupId);
                return 0;
            }
        }

        /// <summary>
        /// Get number of posts in a group within a time period
        /// </summary>
        public async Task<int> GetPostCountSinceAsync(int groupId, DateTime since)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPosts
                    .CountAsync(p => p.GroupId == groupId && p.CreatedAt >= since);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting post count since {Since} for group {GroupId}", since, groupId);
                return 0;
            }
        }

        /// <summary>
        /// Get the last activity timestamp for a group (most recent post)
        /// </summary>
        public async Task<DateTime?> GetLastActivityAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var lastPost = await context.GroupPosts
                    .Where(p => p.GroupId == groupId)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                return lastPost?.CreatedAt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last activity for group {GroupId}", groupId);
                return null;
            }
        }

        #endregion
    }
}
