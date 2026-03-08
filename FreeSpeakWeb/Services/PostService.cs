using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Services
{
    public class PostService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<PostService> _logger;
        private readonly SiteSettings _siteSettings;
        private readonly IWebHostEnvironment _environment;

        public PostService(
            IDbContextFactory<ApplicationDbContext> contextFactory, 
            ILogger<PostService> logger, 
            IOptions<SiteSettings> siteSettings,
            IWebHostEnvironment environment)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _siteSettings = siteSettings.Value;
            _environment = environment;
        }

        #region Post Operations

        /// <summary>
        /// Create a new post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, Post? Post)> CreatePostAsync(string authorId, string content, AudienceType audienceType = AudienceType.Public, List<string>? imageUrls = null)
        {
            // Allow empty content if images are provided
            if (string.IsNullOrWhiteSpace(content) && (imageUrls == null || !imageUrls.Any()))
            {
                return (false, "Post must contain either text or images.", null);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = new Post
                {
                    AuthorId = authorId,
                    Content = string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    AudienceType = audienceType
                };

                context.Posts.Add(post);
                await context.SaveChangesAsync();

                // Add images if provided
                if (imageUrls != null && imageUrls.Any())
                {
                    for (int i = 0; i < imageUrls.Count; i++)
                    {
                        var postImage = new PostImage
                        {
                            PostId = post.Id,
                            ImageUrl = imageUrls[i],
                            DisplayOrder = i,
                            UploadedAt = DateTime.UtcNow
                        };
                        context.PostImages.Add(postImage);
                    }
                    await context.SaveChangesAsync();
                }

                _logger.LogInformation("Post created by user {AuthorId}: Post ID {PostId}", authorId, post.Id);
                return (true, null, post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post for user {AuthorId}", authorId);
                return (false, "An error occurred while creating the post.", null);
            }
        }

        /// <summary>
        /// Update an existing post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> UpdatePostAsync(int postId, string userId, string newContent)
        {
            if (string.IsNullOrWhiteSpace(newContent))
            {
                return (false, "Post content cannot be empty.");
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts.FindAsync(postId);

                if (post == null)
                {
                    return (false, "Post not found.");
                }

                if (post.AuthorId != userId)
                {
                    return (false, "You are not authorized to edit this post.");
                }

                post.Content = newContent.Trim();
                post.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                _logger.LogInformation("Post {PostId} updated by user {UserId}", postId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while updating the post.");
            }
        }

        /// <summary>
        /// Update the audience type of an existing post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> UpdatePostAudienceAsync(int postId, string userId, AudienceType newAudienceType)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts.FindAsync(postId);

                if (post == null)
                {
                    return (false, "Post not found.");
                }

                if (post.AuthorId != userId)
                {
                    return (false, "You are not authorized to modify this post.");
                }

                post.AudienceType = newAudienceType;
                post.UpdatedAt = DateTime.UtcNow;

                // Remove any pinned post records since the audience has changed
                var pinnedPosts = await context.PinnedPosts
                    .Where(pp => pp.PostId == postId)
                    .ToListAsync();

                if (pinnedPosts.Any())
                {
                    context.PinnedPosts.RemoveRange(pinnedPosts);
                    _logger.LogInformation("Removed {Count} pinned post record(s) for post {PostId} due to audience change", pinnedPosts.Count, postId);
                }

                await context.SaveChangesAsync();

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
        /// Delete a post and all related data (comments, likes, images, pinned posts, etc.)
        /// </summary>
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
        /// Get a post by ID with all related data
        /// </summary>
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
        /// Get posts for a user's feed (from friends and self)
        /// </summary>
        public async Task<List<Post>> GetFeedPostsAsync(string userId, int pageSize = 20, int pageNumber = 1)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Get user's friend IDs
                var friendIds = await context.Friendships
                    .Where(f => f.Status == FriendshipStatus.Accepted &&
                               (f.RequesterId == userId || f.AddresseeId == userId))
                    .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
                    .ToListAsync();

                // Include user's own ID
                var authorIds = friendIds.Append(userId).ToList();

                var posts = await context.Posts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Where(p => authorIds.Contains(p.AuthorId))
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return posts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feed for user {UserId}", userId);
                return new List<Post>();
            }
        }

        /// <summary>
        /// Get posts by a specific user
        /// </summary>
        public async Task<List<Post>> GetPostsByUserAsync(string userId, int pageSize = 20, int pageNumber = 1)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var posts = await context.Posts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Where(p => p.AuthorId == userId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return posts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts for user {UserId}", userId);
                return new List<Post>();
            }
        }

        /// <summary>
        /// Get total count of posts in user's feed
        /// </summary>
        public async Task<int> GetFeedPostsCountAsync(string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var friendIds = await context.Friendships
                    .Where(f => f.Status == FriendshipStatus.Accepted &&
                               (f.RequesterId == userId || f.AddresseeId == userId))
                    .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
                    .ToListAsync();

                var authorIds = friendIds.Append(userId).ToList();

                return await context.Posts
                    .Where(p => authorIds.Contains(p.AuthorId))
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feed posts count for user {UserId}", userId);
                return 0;
            }
        }

        #endregion

        #region Comment Operations

        /// <summary>
        /// Add a comment to a post
        /// </summary>
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
                var post = await context.Posts.FindAsync(postId);
                if (post == null)
                {
                    return (false, "Post not found.", null);
                }

                // Verify parent comment exists if specified
                if (parentCommentId.HasValue)
                {
                    var parentComment = await context.Comments.FindAsync(parentCommentId.Value);
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
                return (true, null, comment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment to post {PostId} for user {AuthorId}", postId, authorId);
                return (false, "An error occurred while adding the comment.", null);
            }
        }

        /// <summary>
        /// Delete a comment
        /// </summary>
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
        /// Get comments for a post with nested replies
        /// </summary>
        public async Task<List<Comment>> GetCommentsAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comments = await context.Comments
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
                _logger.LogError(ex, "Error retrieving comments for post {PostId}", postId);
                return new List<Comment>();
            }
        }

        /// <summary>
        /// Get paginated direct comments for a post (no nested replies)
        /// </summary>
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
        /// Get the last N direct comments for a post (for feed display)
        /// </summary>
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
        /// Get the total count of direct comments for a post (excluding replies)
        /// </summary>
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
        /// Get replies for a specific comment
        /// </summary>
        public async Task<List<Comment>> GetRepliesAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var replies = await context.Comments
                    .Include(c => c.Author)
                    .Where(c => c.ParentCommentId == commentId)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();

                return replies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving replies for comment {CommentId}", commentId);
                return new List<Comment>();
            }
        }

        #endregion

        #region Like Operations

        /// <summary>
        /// Toggle like on a post (like if not liked, unlike if already liked)
        /// </summary>
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
        /// Check if a user has liked a post
        /// </summary>
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
        /// Get users who liked a post
        /// </summary>
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
        /// Get like count for a post
        /// </summary>
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
        /// Add or update a reaction to a post
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> AddOrUpdateReactionAsync(int postId, string userId, LikeType reactionType)
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
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating reaction on post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while processing your reaction.");
            }
        }

        /// <summary>
        /// Get the breakdown of reaction types for a post
        /// </summary>
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
        /// Get user's reaction type for a post (null if not reacted)
        /// </summary>
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
        /// Remove a user's reaction from a post
        /// </summary>
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
        /// Get detailed likes for a post including user info and reaction type
        /// </summary>
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
        /// Add an image to a post
        /// </summary>
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
        /// Remove an image from a post
        /// </summary>
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
        /// Get images for a post
        /// </summary>
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
        /// Add or update a reaction to a comment
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> AddOrUpdateCommentReactionAsync(int commentId, string userId, LikeType reactionType)
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
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding/updating reaction on comment {CommentId} for user {UserId}", commentId, userId);
                return (false, "An error occurred while processing your reaction.");
            }
        }

        /// <summary>
        /// Remove a user's reaction from a comment
        /// </summary>
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
        /// Get the breakdown of reaction types for a comment
        /// </summary>
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
        /// Get user's reaction type for a comment (null if not reacted)
        /// </summary>
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
        /// Get like count for a comment
        /// </summary>
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

        #endregion

        #region Pinned Posts Operations

        /// <summary>
        /// Check if a post is pinned by a user
        /// </summary>
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
        /// Pin a post for a user
        /// </summary>
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
        /// Unpin a post for a user
        /// </summary>
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
        /// Get pinned status for multiple posts
        /// </summary>
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

        #endregion

        #region User Uploads

        /// <summary>
        /// Get paginated list of images uploaded by a user
        /// </summary>
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
        /// Get paginated list of videos uploaded by a user
        /// </summary>
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
    }
}
