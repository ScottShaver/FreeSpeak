using FreeSpeakWeb.Data;
using FreeSpeakWeb.Components.SocialFeed;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Mapping;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service providing business logic for managing feed posts, comments, likes, reactions, and related operations.
    /// Handles post CRUD operations, comment management, reaction tracking, post pinning, and notification muting.
    /// </summary>
    public class PostService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IFeedPostRepository<Post, PostImage> _postRepository;
        private readonly IFeedCommentRepository _commentRepository;
        private readonly IFeedPostLikeRepository _likeRepository;
        private readonly IFeedCommentLikeRepository _commentLikeRepository;
        private readonly IPinnedPostRepository _pinnedPostRepository;
        private readonly IPostNotificationMuteRepository _muteRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly ILogger<PostService> _logger;
        private readonly SiteSettings _siteSettings;
        private readonly IWebHostEnvironment _environment;
        private readonly NotificationService _notificationService;
        private readonly UserPreferenceService _userPreferenceService;
        private readonly PostNotificationHelper _notificationHelper;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostService"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="postRepository">Repository for post operations.</param>
        /// <param name="commentRepository">Repository for comment operations.</param>
        /// <param name="likeRepository">Repository for post like operations.</param>
        /// <param name="commentLikeRepository">Repository for comment like operations.</param>
        /// <param name="pinnedPostRepository">Repository for pinned post operations.</param>
        /// <param name="muteRepository">Repository for post notification mute operations.</param>
        /// <param name="notificationRepository">Repository for notification operations.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        /// <param name="siteSettings">Site configuration settings.</param>
        /// <param name="environment">Web hosting environment information.</param>
        /// <param name="notificationService">Service for sending notifications.</param>
        /// <param name="userPreferenceService">Service for user display preferences.</param>
        /// <param name="notificationHelper">Helper for creating post-related notifications.</param>
        public PostService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IFeedPostRepository<Post, PostImage> postRepository,
            IFeedCommentRepository commentRepository,
            IFeedPostLikeRepository likeRepository,
            IFeedCommentLikeRepository commentLikeRepository,
            IPinnedPostRepository pinnedPostRepository,
            IPostNotificationMuteRepository muteRepository,
            INotificationRepository notificationRepository,
            ILogger<PostService> logger, 
            IOptions<SiteSettings> siteSettings,
            IWebHostEnvironment environment,
            NotificationService notificationService,
            UserPreferenceService userPreferenceService,
            PostNotificationHelper notificationHelper)
        {
            _contextFactory = contextFactory;
            _postRepository = postRepository;
            _commentRepository = commentRepository;
            _likeRepository = likeRepository;
            _commentLikeRepository = commentLikeRepository;
            _pinnedPostRepository = pinnedPostRepository;
            _muteRepository = muteRepository;
            _notificationRepository = notificationRepository;
            _logger = logger;
            _siteSettings = siteSettings.Value;
            _environment = environment;
            _notificationService = notificationService;
            _userPreferenceService = userPreferenceService;
            _notificationHelper = notificationHelper;
        }

        #region Post Operations

        /// <summary>
        /// Creates a new post with optional images.
        /// </summary>
        /// <param name="authorId">The unique identifier of the post author.</param>
        /// <param name="content">The text content of the post (can be empty if images are provided).</param>
        /// <param name="audienceType">The visibility setting for the post.</param>
        /// <param name="imageUrls">Optional list of image URLs to attach to the post.</param>
        /// <returns>A tuple containing success status, error message if failed, and the created post if successful.</returns>
        public async Task<(bool Success, string? ErrorMessage, Post? Post)> CreatePostAsync(string authorId, string content, AudienceType audienceType = AudienceType.Public, List<string>? imageUrls = null)
        {
            // Allow empty content if images are provided
            if (string.IsNullOrWhiteSpace(content) && (imageUrls == null || !imageUrls.Any()))
            {
                return (false, "Post must contain either text or images.", null);
            }

            try
            {
                var post = new Post
                {
                    AuthorId = authorId,
                    Content = string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    AudienceType = audienceType
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
                        _logger.LogWarning("Post created but failed to add images: {ErrorMessage}", imageResult.ErrorMessage);
                    }
                }

                _logger.LogInformation("Post created by user {AuthorId}: Post ID {PostId}", authorId, result.Post.Id);
                return (true, null, result.Post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post for user {AuthorId}", authorId);
                return (false, "An error occurred while creating the post.", null);
            }
        }

        /// <summary>
        /// Updates an existing post's content and images.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to update.</param>
        /// <param name="userId">The user ID of the person making the update (must be the author).</param>
        /// <param name="newContent">The new text content for the post.</param>
        /// <param name="newImageUrls">Optional list of new image URLs to add.</param>
        /// <param name="removedImageIds">Optional list of image IDs to remove from the post.</param>
        /// <returns>A tuple containing success status, error message if failed, and the updated list of images.</returns>
        public async Task<(bool Success, string? ErrorMessage, List<PostImage>? UpdatedImages)> UpdatePostAsync(
            int postId, 
            string userId, 
            string newContent, 
            List<string>? newImageUrls = null, 
            List<int>? removedImageIds = null)
        {
            // Allow empty content if images exist or are being added
            var hasImages = newImageUrls != null && newImageUrls.Any();

            if (string.IsNullOrWhiteSpace(newContent) && !hasImages && (removedImageIds == null || !removedImageIds.Any()))
            {
                return (false, "Post must contain either text or images.", null);
            }

            try
            {
                // Update content
                var contentResult = await _postRepository.UpdateContentAsync(postId, userId, newContent);
                if (!contentResult.Success)
                {
                    return (false, contentResult.ErrorMessage, null);
                }

                // Remove specified images
                if (removedImageIds != null && removedImageIds.Any())
                {
                    var removeResult = await _postRepository.RemoveImagesAsync(postId, userId, removedImageIds);
                    if (!removeResult.Success)
                    {
                        _logger.LogWarning("Post updated but failed to remove images: {ErrorMessage}", removeResult.ErrorMessage);
                    }
                }

                // Add new images
                if (newImageUrls != null && newImageUrls.Any())
                {
                    var addResult = await _postRepository.AddImagesAsync(postId, userId, newImageUrls);
                    if (!addResult.Success)
                    {
                        _logger.LogWarning("Post updated but failed to add images: {ErrorMessage}", addResult.ErrorMessage);
                    }
                }

                // Get updated images
                var updatedImages = await _postRepository.GetImagesAsync(postId);

                // Verify post still has content or images
                var post = await _postRepository.GetByIdAsync(postId, includeAuthor: false, includeImages: false);
                if (post != null && string.IsNullOrWhiteSpace(post.Content) && !updatedImages.Any())
                {
                    return (false, "Post must contain either text or images.", null);
                }

                _logger.LogInformation("Post {PostId} updated by user {UserId}", postId, userId);
                return (true, null, updatedImages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while updating the post.", null);
            }
        }

        /// <summary>
        /// Updates the audience visibility type of an existing post.
        /// Also removes any pinned post records since the audience has changed.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to update.</param>
        /// <param name="userId">The user ID of the person making the update (must be the author).</param>
        /// <param name="newAudienceType">The new audience visibility setting.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> UpdatePostAudienceAsync(int postId, string userId, AudienceType newAudienceType)
        {
            try
            {
                var result = await _postRepository.UpdateAudienceAsync(postId, userId, newAudienceType);
                if (!result.Success)
                {
                    return result;
                }

                // Remove any pinned post records since the audience has changed
                await _pinnedPostRepository.RemovePinnedPostsByPostIdAsync(postId);

                _logger.LogInformation("Post {PostId} audience updated to {AudienceType} by user {UserId}", postId, newAudienceType, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post {PostId} audience for user {UserId}", postId, userId);
                return (false, "An error occurred while updating the post audience.");
            }
        }

        /// <summary>
        /// Deletes a post and all related data including comments, likes, images, pinned posts, notification mutes, and notifications.
        /// Also removes associated image files and cached thumbnails from the file system.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to delete.</param>
        /// <param name="userId">The user ID of the person requesting deletion (must be the author).</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> DeletePostAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Load the post with all related data
                var post = await context.Posts
                    .Include(p => p.Comments)
                        .ThenInclude(c => c.Replies)
                    .Include(p => p.Likes)
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                if (post == null)
                {
                    return (false, "Post not found.");
                }

                if (post.AuthorId != userId)
                {
                    return (false, "You are not authorized to delete this post.");
                }

                // Delete all pinned post records
                var pinnedPosts = await context.PinnedPosts
                    .Where(pp => pp.PostId == postId)
                    .ToListAsync();

                if (pinnedPosts.Any())
                {
                    context.PinnedPosts.RemoveRange(pinnedPosts);
                    _logger.LogInformation("Deleted {Count} pinned post record(s) for post {PostId}", pinnedPosts.Count, postId);
                }

                // Delete all post notification mute records
                var mutedNotifications = await context.PostNotificationMutes
                    .Where(m => m.PostId == postId)
                    .ToListAsync();

                if (mutedNotifications.Any())
                {
                    context.PostNotificationMutes.RemoveRange(mutedNotifications);
                    _logger.LogInformation("Deleted {Count} notification mute record(s) for post {PostId}", mutedNotifications.Count, postId);
                }

                // Delete all notifications related to this post (check Data field for PostId)
                var relatedNotifications = await context.UserNotifications
                    .Where(n => n.Data != null && n.Data.Contains($"\"PostId\":{postId}"))
                    .ToListAsync();

                if (relatedNotifications.Any())
                {
                    context.UserNotifications.RemoveRange(relatedNotifications);
                    _logger.LogInformation("Deleted {Count} notification(s) related to post {PostId}", relatedNotifications.Count, postId);
                }

                // Delete all comment likes first (cascade from comments)
                var commentIds = post.Comments.Select(c => c.Id).ToList();
                if (commentIds.Any())
                {
                    var commentLikes = await context.CommentLikes
                        .Where(cl => commentIds.Contains(cl.CommentId))
                        .ToListAsync();

                    if (commentLikes.Any())
                    {
                        context.CommentLikes.RemoveRange(commentLikes);
                        _logger.LogInformation("Deleted {Count} comment like(s) for post {PostId}", commentLikes.Count, postId);
                    }
                }

                // Delete all comments (including replies)
                if (post.Comments.Any())
                {
                    var allComments = post.Comments.ToList();
                    foreach (var comment in allComments)
                    {
                        if (comment.Replies != null && comment.Replies.Any())
                        {
                            context.Comments.RemoveRange(comment.Replies);
                        }
                    }
                    context.Comments.RemoveRange(allComments);
                    _logger.LogInformation("Deleted {Count} comment(s) and their replies for post {PostId}", allComments.Count, postId);
                }

                // Delete all post likes
                if (post.Likes.Any())
                {
                    context.Likes.RemoveRange(post.Likes);
                    _logger.LogInformation("Deleted {Count} like(s) for post {PostId}", post.Likes.Count, postId);
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
                        // ImageUrl format: /api/secure-files/post-image/{userId}/{imageId}/{filename}
                        var urlParts = postImage.ImageUrl.Split('/');
                        if (urlParts.Length >= 3)
                        {
                            var imageUserId = urlParts[^3];
                            var filename = urlParts[^1];

                            // Delete the original image file
                            var originalImagePath = Path.Combine(
                                _environment.ContentRootPath,
                                "AppData",
                                "uploads",
                                "posts",
                                imageUserId,
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
                    context.PostImages.RemoveRange(post.Images);
                    _logger.LogInformation("Deleted {Count} image record(s), {FileCount} original file(s), and {ThumbCount} cached thumbnail(s) for post {PostId}", 
                        post.Images.Count, deletedImageFiles, deletedThumbnails, postId);
                }

                // Finally, delete the post itself
                context.Posts.Remove(post);

                await context.SaveChangesAsync();

                _logger.LogInformation("Post {PostId} and all related data deleted by user {UserId}", postId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while deleting the post.");
            }
        }

        /// <summary>
        /// Retrieves a post by its unique identifier with all related data including author, images, and comments with their authors and replies.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>The post with related data if found; otherwise, null.</returns>
        public async Task<Post?> GetPostByIdAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Posts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Include(p => p.Comments.Where(c => c.ParentCommentId == null))
                        .ThenInclude(c => c.Author)
                    .Include(p => p.Comments.Where(c => c.ParentCommentId == null))
                        .ThenInclude(c => c.Replies)
                            .ThenInclude(r => r.Author)
                    .FirstOrDefaultAsync(p => p.Id == postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving post {PostId}", postId);
                return null;
            }
        }

        /// <summary>
        /// Retrieves posts for a user's feed including posts from friends and the user themselves.
        /// </summary>
        /// <param name="userId">The unique identifier of the user viewing the feed.</param>
        /// <param name="pageSize">Number of posts per page.</param>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <returns>A list of posts for the user's feed.</returns>
        public async Task<List<Post>> GetFeedPostsAsync(string userId, int pageSize = 20, int pageNumber = 1)
        {
            try
            {
                return await _postRepository.GetFeedPostsAsync(userId, (pageNumber - 1) * pageSize, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feed for user {UserId}", userId);
                return new List<Post>();
            }
        }

        /// <summary>
        /// Retrieves posts authored by a specific user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose posts to retrieve.</param>
        /// <param name="pageSize">Number of posts per page.</param>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <returns>A list of posts by the specified user.</returns>
        public async Task<List<Post>> GetPostsByUserAsync(string userId, int pageSize = 20, int pageNumber = 1)
        {
            try
            {
                return await _postRepository.GetByAuthorAsync(userId, (pageNumber - 1) * pageSize, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts for user {UserId}", userId);
                return new List<Post>();
            }
        }

        /// <summary>
        /// Retrieves public posts for unauthenticated users.
        /// </summary>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <param name="pageSize">Number of posts per page.</param>
        /// <returns>A tuple containing the list of posts and a flag indicating if more posts are available.</returns>
        public async Task<(List<Post> Posts, bool HasMore)> GetPublicPostsAsync(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                return await _postRepository.GetPublicPostsAsync(pageNumber, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public posts");
                return (new List<Post>(), false);
            }
        }

        /// <summary>
        /// Gets the total count of posts in a user's feed.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The total number of posts in the user's feed.</returns>
        public async Task<int> GetFeedPostsCountAsync(string userId)
        {
            try
            {
                return await _postRepository.GetFeedPostsCountAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feed posts count for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Retrieves a single public post by ID with all its details for direct link access.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="currentUserId">Optional user ID of the current viewer for personalized data like user reactions.</param>
        /// <returns>A tuple containing success status, error message if failed, and the post view model if successful.</returns>
        public async Task<(bool Success, string? ErrorMessage, PostViewModel? Data)> GetPublicPostByIdAsync(int postId, string? currentUserId = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Get the post with all its includes
                var post = await context.Posts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .FirstOrDefaultAsync(p => p.Id == postId);

                // Check if post exists
                if (post == null)
                {
                    return (false, "Sorry, this post is no longer available.", null);
                }

                // Check if post is public
                if (post.AudienceType != AudienceType.Public)
                {
                    return (false, "Sorry, this post is no longer available.", null);
                }

                // Build the PostViewModel
                var postViewModel = new PostViewModel
                {
                    PostId = post.Id,
                    AuthorId = post.AuthorId,
                    AuthorName = await _userPreferenceService.FormatUserDisplayNameAsync(
                        post.Author.Id,
                        post.Author.FirstName ?? string.Empty,
                        post.Author.LastName ?? string.Empty,
                        post.Author.UserName ?? "Unknown"
                    ),
                    AuthorImageUrl = post.Author.ProfilePictureUrl,
                    CreatedAt = post.CreatedAt,
                    Content = post.Content,
                    LikeCount = post.LikeCount,
                    CommentCount = post.CommentCount,
                    ShareCount = post.ShareCount,
                    AudienceType = post.AudienceType,
                    Images = post.Images?.ToList() ?? new List<PostImage>()
                };

                // Get reaction breakdown
                postViewModel.ReactionBreakdown = await GetReactionBreakdownAsync(postId);

                // Get user's reaction if logged in
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    postViewModel.UserReaction = await GetUserReactionAsync(postId, currentUserId);
                    postViewModel.IsPinned = await IsPostPinnedAsync(postId, currentUserId);
                }

                // Get direct comment count
                postViewModel.DirectCommentCount = await GetDirectCommentCountAsync(postId);

                // Get comments
                var comments = await GetCommentsAsync(postId);
                var commentModels = new List<CommentDisplayModel>();

                foreach (var comment in comments)
                {
                    var commentModel = await BuildCommentDisplayModelAsync(comment, currentUserId);
                    commentModels.Add(commentModel);
                }

                postViewModel.Comments = commentModels;

                return (true, null, postViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public post {PostId}", postId);
                return (false, "An error occurred while loading this post.", null);
            }
        }

        /// <summary>
        /// Builds a display model for a comment including its reactions, replies, and author information.
        /// </summary>
        /// <param name="comment">The comment entity to transform.</param>
        /// <param name="currentUserId">Optional user ID of the current viewer for user-specific reaction data.</param>
        /// <returns>A fully populated comment display model.</returns>
        private async Task<CommentDisplayModel> BuildCommentDisplayModelAsync(Comment comment, string? currentUserId)
        {
            // Load replies for this comment
            var replies = await GetRepliesAsync(comment.Id);
            var replyModels = new List<CommentDisplayModel>();

            foreach (var reply in replies)
            {
                var replyModel = await BuildCommentDisplayModelAsync(reply, currentUserId);
                replyModels.Add(replyModel);
            }

            return new CommentDisplayModel
            {
                CommentId = comment.Id,
                UserName = await _userPreferenceService.FormatUserDisplayNameAsync(
                    comment.Author.Id,
                    comment.Author.FirstName ?? string.Empty,
                    comment.Author.LastName ?? string.Empty,
                    comment.Author.UserName ?? "Unknown"
                ),
                UserImageUrl = comment.Author?.ProfilePictureUrl,
                CommentAuthorId = comment.AuthorId,
                CommentText = comment.Content,
                ImageUrl = comment.ImageUrl,
                Timestamp = comment.CreatedAt,
                Replies = replyModels.Any() ? replyModels : null,
                LikeCount = await GetCommentLikeCountAsync(comment.Id),
                UserReaction = !string.IsNullOrEmpty(currentUserId) ? await GetUserCommentReactionAsync(comment.Id, currentUserId) : null,
                ReactionBreakdown = await GetCommentReactionBreakdownAsync(comment.Id)
            };
        }

        #endregion

        #region Comment Operations

        /// <summary>
        /// Adds a comment to a post, with optional image attachment and support for nested replies.
        /// Sends appropriate notifications to post author or parent comment author.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to comment on.</param>
        /// <param name="authorId">The unique identifier of the comment author.</param>
        /// <param name="content">The text content of the comment.</param>
        /// <param name="imageUrl">Optional URL of an image to attach to the comment.</param>
        /// <param name="parentCommentId">Optional ID of the parent comment if this is a reply.</param>
        /// <returns>A tuple containing success status, error message if failed, and the created comment if successful.</returns>
        public async Task<(bool Success, string? ErrorMessage, Comment? Comment)> AddCommentAsync(int postId, string authorId, string content, string? imageUrl = null, int? parentCommentId = null)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return (false, "Comment content cannot be empty.", null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify post exists
                var post = await context.Posts
                    .Include(p => p.Author)
                    .FirstOrDefaultAsync(p => p.Id == postId);
                if (post == null)
                {
                    return (false, "Post not found.", null);
                }

                Comment? parentComment = null;
                // Verify parent comment exists if specified
                if (parentCommentId.HasValue)
                {
                    parentComment = await context.Comments
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
                else
                {
                    // This is a direct comment - check if we've reached the limit
                    var directCommentCount = await context.Comments
                        .CountAsync(c => c.PostId == postId && c.ParentCommentId == null);

                    if (directCommentCount >= _siteSettings.MaxFeedPostDirectCommentCount)
                    {
                        return (false, $"This post has reached the maximum of {_siteSettings.MaxFeedPostDirectCommentCount} direct comments.", null);
                    }
                }

                var comment = new Comment
                {
                    PostId = postId,
                    AuthorId = authorId,
                    Content = content.Trim(),
                    ImageUrl = imageUrl,
                    ParentCommentId = parentCommentId,
                    CreatedAt = DateTime.UtcNow
                };

                context.Comments.Add(comment);

                // Update post comment count
                post.CommentCount++;

                await context.SaveChangesAsync();

                _logger.LogInformation("Comment added to post {PostId} by user {AuthorId}", postId, authorId);

                // Send notifications using helper
                if (parentCommentId.HasValue && parentComment != null)
                {
                    // Reply to a comment - notify the parent comment author
                    await _notificationHelper.NotifyCommentReplyAsync(
                        parentComment.AuthorId,
                        authorId,
                        postId,
                        comment.Id,
                        NotificationType.CommentReply
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
                        NotificationType.PostComment
                    );
                }

                return (true, null, comment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment to post {PostId} for user {AuthorId}", postId, authorId);
                return (false, "An error occurred while adding the comment.", null);
            }
        }

        /// <summary>
        /// Deletes a comment and all its replies, updating the post's comment count accordingly.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment to delete.</param>
        /// <param name="userId">The user ID of the person requesting deletion (must be the author).</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> DeleteCommentAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.Comments
                    .Include(c => c.Post)
                    .Include(c => c.Replies)
                    .FirstOrDefaultAsync(c => c.Id == commentId);

                if (comment == null)
                {
                    return (false, "Comment not found.");
                }

                if (comment.AuthorId != userId)
                {
                    return (false, "You are not authorized to delete this comment.");
                }

                var post = comment.Post;
                var commentCount = 1 + comment.Replies.Count; // Include the comment and all replies

                context.Comments.Remove(comment);

                // Update post comment count
                post.CommentCount -= commentCount;

                await context.SaveChangesAsync();

                _logger.LogInformation("Comment {CommentId} deleted by user {UserId}", commentId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment {CommentId} for user {UserId}", commentId, userId);
                return (false, "An error occurred while deleting the comment.");
            }
        }

        /// <summary>
        /// Retrieves top-level comments for a post (replies are loaded separately via GetRepliesAsync).
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of top-level comments ordered by creation date ascending.</returns>
        public async Task<List<Comment>> GetCommentsAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Load only top-level comments with their authors
                // Nested replies are loaded recursively via GetRepliesAsync() calls
                var comments = await context.Comments
                    .Include(c => c.Author)
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();

                return comments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comments for post {PostId}", postId);
                return new List<Comment>();
            }
        }

        /// <summary>
        /// Retrieves paginated direct comments for a post (excluding nested replies).
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="pageSize">Number of comments per page.</param>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <returns>A list of comments for the specified page.</returns>
        public async Task<List<Comment>> GetCommentsPagedAsync(int postId, int pageSize, int pageNumber)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comments = await context.Comments
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
                _logger.LogError(ex, "Error retrieving paged comments for post {PostId}", postId);
                return new List<Comment>();
            }
        }

        /// <summary>
        /// Retrieves the last N direct comments for a post for feed display, ordered oldest first.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="count">The number of recent comments to retrieve.</param>
        /// <returns>A list of comments ordered from oldest to newest.</returns>
        public async Task<List<Comment>> GetLastCommentsAsync(int postId, int count)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comments = await context.Comments
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
                _logger.LogError(ex, "Error retrieving last comments for post {PostId}", postId);
                return new List<Comment>();
            }
        }

        /// <summary>
        /// Retrieves a comment by its unique identifier including author information.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>The comment if found; otherwise, null.</returns>
        public async Task<Comment?> GetCommentByIdAsync(int commentId)
        {
            return await _commentRepository.GetByIdAsync(commentId, includeAuthor: true, includeReplies: false);
        }

        /// <summary>
        /// Gets the total count of direct comments for a post (excluding nested replies).
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>The count of direct comments.</returns>
        public async Task<int> GetDirectCommentCountAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Comments
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving direct comment count for post {PostId}", postId);
                return 0;
            }
        }

        /// <summary>
        /// Retrieves replies for a specific comment ordered by creation date.
        /// </summary>
        /// <param name="commentId">The unique identifier of the parent comment.</param>
        /// <returns>A list of reply comments with author information.</returns>
        public async Task<List<Comment>> GetRepliesAsync(int commentId)
        {
            return await _commentRepository.GetRepliesAsync(commentId);
        }

        #endregion

        #region Like Operations

        /// <summary>
        /// Toggles a like on a post - adds like if not liked, removes if already liked.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user toggling the like.</param>
        /// <returns>A tuple containing success status, error message if failed, and whether the post is now liked.</returns>
        public async Task<(bool Success, string? ErrorMessage, bool IsLiked)> ToggleLikeAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts.FindAsync(postId);
                if (post == null)
                {
                    return (false, "Post not found.", false);
                }

                var existingLike = await context.Likes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

                if (existingLike != null)
                {
                    // Unlike
                    context.Likes.Remove(existingLike);
                    post.LikeCount--;
                    await context.SaveChangesAsync();

                    _logger.LogInformation("User {UserId} unliked post {PostId}", userId, postId);
                    return (true, null, false);
                }
                else
                {
                    // Like
                    var like = new Like
                    {
                        PostId = postId,
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.Likes.Add(like);
                    post.LikeCount++;
                    await context.SaveChangesAsync();

                    _logger.LogInformation("User {UserId} liked post {PostId}", userId, postId);
                    return (true, null, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling like on post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while processing your like.", false);
            }
        }

        /// <summary>
        /// Checks if a user has liked a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user has liked the post; otherwise, false.</returns>
        public async Task<bool> HasUserLikedPostAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Likes
                    .AnyAsync(l => l.PostId == postId && l.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} liked post {PostId}", userId, postId);
                return false;
            }
        }

        /// <summary>
        /// Retrieves the users who have liked a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of users who liked the post, ordered by most recent first.</returns>
        public async Task<List<ApplicationUser>> GetPostLikesAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var users = await context.Likes
                    .Include(l => l.User)
                    .Where(l => l.PostId == postId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => l.User)
                    .ToListAsync();

                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving likes for post {PostId}", postId);
                return new List<ApplicationUser>();
            }
        }

        /// <summary>
        /// Gets the total like count for a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>The number of likes on the post.</returns>
        public async Task<int> GetLikeCountAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts.FindAsync(postId);
                return post?.LikeCount ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting like count for post {PostId}", postId);
                return 0;
            }
        }

        /// <summary>
        /// Adds a new reaction to a post or updates an existing reaction type.
        /// Sends a notification to the post author for new reactions.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user reacting.</param>
        /// <param name="reactionType">The type of reaction to add or update to.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> AddOrUpdateReactionAsync(int postId, string userId, LikeType reactionType)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts
                    .Include(p => p.Author)
                    .FirstOrDefaultAsync(p => p.Id == postId);
                if (post == null)
                {
                    return (false, "Post not found.");
                }

                var existingLike = await context.Likes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

                bool isNewReaction = existingLike == null;

                if (existingLike != null)
                {
                    // Update existing reaction type
                    existingLike.Type = reactionType;
                    _logger.LogInformation("User {UserId} changed reaction on post {PostId} to {ReactionType}", userId, postId, reactionType);
                }
                else
                {
                    // Add new reaction
                    var like = new Like
                    {
                        PostId = postId,
                        UserId = userId,
                        Type = reactionType,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.Likes.Add(like);
                    post.LikeCount++;
                    _logger.LogInformation("User {UserId} reacted to post {PostId} with {ReactionType}", userId, postId, reactionType);
                }

                await context.SaveChangesAsync();

                // Create notification for new reactions only (not for changing reactions)
                if (isNewReaction)
                {
                    await _notificationHelper.NotifyPostReactionAsync(
                        post.AuthorId,
                        userId,
                        postId,
                        reactionType,
                        NotificationType.PostLiked
                    );
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating reaction on post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while processing your reaction.");
            }
        }

        /// <summary>
        /// Gets the breakdown of reaction types and their counts for a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A dictionary mapping reaction types to their counts.</returns>
        public async Task<Dictionary<LikeType, int>> GetReactionBreakdownAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var reactionCounts = await context.Likes
                    .Where(l => l.PostId == postId)
                    .GroupBy(l => l.Type)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Type, x => x.Count);

                return reactionCounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reaction breakdown for post {PostId}", postId);
                return new Dictionary<LikeType, int>();
            }
        }

        /// <summary>
        /// Gets a user's reaction type for a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The user's reaction type if they have reacted; otherwise, null.</returns>
        public async Task<LikeType?> GetUserReactionAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var like = await context.Likes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

                return like?.Type;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user reaction for post {PostId} and user {UserId}", postId, userId);
                return null;
            }
        }

        /// <summary>
        /// Gets reaction breakdowns for multiple posts in a single database query.
        /// </summary>
        /// <param name="postIds">The list of post identifiers to query.</param>
        /// <returns>A dictionary mapping post IDs to dictionaries of like types and their counts.</returns>
        public async Task<Dictionary<int, Dictionary<LikeType, int>>> GetReactionBreakdownForPostsAsync(List<int> postIds)
        {
            try
            {
                return await _likeRepository.GetCountsByTypeForPostsAsync(postIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reaction breakdown for multiple posts");
                return new Dictionary<int, Dictionary<LikeType, int>>();
            }
        }

        /// <summary>
        /// Gets user reactions for multiple posts in a single database query.
        /// </summary>
        /// <param name="postIds">The list of post identifiers to query.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A dictionary mapping post IDs to the user's reaction type (or null if no reaction).</returns>
        public async Task<Dictionary<int, LikeType?>> GetUserReactionsForPostsAsync(List<int> postIds, string userId)
        {
            try
            {
                return await _likeRepository.GetUserReactionsForPostsAsync(postIds, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user reactions for multiple posts");
                return new Dictionary<int, LikeType?>();
            }
        }

        /// <summary>
        /// Removes a user's reaction from a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user whose reaction to remove.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> RemoveLikeAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts.FindAsync(postId);
                if (post == null)
                {
                    return (false, "Post not found.");
                }

                var existingLike = await context.Likes
                    .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

                if (existingLike != null)
                {
                    context.Likes.Remove(existingLike);
                    post.LikeCount = Math.Max(0, post.LikeCount - 1); // Ensure count doesn't go negative
                    await context.SaveChangesAsync();

                    _logger.LogInformation("User {UserId} removed reaction from post {PostId}", userId, postId);
                    return (true, null);
                }

                return (false, "No reaction to remove.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction from post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while removing your reaction.");
            }
        }

        /// <summary>
        /// Retrieves detailed like information for a post including user info, reaction type, and timestamp.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="maxCount">Maximum number of likes to retrieve.</param>
        /// <returns>A list of tuples containing user, reaction type, and creation timestamp.</returns>
        public async Task<List<(ApplicationUser User, LikeType Type, DateTime CreatedAt)>> GetPostLikesWithDetailsAsync(int postId, int maxCount = 100)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var likes = await context.Likes
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
                _logger.LogError(ex, "Error getting detailed likes for post {PostId}", postId);
                return new List<(ApplicationUser, LikeType, DateTime)>();
            }
        }

        #endregion

        #region Post Image Operations

        /// <summary>
        /// Adds an image to an existing post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="imageUrl">The URL of the image to add.</param>
        /// <param name="userId">The user ID of the person adding the image (must be the post author).</param>
        /// <returns>A tuple containing success status, error message if failed, and the created post image if successful.</returns>
        public async Task<(bool Success, string? ErrorMessage, PostImage? PostImage)> AddImageToPostAsync(int postId, string imageUrl, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts.FindAsync(postId);
                if (post == null)
                {
                    return (false, "Post not found.", null);
                }

                if (post.AuthorId != userId)
                {
                    return (false, "You are not authorized to add images to this post.", null);
                }

                // Get the next display order
                var maxDisplayOrder = await context.PostImages
                    .Where(pi => pi.PostId == postId)
                    .MaxAsync(pi => (int?)pi.DisplayOrder) ?? -1;

                var postImage = new PostImage
                {
                    PostId = postId,
                    ImageUrl = imageUrl,
                    DisplayOrder = maxDisplayOrder + 1,
                    UploadedAt = DateTime.UtcNow
                };

                context.PostImages.Add(postImage);
                await context.SaveChangesAsync();

                _logger.LogInformation("Image added to post {PostId} by user {UserId}", postId, userId);
                return (true, null, postImage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding image to post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while adding the image.", null);
            }
        }

        /// <summary>
        /// Removes an image from a post.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image to remove.</param>
        /// <param name="userId">The user ID of the person removing the image (must be the post author).</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> RemoveImageFromPostAsync(int imageId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var postImage = await context.PostImages
                    .Include(pi => pi.Post)
                    .FirstOrDefaultAsync(pi => pi.Id == imageId);

                if (postImage == null)
                {
                    return (false, "Image not found.");
                }

                if (postImage.Post.AuthorId != userId)
                {
                    return (false, "You are not authorized to remove this image.");
                }

                context.PostImages.Remove(postImage);
                await context.SaveChangesAsync();

                _logger.LogInformation("Image {ImageId} removed from post {PostId} by user {UserId}", imageId, postImage.PostId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing image {ImageId} for user {UserId}", imageId, userId);
                return (false, "An error occurred while removing the image.");
            }
        }

        /// <summary>
        /// Retrieves all images for a specific post ordered by display order.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of post images in display order.</returns>
        public async Task<List<PostImage>> GetPostImagesAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var images = await context.PostImages
                    .Where(pi => pi.PostId == postId)
                    .OrderBy(pi => pi.DisplayOrder)
                    .ToListAsync();

                return images;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving images for post {PostId}", postId);
                return new List<PostImage>();
            }
        }

        #endregion

        #region Comment Like Operations

        /// <summary>
        /// Adds a new reaction to a comment or updates an existing reaction type.
        /// Sends a notification to the comment author for new reactions.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The unique identifier of the user reacting.</param>
        /// <param name="reactionType">The type of reaction to add or update to.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> AddOrUpdateCommentReactionAsync(int commentId, string userId, LikeType reactionType)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.Comments
                    .Include(c => c.Author)
                    .Include(c => c.Post)
                    .FirstOrDefaultAsync(c => c.Id == commentId);
                if (comment == null)
                {
                    return (false, "Comment not found.");
                }

                var existingLike = await context.CommentLikes
                    .FirstOrDefaultAsync(cl => cl.CommentId == commentId && cl.UserId == userId);

                bool isNewReaction = existingLike == null;

                if (existingLike != null)
                {
                    // Update existing reaction type
                    existingLike.Type = reactionType;
                    _logger.LogInformation("User {UserId} changed reaction on comment {CommentId} to {ReactionType}", userId, commentId, reactionType);
                }
                else
                {
                    // Add new reaction
                    var commentLike = new CommentLike
                    {
                        CommentId = commentId,
                        UserId = userId,
                        Type = reactionType,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.CommentLikes.Add(commentLike);
                    _logger.LogInformation("User {UserId} reacted to comment {CommentId} with {ReactionType}", userId, commentId, reactionType);
                }

                await context.SaveChangesAsync();

                // Create notification for new reactions only (not for changing reactions)
                if (isNewReaction)
                {
                    await _notificationHelper.NotifyCommentReactionAsync(
                        comment.AuthorId,
                        userId,
                        comment.PostId,
                        commentId,
                        reactionType,
                        NotificationType.CommentLiked
                    );
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating reaction on comment {CommentId} for user {UserId}", commentId, userId);
                return (false, "An error occurred while processing your reaction.");
            }
        }

        /// <summary>
        /// Removes a user's reaction from a specific comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The unique identifier of the user whose reaction to remove.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> RemoveCommentReactionAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.Comments.FindAsync(commentId);
                if (comment == null)
                {
                    return (false, "Comment not found.");
                }

                var existingLike = await context.CommentLikes
                    .FirstOrDefaultAsync(cl => cl.CommentId == commentId && cl.UserId == userId);

                if (existingLike != null)
                {
                    context.CommentLikes.Remove(existingLike);
                    await context.SaveChangesAsync();

                    _logger.LogInformation("User {UserId} removed reaction from comment {CommentId}", userId, commentId);
                    return (true, null);
                }

                return (false, "No reaction to remove.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing reaction from comment {CommentId} for user {UserId}", commentId, userId);
                return (false, "An error occurred while removing your reaction.");
            }
        }

        /// <summary>
        /// Gets the breakdown of reaction types and their counts for a specific comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>A dictionary mapping reaction types to their counts.</returns>
        public async Task<Dictionary<LikeType, int>> GetCommentReactionBreakdownAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var reactionCounts = await context.CommentLikes
                    .Where(cl => cl.CommentId == commentId)
                    .GroupBy(cl => cl.Type)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Type, x => x.Count);

                return reactionCounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reaction breakdown for comment {CommentId}", commentId);
                return new Dictionary<LikeType, int>();
            }
        }

        /// <summary>
        /// Gets a user's reaction type for a specific comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>The user's reaction type if they have reacted; otherwise, null.</returns>
        public async Task<LikeType?> GetUserCommentReactionAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var like = await context.CommentLikes
                    .FirstOrDefaultAsync(cl => cl.CommentId == commentId && cl.UserId == userId);

                return like?.Type;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user reaction for comment {CommentId} and user {UserId}", commentId, userId);
                return null;
            }
        }

        /// <summary>
        /// Gets the total like count for a specific comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>The number of likes on the comment.</returns>
        public async Task<int> GetCommentLikeCountAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.CommentLikes
                    .CountAsync(cl => cl.CommentId == commentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting like count for comment {CommentId}", commentId);
                return 0;
            }
        }

        /// <summary>
        /// Gets multiple comments by their IDs in a single query.
        /// This batch method reduces database round trips when loading comment trees.
        /// </summary>
        /// <param name="commentIds">Collection of comment IDs to retrieve.</param>
        /// <returns>A list of comments matching the provided IDs.</returns>
        public async Task<List<Comment>> GetCommentsByIdsAsync(IEnumerable<int> commentIds)
        {
            return await _commentRepository.GetByIdsAsync(commentIds, includeAuthor: true);
        }

        /// <summary>
        /// Gets all comments for a post including top-level comments and all nested replies in a single query.
        /// Optimized method that eliminates N+1 query problems when loading comment trees.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of all comments for the post with author information.</returns>
        public async Task<List<Comment>> GetAllCommentsAsync(int postId)
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

        #region Pinned Posts Operations

        /// <summary>
        /// Checks if a post is pinned by a specific user.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the post is pinned by the user; otherwise, false.</returns>
        public async Task<bool> IsPostPinnedAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.PinnedPosts
                    .AnyAsync(pp => pp.PostId == postId && pp.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if post {PostId} is pinned by user {UserId}", postId, userId);
                return false;
            }
        }

        /// <summary>
        /// Pins a post for a user so it appears in their pinned posts collection.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to pin.</param>
        /// <param name="userId">The unique identifier of the user pinning the post.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> PinPostAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check if post exists
                var postExists = await context.Posts.AnyAsync(p => p.Id == postId);
                if (!postExists)
                {
                    return (false, "Post not found.");
                }

                // Check if already pinned
                var alreadyPinned = await context.PinnedPosts
                    .AnyAsync(pp => pp.PostId == postId && pp.UserId == userId);

                if (alreadyPinned)
                {
                    return (true, null); // Already pinned, consider it successful
                }

                // Create pinned post entry
                var pinnedPost = new PinnedPost
                {
                    PostId = postId,
                    UserId = userId,
                    PinnedAt = DateTime.UtcNow
                };

                context.PinnedPosts.Add(pinnedPost);
                await context.SaveChangesAsync();

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while pinning the post.");
            }
        }

        /// <summary>
        /// Unpins a post for a user, removing it from their pinned posts collection.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to unpin.</param>
        /// <param name="userId">The unique identifier of the user unpinning the post.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> UnpinPostAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var pinnedPost = await context.PinnedPosts
                    .FirstOrDefaultAsync(pp => pp.PostId == postId && pp.UserId == userId);

                if (pinnedPost == null)
                {
                    return (true, null); // Not pinned, consider it successful
                }

                context.PinnedPosts.Remove(pinnedPost);
                await context.SaveChangesAsync();

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while unpinning the post.");
            }
        }

        /// <summary>
        /// Gets the pinned status for multiple posts for a specific user in a single query.
        /// </summary>
        /// <param name="postIds">List of post IDs to check pinned status for.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A dictionary mapping post IDs to their pinned status.</returns>
        public async Task<Dictionary<int, bool>> GetPinnedStatusForPostsAsync(List<int> postIds, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var pinnedPostIds = await context.PinnedPosts
                    .Where(pp => postIds.Contains(pp.PostId) && pp.UserId == userId)
                    .Select(pp => pp.PostId)
                    .ToListAsync();

                return postIds.ToDictionary(id => id, id => pinnedPostIds.Contains(id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pinned status for posts for user {UserId}", userId);
                return postIds.ToDictionary(id => id, id => false);
            }
        }

        /// <summary>
        /// Retrieves all pinned posts for a user with author and image information.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A list of pinned posts ordered by most recently pinned first.</returns>
        public async Task<List<Post>> GetPinnedPostsAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var pinnedPosts = await context.PinnedPosts
                    .Where(pp => pp.UserId == userId)
                    .Include(pp => pp.Post)
                        .ThenInclude(p => p.Author)
                    .Include(pp => pp.Post)
                        .ThenInclude(p => p.Images)
                    .OrderByDescending(pp => pp.PinnedAt)
                    .Select(pp => pp.Post)
                    .ToListAsync();

                return pinnedPosts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pinned posts for user {UserId}", userId);
                return new List<Post>();
            }
        }

        #endregion

        #region Post Notification Muting

        /// <summary>
        /// Mutes notifications for a specific post for a user.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user muting notifications.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> MutePostNotificationsAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check if post exists
                var postExists = await context.Posts.AnyAsync(p => p.Id == postId);
                if (!postExists)
                {
                    return (false, "Post not found.");
                }

                // Check if already muted
                var alreadyMuted = await context.PostNotificationMutes
                    .AnyAsync(m => m.PostId == postId && m.UserId == userId);

                if (alreadyMuted)
                {
                    return (true, null); // Already muted, consider it successful
                }

                // Create mute entry
                var mute = new PostNotificationMute
                {
                    PostId = postId,
                    UserId = userId,
                    MutedAt = DateTime.UtcNow
                };

                context.PostNotificationMutes.Add(mute);
                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} muted notifications for post {PostId}", userId, postId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error muting notifications for post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while muting notifications.");
            }
        }

        /// <summary>
        /// Unmutes notifications for a specific post for a user.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user unmuting notifications.</param>
        /// <returns>A tuple containing success status and error message if failed.</returns>
        public async Task<(bool Success, string? ErrorMessage)> UnmutePostNotificationsAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var mute = await context.PostNotificationMutes
                    .FirstOrDefaultAsync(m => m.PostId == postId && m.UserId == userId);

                if (mute == null)
                {
                    return (true, null); // Not muted, consider it successful
                }

                context.PostNotificationMutes.Remove(mute);
                await context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} unmuted notifications for post {PostId}", userId, postId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unmuting notifications for post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while unmuting notifications.");
            }
        }

        /// <summary>
        /// Checks if a user has muted notifications for a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if notifications are muted for the post; otherwise, false.</returns>
        public async Task<bool> IsPostNotificationMutedAsync(int postId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.PostNotificationMutes
                    .AnyAsync(m => m.PostId == postId && m.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if post {PostId} notifications are muted for user {UserId}", postId, userId);
                return false;
            }
        }

        /// <summary>
        /// Gets the mute status for multiple posts for a specific user in a single query.
        /// Used to batch load mute status and avoid N+1 queries when displaying feeds.
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

                var mutedPostIds = await context.PostNotificationMutes
                    .Where(m => postIds.Contains(m.PostId) && m.UserId == userId)
                    .Select(m => m.PostId)
                    .ToListAsync();

                return postIds.ToDictionary(id => id, id => mutedPostIds.Contains(id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mute status for posts for user {UserId}", userId);
                return postIds.ToDictionary(id => id, id => false);
            }
        }

        #endregion

        #region User Uploads

        /// <summary>
        /// Retrieves a paginated list of images uploaded by a user.
        /// Filters to common image file extensions (jpg, jpeg, png, gif, webp).
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="page">The page number to retrieve (1-based).</param>
        /// <param name="pageSize">Number of images per page.</param>
        /// <returns>A tuple containing success status, list of images, total count, and error message if failed.</returns>
        public async Task<(bool Success, List<PostImage>? Images, int TotalCount, string? ErrorMessage)> GetUserUploadedImagesAsync(string userId, int page = 1, int pageSize = 24)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Get all images from posts authored by this user
                // Filter by image file extensions (.jpg, .png, .gif, .webp)
                var query = context.PostImages
                    .Include(pi => pi.Post)
                    .Where(pi => pi.Post.AuthorId == userId && 
                                 (pi.ImageUrl.EndsWith(".jpg") || 
                                  pi.ImageUrl.EndsWith(".jpeg") || 
                                  pi.ImageUrl.EndsWith(".png") || 
                                  pi.ImageUrl.EndsWith(".gif") || 
                                  pi.ImageUrl.EndsWith(".webp")))
                    .OrderByDescending(pi => pi.UploadedAt);

                var totalCount = await query.CountAsync();

                var images = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (true, images, totalCount, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting uploaded images for user {UserId}", userId);
                return (false, null, 0, "An error occurred while retrieving your images.");
            }
        }

        /// <summary>
        /// Retrieves a paginated list of videos uploaded by a user.
        /// Filters to common video file extensions (mp4, webm, ogg, mov).
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="page">The page number to retrieve (1-based).</param>
        /// <param name="pageSize">Number of videos per page.</param>
        /// <returns>A tuple containing success status, list of videos, total count, and error message if failed.</returns>
        public async Task<(bool Success, List<PostImage>? Videos, int TotalCount, string? ErrorMessage)> GetUserUploadedVideosAsync(string userId, int page = 1, int pageSize = 24)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Get all videos from posts authored by this user
                // Filter by video file extensions (.mp4, .webm, .ogg, .mov)
                var query = context.PostImages
                    .Include(pi => pi.Post)
                    .Where(pi => pi.Post.AuthorId == userId && 
                                 (pi.ImageUrl.EndsWith(".mp4") || 
                                  pi.ImageUrl.EndsWith(".webm") || 
                                  pi.ImageUrl.EndsWith(".ogg") || 
                                  pi.ImageUrl.EndsWith(".mov")))
                    .OrderByDescending(pi => pi.UploadedAt);

                var totalCount = await query.CountAsync();

                var videos = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (true, videos, totalCount, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting uploaded videos for user {UserId}", userId);
                return (false, null, 0, "An error occurred while retrieving your videos.");
            }
        }

        #endregion

        #region Projection-Based Methods (Phase 3 Optimizations)

        /// <summary>
        /// Retrieves feed posts as view models using optimized projection queries.
        /// Uses database-side projection to reduce data transfer by 50-70%.
        /// Returns PostViewModel objects ready for direct use in UI components.
        /// </summary>
        /// <param name="userId">The unique identifier of the user viewing the feed.</param>
        /// <param name="pageSize">Number of posts per page.</param>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <returns>A list of PostViewModel objects for the user's feed.</returns>
        public async Task<List<PostViewModel>> GetFeedPostsAsViewModelsAsync(string userId, int pageSize = 20, int pageNumber = 1)
        {
            try
            {
                var projections = await _postRepository.GetFeedPostsAsProjectionAsync(userId, (pageNumber - 1) * pageSize, pageSize);
                return projections.ToViewModels();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feed view models for user {UserId}", userId);
                return new List<PostViewModel>();
            }
        }

        /// <summary>
        /// Retrieves a single post as a view model using optimized projection query.
        /// Returns a PostViewModel object ready for direct use in UI components.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>The PostViewModel if found; otherwise, null.</returns>
        public async Task<PostViewModel?> GetPostAsViewModelAsync(int postId)
        {
            try
            {
                var projection = await _postRepository.GetByIdAsProjectionAsync(postId);
                return projection?.ToViewModel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving post view model for post {PostId}", postId);
                return null;
            }
        }

        /// <summary>
        /// Retrieves posts by a specific author as view models using optimized projection queries.
        /// Uses database-side projection to reduce data transfer by 50-70%.
        /// </summary>
        /// <param name="authorId">The unique identifier of the author.</param>
        /// <param name="pageSize">Number of posts per page.</param>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <returns>A list of PostViewModel objects for the author's posts.</returns>
        public async Task<List<PostViewModel>> GetPostsByAuthorAsViewModelsAsync(string authorId, int pageSize = 20, int pageNumber = 1)
        {
            try
            {
                var projections = await _postRepository.GetByAuthorAsProjectionAsync(authorId, (pageNumber - 1) * pageSize, pageSize);
                return projections.ToViewModels();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving author post view models for user {AuthorId}", authorId);
                return new List<PostViewModel>();
            }
        }

        /// <summary>
        /// Retrieves public posts as view models using optimized projection queries.
        /// Uses database-side projection to reduce data transfer by 50-70%.
        /// </summary>
        /// <param name="pageNumber">The page number to retrieve (1-based).</param>
        /// <param name="pageSize">Number of posts per page.</param>
        /// <returns>A tuple containing the list of PostViewModel objects and a flag indicating if more posts are available.</returns>
        public async Task<(List<PostViewModel> Posts, bool HasMore)> GetPublicPostsAsViewModelsAsync(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var (projections, hasMore) = await _postRepository.GetPublicPostsAsProjectionAsync(pageNumber, pageSize);
                return (projections.ToViewModels(), hasMore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public post view models");
                return (new List<PostViewModel>(), false);
            }
        }

        #endregion
    }
}
