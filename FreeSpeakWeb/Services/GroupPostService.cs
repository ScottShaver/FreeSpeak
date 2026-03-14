using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service providing business logic for managing group posts, comments, likes, reactions, and related operations.
    /// Handles post CRUD, comment management, reaction tracking, notification muting, and group statistics.
    /// </summary>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupPostService"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="postRepository">Repository for group post operations.</param>
        /// <param name="commentRepository">Repository for group comment operations.</param>
        /// <param name="likeRepository">Repository for group post like operations.</param>
        /// <param name="commentLikeRepository">Repository for group comment like operations.</param>
        /// <param name="groupRepository">Repository for group operations.</param>
        /// <param name="notificationRepository">Repository for notification operations.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        /// <param name="notificationService">Service for sending notifications.</param>
        /// <param name="userPreferenceService">Service for user display preferences.</param>
        /// <param name="environment">Web hosting environment information.</param>
        /// <param name="notificationHelper">Helper for creating post-related notifications.</param>
        /// <param name="accessValidator">Validator for group access permissions.</param>
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
        /// Creates a new post in a group with optional images.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="authorId">The unique identifier of the post author.</param>
        /// <param name="content">The text content of the post (can be empty if images are provided).</param>
        /// <param name="imageUrls">Optional list of image URLs to attach to the post.</param>
        /// <returns>A tuple containing success status, error message if failed, and the created post if successful.</returns>
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
        /// Updates an existing group post's content and images.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to update.</param>
        /// <param name="userId">The user ID of the person making the update (must be the author).</param>
        /// <param name="newContent">The new text content for the post.</param>
        /// <param name="newImageUrls">Optional list of new image URLs to add.</param>
        /// <param name="removedImageIds">Optional list of image IDs to remove from the post.</param>
        /// <returns>A tuple containing success status, error message if failed, and the updated list of images.</returns>
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
        /// Deletes a group post and all related data including comments, likes, images, pinned posts, and notifications.
        /// Also removes associated image files and cached thumbnails from the file system.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to delete.</param>
        /// <param name="userId">The user ID of the person requesting deletion (must be author or group admin/moderator).</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Retrieves a specific group post by its unique identifier.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>The group post if found; otherwise, null.</returns>
        public async Task<GroupPost?> GetGroupPostByIdAsync(int postId)
        {
            return await _postRepository.GetByIdAsync(postId);
        }

        /// <summary>
        /// Retrieves all posts for a specific group with pagination.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of group posts.</returns>
        public async Task<List<GroupPost>> GetGroupPostsAsync(int groupId, int skip = 0, int take = 20)
        {
            return await _postRepository.GetByGroupAsync(groupId, skip, take);
        }

        /// <summary>
        /// Retrieves posts by a specific user in a group with pagination.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="userId">The unique identifier of the post author.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of group posts by the specified user.</returns>
        public async Task<List<GroupPost>> GetUserGroupPostsAsync(int groupId, string userId, int skip = 0, int take = 20)
        {
            return await _postRepository.GetByGroupAndAuthorAsync(groupId, userId, skip, take);
        }

        /// <summary>
        /// Gets the total count of posts in a group.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <returns>The number of posts in the group.</returns>
        public async Task<int> GetGroupPostCountAsync(int groupId)
        {
            return await _postRepository.GetCountByGroupAsync(groupId);
        }

        /// <summary>
        /// Retrieves all posts from groups the user is a member of (combined feed).
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of group posts from all groups the user belongs to.</returns>
        public async Task<List<GroupPost>> GetAllGroupPostsForUserAsync(string userId, int skip = 0, int take = 20)
        {
            return await _postRepository.GetAllGroupPostsForUserAsync(userId, skip, take);
        }

        #endregion

        #region Comment Operations

        /// <summary>
        /// Adds a comment to a group post, with optional image attachment and support for nested replies.
        /// Sends appropriate notifications to post author or parent comment author.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to comment on.</param>
        /// <param name="authorId">The unique identifier of the comment author.</param>
        /// <param name="content">The text content of the comment.</param>
        /// <param name="imageUrl">Optional URL of an image to attach to the comment.</param>
        /// <param name="parentCommentId">Optional ID of the parent comment if this is a reply.</param>
        /// <returns>A tuple containing success status, error message if failed, and the created comment if successful.</returns>
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
        /// Deletes a comment from a group post and all its replies, updating the post's comment count.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment to delete.</param>
        /// <param name="userId">The user ID of the person requesting deletion (must be author or group admin/moderator).</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Retrieves top-level comments for a group post (replies are loaded separately via GetRepliesAsync).
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of top-level comments.</returns>
        public async Task<List<GroupPostComment>> GetCommentsAsync(int postId)
        {
            return await _commentRepository.GetTopLevelCommentsAsync(postId);
        }

        /// <summary>
        /// Retrieves a comment by its unique identifier including author information.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>The comment if found; otherwise, null.</returns>
        public async Task<GroupPostComment?> GetCommentByIdAsync(int commentId)
        {
            return await _commentRepository.GetByIdAsync(commentId, includeAuthor: true, includeReplies: false);
        }

        /// <summary>
        /// Retrieves the last N direct comments for a group post for feed display, ordered oldest first.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="count">The number of recent comments to retrieve.</param>
        /// <returns>A list of comments ordered from oldest to newest.</returns>
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
        /// Adds a new reaction to a group post or updates an existing reaction type.
        /// Sends a notification to the post author for new reactions.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user reacting.</param>
        /// <param name="type">The type of reaction to add or update to.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Removes a user's reaction from a specific group post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user whose reaction to remove.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Gets a user's like record for a specific group post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The like record if found; otherwise, null.</returns>
        public async Task<GroupPostLike?> GetUserLikeAsync(int postId, string userId)
        {
            return await _likeRepository.GetUserLikeAsync(postId, userId);
        }

        #endregion

        #region Comment Like Operations

        /// <summary>
        /// Adds a new reaction to a group post comment or updates an existing reaction type.
        /// Sends a notification to the comment author for new reactions.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The unique identifier of the user reacting.</param>
        /// <param name="type">The type of reaction to add or update to.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Removes a user's reaction from a specific group post comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The unique identifier of the user whose reaction to remove.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Gets a user's like record for a specific group post comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The like record if found; otherwise, null.</returns>
        public async Task<GroupPostCommentLike?> GetUserCommentLikeAsync(int commentId, string userId)
        {
            return await _commentLikeRepository.GetUserLikeAsync(commentId, userId);
        }

        #endregion

        #region Notification Mute Operations

        /// <summary>
        /// Mutes notifications for a specific group post for a user.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user muting notifications.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Unmutes notifications for a specific group post for a user.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user unmuting notifications.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Checks if a user has muted notifications for a specific group post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if notifications are muted for the post; otherwise, false.</returns>
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

        /// <summary>
        /// Gets the mute status for multiple group posts for a specific user in a single query.
        /// Used to batch load mute status and avoid N+1 queries when displaying group feeds.
        /// </summary>
        /// <param name="postIds">List of post IDs to check mute status for.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A dictionary mapping post IDs to their mute status (true if muted).</returns>
        public async Task<Dictionary<int, bool>> GetMuteStatusForPostsAsync(List<int> postIds, string userId)
        {
            if (postIds == null || !postIds.Any() || string.IsNullOrEmpty(userId))
            {
                return new Dictionary<int, bool>();
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var mutedPostIds = await context.GroupPostNotificationMutes
                    .Where(m => postIds.Contains(m.PostId) && m.UserId == userId)
                    .Select(m => m.PostId)
                    .ToListAsync();

                return postIds.ToDictionary(id => id, id => mutedPostIds.Contains(id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mute status for group posts for user {UserId}", userId);
                return postIds.ToDictionary(id => id, id => false);
            }
        }

        #endregion

        #region Like Details Operations

        /// <summary>
        /// Retrieves detailed like information for a group post including user info, reaction type, and timestamp.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="maxCount">Maximum number of likes to retrieve.</param>
        /// <returns>A list of tuples containing user, reaction type, and creation timestamp.</returns>
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
        /// Gets the breakdown of reaction types and their counts for a specific group post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A dictionary mapping reaction types to their counts.</returns>
        public async Task<Dictionary<LikeType, int>> GetReactionBreakdownAsync(int postId)
        {
            return await _likeRepository.GetCountsByTypeAsync(postId);
        }

        /// <summary>
        /// Gets a user's reaction type for a specific group post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The user's reaction type if they have reacted; otherwise, null.</returns>
        public async Task<LikeType?> GetUserReactionAsync(int postId, string userId)
        {
            var like = await _likeRepository.GetUserLikeAsync(postId, userId);
            return like?.Type;
        }

        /// <summary>
        /// Gets reaction breakdowns for multiple group posts in a single database query.
        /// </summary>
        /// <param name="postIds">The list of post identifiers to query.</param>
        /// <returns>A dictionary mapping post IDs to dictionaries of like types and their counts.</returns>
        public async Task<Dictionary<int, Dictionary<LikeType, int>>> GetReactionBreakdownForPostsAsync(List<int> postIds)
        {
            return await _likeRepository.GetCountsByTypeForPostsAsync(postIds);
        }

        /// <summary>
        /// Gets user reactions for multiple group posts in a single database query.
        /// </summary>
        /// <param name="postIds">The list of post identifiers to query.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A dictionary mapping post IDs to the user's reaction type (or null if no reaction).</returns>
        public async Task<Dictionary<int, LikeType?>> GetUserReactionsForPostsAsync(List<int> postIds, string userId)
        {
            return await _likeRepository.GetUserReactionsForPostsAsync(postIds, userId);
        }

        #endregion

        #region Comment Retrieval Operations

        /// <summary>
        /// Retrieves paginated direct comments for a group post (excluding nested replies).
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="pageSize">Number of comments per page.</param>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <returns>A list of comments for the specified page.</returns>
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
        /// Gets the total count of direct comments for a group post (excluding nested replies).
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>The count of direct comments.</returns>
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
        /// Retrieves replies for a specific group post comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the parent comment.</param>
        /// <returns>A list of reply comments.</returns>
        public async Task<List<GroupPostComment>> GetRepliesAsync(int commentId)
        {
            return await _commentRepository.GetRepliesAsync(commentId);
        }

        /// <summary>
        /// Gets the total like count for a specific group post comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>The number of likes on the comment.</returns>
        public async Task<int> GetCommentLikeCountAsync(int commentId)
        {
            return await _commentLikeRepository.GetCountAsync(commentId);
        }

        /// <summary>
        /// Gets the breakdown of reaction types and their counts for a specific group post comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>A dictionary mapping reaction types to their counts.</returns>
        public async Task<Dictionary<LikeType, int>> GetCommentReactionBreakdownAsync(int commentId)
        {
            return await _commentLikeRepository.GetCountsByTypeAsync(commentId);
        }

        /// <summary>
        /// Gets a user's reaction type for a specific group post comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The user's reaction type if they have reacted; otherwise, null.</returns>
        public async Task<LikeType?> GetUserCommentReactionAsync(int commentId, string userId)
        {
            var like = await _commentLikeRepository.GetUserLikeAsync(commentId, userId);
            return like?.Type;
        }

        /// <summary>
        /// Gets multiple comments by their IDs in a single query.
        /// This batch method reduces database round trips when loading comment trees.
        /// </summary>
        /// <param name="commentIds">Collection of comment IDs to retrieve.</param>
        /// <returns>A list of comments matching the provided IDs.</returns>
        public async Task<List<GroupPostComment>> GetCommentsByIdsAsync(IEnumerable<int> commentIds)
        {
            return await _commentRepository.GetByIdsAsync(commentIds, includeAuthor: true);
        }

        /// <summary>
        /// Gets all comments for a group post including top-level comments and all nested replies in a single query.
        /// Optimized method that eliminates N+1 query problems when loading comment trees.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of all comments for the post with author information.</returns>
        public async Task<List<GroupPostComment>> GetAllCommentsAsync(int postId)
        {
            return await _commentRepository.GetAllCommentsAsync(postId);
        }

        /// <summary>
        /// Gets the like counts for multiple comments in a single query.
        /// This batch method reduces database round trips when loading comment lists.
        /// </summary>
        /// <param name="commentIds">Collection of comment IDs to get counts for.</param>
        /// <returns>A dictionary mapping each comment ID to its like count.</returns>
        public async Task<Dictionary<int, int>> GetCommentLikeCountsAsync(IEnumerable<int> commentIds)
        {
            return await _commentLikeRepository.GetCountsForCommentsAsync(commentIds);
        }

        /// <summary>
        /// Gets the user reactions for multiple comments in a single query.
        /// This batch method reduces database round trips when loading comment lists.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="commentIds">Collection of comment IDs to check reactions for.</param>
        /// <returns>A dictionary mapping each comment ID to the user's reaction type (or null).</returns>
        public async Task<Dictionary<int, LikeType?>> GetUserCommentReactionsAsync(string userId, IEnumerable<int> commentIds)
        {
            var likes = await _commentLikeRepository.GetUserLikesForCommentsAsync(userId, commentIds);
            return likes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Type);
        }

        /// <summary>
        /// Gets the reaction breakdowns for multiple comments in a single query.
        /// This batch method reduces database round trips when loading comment lists.
        /// </summary>
        /// <param name="commentIds">Collection of comment IDs to get reaction breakdowns for.</param>
        /// <returns>A dictionary mapping each comment ID to its reaction breakdown.</returns>
        public async Task<Dictionary<int, Dictionary<LikeType, int>>> GetCommentReactionBreakdownsAsync(IEnumerable<int> commentIds)
        {
            return await _commentLikeRepository.GetReactionBreakdownsForCommentsAsync(commentIds);
        }

        #endregion

        #region Post Image Operations

        /// <summary>
        /// Adds an image to an existing group post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="imageUrl">The URL of the image to add.</param>
        /// <param name="userId">The user ID of the person adding the image (must be the post author).</param>
        /// <returns>A tuple containing success status, error message if failed, and the created post image if successful.</returns>
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
        /// Removes an image from a group post.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image to remove.</param>
        /// <param name="userId">The user ID of the person removing the image (must be author or group admin/moderator).</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
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
        /// Retrieves all images for a specific group post ordered by display order.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of post images in display order.</returns>
        public async Task<List<GroupPostImage>> GetPostImagesAsync(int postId)
        {
            return await _postRepository.GetImagesAsync(postId);
        }

        #endregion

        #region Group Statistics

        /// <summary>
        /// Gets the total number of posts in a group.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <returns>The total count of posts.</returns>
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
        /// Gets the number of posts in a group created since a specific date.
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <param name="since">The start date for counting posts.</param>
        /// <returns>The count of posts created since the specified date.</returns>
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
        /// Gets the last activity timestamp for a group (most recent post creation date).
        /// </summary>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <returns>The creation date of the most recent post, or null if no posts exist.</returns>
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
