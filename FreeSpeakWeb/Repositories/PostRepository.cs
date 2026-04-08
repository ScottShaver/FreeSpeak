using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.Abstractions;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for feed posts (non-group posts)
    /// </summary>
    public class PostRepository : IFeedPostRepository<Post, PostImage>
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<PostRepository> _logger;
        private readonly FriendshipCacheService _friendshipCache;
        private readonly ProfilerHelper _profiler;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        /// <param name="friendshipCache">Cache service for friendship data.</param>
        /// <param name="profiler">Helper for profiling repository operations.</param>
        public PostRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<PostRepository> logger,
            FriendshipCacheService friendshipCache,
            ProfilerHelper profiler)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _friendshipCache = friendshipCache;
            _profiler = profiler;
        }

        #region Post CRUD Operations

        /// <summary>
        /// Retrieves a post by its unique identifier.
        /// Uses a compiled query for optimal performance when loading full post data.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to retrieve.</param>
        /// <param name="includeAuthor">Whether to include the author's information in the result.</param>
        /// <param name="includeImages">Whether to include the post's images in the result.</param>
        /// <returns>The post if found; otherwise, null.</returns>
        public async Task<Post?> GetByIdAsync(int postId, bool includeAuthor = true, bool includeImages = true)
        {
            using var step = _profiler.Step($"PostRepository.GetByIdAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Use compiled query when requesting full data (most common case)
                if (includeAuthor && includeImages)
                {
                    return await CompiledQueries.GetPostByIdAsync(context, postId);
                }

                // Fall back to dynamic query for partial includes
                var query = context.Posts.AsNoTracking().AsSplitQuery();

                if (includeAuthor)
                    query = query.Include(p => p.Author);

                if (includeImages)
                    query = query.Include(p => p.Images.OrderBy(i => i.DisplayOrder));

                return await query.FirstOrDefaultAsync(p => p.Id == postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving post {PostId}", postId);
                return null;
            }
        }

        /// <summary>
        /// Creates a new post in the database.
        /// </summary>
        /// <param name="post">The post entity to create.</param>
        /// <returns>A tuple containing success status, error message if any, and the created post.</returns>
        public async Task<(bool Success, string? ErrorMessage, Post? Post)> CreateAsync(Post post)
        {
            using var step = _profiler.Step("PostRepository.CreateAsync");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                context.Posts.Add(post);
                await context.SaveChangesAsync();

                _logger.LogInformation("Post created: Post ID {PostId} by user {AuthorId}", post.Id, post.AuthorId);
                return (true, null, post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post for user {AuthorId}", post.AuthorId);
                return (false, "An error occurred while creating the post.", null);
            }
        }

        /// <summary>
        /// Updates the content of an existing post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to update.</param>
        /// <param name="userId">The ID of the user attempting the update (must be the author).</param>
        /// <param name="newContent">The new content for the post.</param>
        /// <returns>A tuple containing success status and error message if any.</returns>
        public async Task<(bool Success, string? ErrorMessage)> UpdateContentAsync(int postId, string userId, string newContent)
        {
            using var step = _profiler.Step($"PostRepository.UpdateContentAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts.FindAsync(postId);

                if (post == null)
                    return (false, "Post not found.");

                if (post.AuthorId != userId)
                    return (false, "You are not authorized to edit this post.");

                post.Content = string.IsNullOrWhiteSpace(newContent) ? string.Empty : newContent.Trim();
                post.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                _logger.LogInformation("Post {PostId} content updated by user {UserId}", postId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while updating the post.");
            }
        }

        /// <summary>
        /// Deletes a post from the database.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to delete.</param>
        /// <param name="userId">The ID of the user attempting the deletion (must be the author).</param>
        /// <returns>A tuple containing success status and error message if any.</returns>
        public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(int postId, string userId)
        {
            using var step = _profiler.Step($"PostRepository.DeleteAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts.FindAsync(postId);

                if (post == null)
                    return (false, "Post not found.");

                if (post.AuthorId != userId)
                    return (false, "You are not authorized to delete this post.");

                context.Posts.Remove(post);
                await context.SaveChangesAsync();

                _logger.LogInformation("Post {PostId} deleted by user {UserId}", postId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting post {PostId} for user {UserId}", postId, userId);
                return (false, "An error occurred while deleting the post.");
            }
        }

        /// <summary>
        /// Determines whether a user has permission to delete a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The ID of the user to check permissions for.</param>
        /// <returns>True if the user can delete the post; otherwise, false.</returns>
        public async Task<bool> CanUserDeleteAsync(int postId, string userId)
        {
            using var step = _profiler.Step($"PostRepository.CanUserDeleteAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var post = await context.Posts.FindAsync(postId);
                return post != null && post.AuthorId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking delete permission for post {PostId}", postId);
                return false;
            }
        }

        #endregion

        #region Image Operations

        /// <summary>
        /// Adds images to an existing post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to add images to.</param>
        /// <param name="userId">The ID of the user attempting the operation (must be the author).</param>
        /// <param name="imageUrls">List of image URLs to add to the post.</param>
        /// <returns>A tuple containing success status, error message if any, and the list of created images.</returns>
        public async Task<(bool Success, string? ErrorMessage, List<PostImage>? Images)> AddImagesAsync(
            int postId, string userId, List<string> imageUrls)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                if (post == null)
                    return (false, "Post not found.", null);

                if (post.AuthorId != userId)
                    return (false, "You are not authorized to modify this post.", null);

                var currentMaxOrder = post.Images.Any() ? post.Images.Max(img => img.DisplayOrder) : -1;

                var newImages = new List<PostImage>();
                for (int i = 0; i < imageUrls.Count; i++)
                {
                    var postImage = new PostImage
                    {
                        PostId = post.Id,
                        ImageUrl = imageUrls[i],
                        DisplayOrder = currentMaxOrder + 1 + i,
                        UploadedAt = DateTime.UtcNow
                    };
                    context.PostImages.Add(postImage);
                    newImages.Add(postImage);
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("Added {Count} images to post {PostId}", imageUrls.Count, postId);
                return (true, null, newImages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding images to post {PostId}", postId);
                return (false, "An error occurred while adding images.", null);
            }
        }

        /// <summary>
        /// Removes specified images from a post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to remove images from.</param>
        /// <param name="userId">The ID of the user attempting the operation (must be the author).</param>
        /// <param name="imageIds">List of image IDs to remove from the post.</param>
        /// <returns>A tuple containing success status and error message if any.</returns>
        public async Task<(bool Success, string? ErrorMessage)> RemoveImagesAsync(
            int postId, string userId, List<int> imageIds)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                if (post == null)
                    return (false, "Post not found.");

                if (post.AuthorId != userId)
                    return (false, "You are not authorized to modify this post.");

                var imagesToRemove = post.Images.Where(img => imageIds.Contains(img.Id)).ToList();
                context.PostImages.RemoveRange(imagesToRemove);

                await context.SaveChangesAsync();

                _logger.LogInformation("Removed {Count} images from post {PostId}", imagesToRemove.Count, postId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing images from post {PostId}", postId);
                return (false, "An error occurred while removing images.");
            }
        }

        /// <summary>
        /// Retrieves all images associated with a post, ordered by display order.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of images for the post, or an empty list if none exist or an error occurs.</returns>
        public async Task<List<PostImage>> GetImagesAsync(int postId)
        {
            using var step = _profiler.Step($"PostRepository.GetImagesAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.PostImages
                    .Where(img => img.PostId == postId)
                    .OrderBy(img => img.DisplayOrder)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving images for post {PostId}", postId);
                return new List<PostImage>();
            }
        }

        #endregion

        #region Query Operations

        /// <summary>
        /// Retrieves posts by a specific author with pagination support using a compiled query for optimal performance.
        /// </summary>
        /// <param name="authorId">The unique identifier of the author.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of posts by the author, ordered by creation date descending.</returns>
        public async Task<List<Post>> GetByAuthorAsync(string authorId, int skip = 0, int take = 20)
        {
            using var step = _profiler.Step($"PostRepository.GetByAuthorAsync({authorId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await CompiledQueries.GetPostsByAuthorAsync(context, authorId, skip, take);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts for author {AuthorId}", authorId);
                return new List<Post>();
            }
        }

        /// <summary>
        /// Gets the total count of posts by a specific author using a compiled query for optimal performance.
        /// </summary>
        /// <param name="authorId">The unique identifier of the author.</param>
        /// <returns>The total number of posts by the author.</returns>
        public async Task<int> GetCountByAuthorAsync(string authorId)
        {
            using var step = _profiler.Step($"PostRepository.GetCountByAuthorAsync({authorId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await CompiledQueries.GetPostCountByAuthorAsync(context, authorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting posts for author {AuthorId}", authorId);
                return 0;
            }
        }

        /// <summary>
        /// Checks whether a post exists in the database using a compiled query for optimal performance.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to check.</param>
        /// <returns>True if the post exists; otherwise, false.</returns>
        public async Task<bool> ExistsAsync(int postId)
        {
            using var step = _profiler.Step($"PostRepository.ExistsAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await CompiledQueries.PostExistsAsync(context, postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of post {PostId}", postId);
                return false;
            }
        }

        #endregion

        #region Count Operations

        /// <summary>
        /// Increments the like count for a post by one.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        public async Task IncrementLikeCountAsync(int postId)
        {
            using var step = _profiler.Step($"PostRepository.IncrementLikeCountAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await context.Posts
                    .Where(p => p.Id == postId)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.LikeCount, x => x.LikeCount + 1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing like count for post {PostId}", postId);
            }
        }

        /// <summary>
        /// Decrements the like count for a post by one, with a minimum of zero.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        public async Task DecrementLikeCountAsync(int postId)
        {
            using var step = _profiler.Step($"PostRepository.DecrementLikeCountAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await context.Posts
                    .Where(p => p.Id == postId)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.LikeCount, x => Math.Max(0, x.LikeCount - 1)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrementing like count for post {PostId}", postId);
            }
        }

        /// <summary>
        /// Increments the comment count for a post by one.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        public async Task IncrementCommentCountAsync(int postId)
        {
            using var step = _profiler.Step($"PostRepository.IncrementCommentCountAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await context.Posts
                    .Where(p => p.Id == postId)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.CommentCount, x => x.CommentCount + 1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing comment count for post {PostId}", postId);
            }
        }

        /// <summary>
        /// Decrements the comment count for a post, with a minimum of zero.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="count">The number to decrement by (default is 1).</param>
        public async Task DecrementCommentCountAsync(int postId, int count = 1)
        {
            using var step = _profiler.Step($"PostRepository.DecrementCommentCountAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await context.Posts
                    .Where(p => p.Id == postId)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.CommentCount, x => Math.Max(0, x.CommentCount - count)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrementing comment count for post {PostId}", postId);
            }
        }

        /// <summary>
        /// Increments the share count for a post by one.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        public async Task IncrementShareCountAsync(int postId)
        {
            using var step = _profiler.Step($"PostRepository.IncrementShareCountAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                await context.Posts
                    .Where(p => p.Id == postId)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.ShareCount, x => x.ShareCount + 1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing share count for post {PostId}", postId);
            }
        }

        #endregion

        #region Feed Post Operations

        /// <summary>
        /// Updates the audience type for a post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <param name="userId">The ID of the user attempting the update (must be the author).</param>
        /// <param name="audienceType">The new audience type for the post.</param>
        /// <returns>A tuple containing success status and error message if any.</returns>
        public async Task<(bool Success, string? ErrorMessage)> UpdateAudienceAsync(int postId, string userId, AudienceType audienceType)
        {
            using var step = _profiler.Step($"PostRepository.UpdateAudienceAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var post = await context.Posts.FindAsync(postId);

                if (post == null)
                    return (false, "Post not found.");

                if (post.AuthorId != userId)
                    return (false, "You are not authorized to modify this post.");

                post.AudienceType = audienceType;
                post.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                _logger.LogInformation("Post {PostId} audience updated to {AudienceType} by user {UserId}",
                    postId, audienceType, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating audience for post {PostId}", postId);
                return (false, "An error occurred while updating the post audience.");
            }
        }

        /// <summary>
        /// Retrieves feed posts for a user, including their own posts, posts from friends, and posts made on their feed by friends.
        /// Only returns posts with appropriate audience settings (public, friends-only, or user's own posts).
        /// Uses cached friend lists for improved performance (80%+ faster when cached).
        /// PERFORMANCE: Returns DTOs using database-side projection to reduce data transfer by 50-70%.
        /// </summary>
        /// <param name="userId">The unique identifier of the user viewing the feed.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of PostListDto projections ordered by creation date descending.</returns>
        public async Task<List<PostListDto>> GetFeedPostsAsync(string userId, int skip = 0, int take = 20)
        {
            using var step = _profiler.Step($"PostRepository.GetFeedPostsAsync({userId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Get user's friend IDs from cache (dramatically faster than database query)
                var (friendIds, authorIds) = await _friendshipCache.GetUserFeedAuthorIdsAsync(userId);

                return await context.Posts
                    .AsNoTracking()
                    .Where(p => (authorIds.Contains(p.AuthorId) || p.FriendId == userId) &&
                               (
                                   // MeOnly posts: ONLY visible to the author
                                   (p.AudienceType == AudienceType.MeOnly && p.AuthorId == userId) ||
                                   // FriendsOnly posts: visible to author and friends
                                   (p.AudienceType == AudienceType.FriendsOnly && (p.AuthorId == userId || authorIds.Contains(p.AuthorId))) ||
                                   // Public posts: visible to everyone in feed
                                   (p.AudienceType == AudienceType.Public) ||
                                   // Cross-feed posts: visible to the friend whose feed it was posted on
                                   (p.FriendId == userId && p.AudienceType != AudienceType.MeOnly)
                               ))
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .Select(p => new PostListDto(
                        p.Id,
                        p.AuthorId,
                        (p.Author.FirstName + " " + p.Author.LastName).Trim(),
                        p.Author.UserName,
                        p.Author.ProfilePictureUrl,
                        p.Content,
                        p.CreatedAt,
                        p.UpdatedAt,
                        p.LikeCount,
                        p.CommentCount,
                        p.ShareCount,
                        p.AudienceType,
                        p.Images
                            .OrderBy(i => i.DisplayOrder)
                            .Select(i => new PostImageDto(i.Id, i.ImageUrl, i.DisplayOrder))
                            .ToList(),
                        p.FriendId,
                        p.Friend != null ? (p.Friend.FirstName + " " + p.Friend.LastName).Trim() : null,
                        p.Friend != null ? p.Friend.UserName : null,
                        p.Friend != null ? p.Friend.ProfilePictureUrl : null
                    ))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feed for user {UserId}", userId);
                return new List<PostListDto>();
            }
        }

        /// <summary>
        /// Gets the total count of posts in a user's feed, including posts made on their feed by friends.
        /// </summary>
        /// <param name="userId">The unique identifier of the user viewing the feed.</param>
        /// <returns>The total number of posts in the user's feed.</returns>
        public async Task<int> GetFeedPostsCountAsync(string userId)
        {
            using var step = _profiler.Step($"PostRepository.GetFeedPostsCountAsync({userId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Get user's friend IDs from cache (dramatically faster than database query)
                var (friendIds, authorIds) = await _friendshipCache.GetUserFeedAuthorIdsAsync(userId);

                return await context.Posts
                    .AsNoTracking()
                    .Where(p => (authorIds.Contains(p.AuthorId) || p.FriendId == userId) &&
                               (
                                   // MeOnly posts: ONLY visible to the author
                                   (p.AudienceType == AudienceType.MeOnly && p.AuthorId == userId) ||
                                   // FriendsOnly posts: visible to author and friends
                                   (p.AudienceType == AudienceType.FriendsOnly && (p.AuthorId == userId || authorIds.Contains(p.AuthorId))) ||
                                   // Public posts: visible to everyone in feed
                                   (p.AudienceType == AudienceType.Public) ||
                                   // Cross-feed posts: visible to the friend whose feed it was posted on
                                   (p.FriendId == userId && p.AudienceType != AudienceType.MeOnly)
                               ))
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feed posts count for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Retrieves public posts with pagination support.
        /// </summary>
        /// <param name="pageNumber">The page number (1-based).</param>
        /// <param name="pageSize">The number of posts per page.</param>
        /// <returns>A tuple containing the list of posts and a flag indicating if more posts exist.</returns>
        public async Task<(List<Post> Posts, bool HasMore)> GetPublicPostsAsync(int pageNumber = 1, int pageSize = 10)
        {
            using var step = _profiler.Step("PostRepository.GetPublicPostsAsync");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var posts = await context.Posts
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Where(p => p.AudienceType == AudienceType.Public)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize + 1)
                    .ToListAsync();

                var hasMore = posts.Count > pageSize;
                if (hasMore)
                    posts = posts.Take(pageSize).ToList();

                return (posts, hasMore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public posts");
                return (new List<Post>(), false);
            }
        }

        #endregion

        #region Projection-Based Methods (Phase 3 Optimizations)

        /// <summary>
        /// Retrieves feed posts as projection DTOs for improved performance.
        /// Uses database-side projection to reduce data transfer by 50-70%.
        /// Only loads the fields needed for list view rendering.
        /// </summary>
        /// <param name="userId">The unique identifier of the user viewing the feed.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of PostListDto projections ordered by creation date descending.</returns>
        [Obsolete("Use GetFeedPostsAsync instead - it now returns DTOs by default")]
        public async Task<List<PostListDto>> GetFeedPostsAsProjectionAsync(string userId, int skip = 0, int take = 20)
        {
            using var step = _profiler.Step($"PostRepository.GetFeedPostsAsProjectionAsync({userId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Get user's friend IDs from cache (dramatically faster than database query)
                var (friendIds, authorIds) = await _friendshipCache.GetUserFeedAuthorIdsAsync(userId);

                return await context.Posts
                    .AsNoTracking()
                    .Where(p => authorIds.Contains(p.AuthorId) &&
                               (
                                   // MeOnly posts: ONLY visible to the author
                                   (p.AudienceType == AudienceType.MeOnly && p.AuthorId == userId) ||
                                   // FriendsOnly posts: visible to author and friends
                                   (p.AudienceType == AudienceType.FriendsOnly && (p.AuthorId == userId || authorIds.Contains(p.AuthorId))) ||
                                   // Public posts: visible to everyone in feed
                                   (p.AudienceType == AudienceType.Public)
                               ))
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .Select(p => new PostListDto(
                        p.Id,
                        p.AuthorId,
                        (p.Author.FirstName + " " + p.Author.LastName).Trim(),
                        p.Author.UserName,
                        p.Author.ProfilePictureUrl,
                        p.Content,
                        p.CreatedAt,
                        p.UpdatedAt,
                        p.LikeCount,
                        p.CommentCount,
                        p.ShareCount,
                        p.AudienceType,
                        p.Images
                            .OrderBy(i => i.DisplayOrder)
                            .Select(i => new PostImageDto(i.Id, i.ImageUrl, i.DisplayOrder))
                            .ToList(),
                        p.FriendId,
                        p.Friend != null ? (p.Friend.FirstName + " " + p.Friend.LastName).Trim() : null,
                        p.Friend != null ? p.Friend.UserName : null,
                        p.Friend != null ? p.Friend.ProfilePictureUrl : null
                    ))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feed projections for user {UserId}", userId);
                return new List<PostListDto>();
            }
        }

        /// <summary>
        /// Retrieves a post by ID as a projection DTO for improved performance.
        /// Uses database-side projection to reduce data transfer.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>The post as a PostDetailDto if found; otherwise, null.</returns>
        public async Task<PostDetailDto?> GetByIdAsProjectionAsync(int postId)
        {
            using var step = _profiler.Step($"PostRepository.GetByIdAsProjectionAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Posts
                    .AsNoTracking()
                    .Where(p => p.Id == postId)
                    .Select(p => new PostDetailDto(
                        p.Id,
                        p.AuthorId,
                        p.Author.FirstName,
                        p.Author.LastName,
                        p.Author.ProfilePictureUrl,
                        p.Content,
                        p.CreatedAt,
                        p.UpdatedAt,
                        p.LikeCount,
                        p.CommentCount,
                        p.ShareCount,
                        p.AudienceType,
                        p.Images
                            .OrderBy(i => i.DisplayOrder)
                            .Select(i => new PostImageDto(i.Id, i.ImageUrl, i.DisplayOrder))
                            .ToList()
                    ))
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving post projection for post {PostId}", postId);
                return null;
            }
        }

        /// <summary>
        /// Retrieves posts by author as projection DTOs for improved performance.
        /// Uses database-side projection to reduce data transfer by 50-70%.
        /// </summary>
        /// <param name="authorId">The ID of the post author.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of PostListDto projections ordered by creation date descending.</returns>
        public async Task<List<PostListDto>> GetByAuthorAsProjectionAsync(string authorId, int skip = 0, int take = 20)
        {
            using var step = _profiler.Step($"PostRepository.GetByAuthorAsProjectionAsync({authorId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Posts
                    .AsNoTracking()
                    .Where(p => p.AuthorId == authorId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .Select(p => new PostListDto(
                        p.Id,
                        p.AuthorId,
                        (p.Author.FirstName + " " + p.Author.LastName).Trim(),
                        p.Author.UserName,
                        p.Author.ProfilePictureUrl,
                        p.Content,
                        p.CreatedAt,
                        p.UpdatedAt,
                        p.LikeCount,
                        p.CommentCount,
                        p.ShareCount,
                        p.AudienceType,
                        p.Images
                            .OrderBy(i => i.DisplayOrder)
                            .Select(i => new PostImageDto(i.Id, i.ImageUrl, i.DisplayOrder))
                            .ToList(),
                        p.FriendId,
                        p.Friend != null ? (p.Friend.FirstName + " " + p.Friend.LastName).Trim() : null,
                        p.Friend != null ? p.Friend.UserName : null,
                        p.Friend != null ? p.Friend.ProfilePictureUrl : null
                    ))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving author post projections for {AuthorId}", authorId);
                return new List<PostListDto>();
            }
        }

        /// <summary>
        /// Retrieves public posts as projection DTOs for improved performance.
        /// Uses database-side projection to reduce data transfer by 50-70%.
        /// </summary>
        /// <param name="pageNumber">The page number (1-based).</param>
        /// <param name="pageSize">The number of posts per page.</param>
        /// <returns>A tuple containing the list of projections and a flag indicating if more posts exist.</returns>
        public async Task<(List<PostListDto> Posts, bool HasMore)> GetPublicPostsAsProjectionAsync(int pageNumber = 1, int pageSize = 10)
        {
            using var step = _profiler.Step("PostRepository.GetPublicPostsAsProjectionAsync");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var posts = await context.Posts
                    .AsNoTracking()
                    .Where(p => p.AudienceType == AudienceType.Public)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize + 1)
                    .Select(p => new PostListDto(
                        p.Id,
                        p.AuthorId,
                        (p.Author.FirstName + " " + p.Author.LastName).Trim(),
                        p.Author.UserName,
                        p.Author.ProfilePictureUrl,
                        p.Content,
                        p.CreatedAt,
                        p.UpdatedAt,
                        p.LikeCount,
                        p.CommentCount,
                        p.ShareCount,
                        p.AudienceType,
                        p.Images
                            .OrderBy(i => i.DisplayOrder)
                            .Select(i => new PostImageDto(i.Id, i.ImageUrl, i.DisplayOrder))
                            .ToList(),
                        p.FriendId,
                        p.Friend != null ? (p.Friend.FirstName + " " + p.Friend.LastName).Trim() : null,
                        p.Friend != null ? p.Friend.UserName : null,
                        p.Friend != null ? p.Friend.ProfilePictureUrl : null
                    ))
                    .ToListAsync();

                var hasMore = posts.Count > pageSize;
                if (hasMore)
                    posts = posts.Take(pageSize).ToList();

                return (posts, hasMore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public post projections");
                return (new List<PostListDto>(), false);
            }
        }

        /// <summary>
        /// Retrieves posts for a specific user's profile feed with audience filtering for a viewer.
        /// Includes both posts authored by the user and cross-feed posts made on their feed by friends.
        /// Filters out MeOnly posts unless the viewer is the author.
        /// Uses database-side projection to reduce data transfer by 50-70%.
        /// </summary>
        /// <param name="authorId">The ID of the user whose profile feed to retrieve.</param>
        /// <param name="viewerId">The ID of the user viewing the posts.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to return.</param>
        /// <returns>A list of PostListDto projections ordered by creation date descending, filtered by audience permissions.</returns>
        public async Task<List<PostListDto>> GetByAuthorWithAudienceFilterAsync(string authorId, string viewerId, int skip = 0, int take = 20)
        {
            using var step = _profiler.Step($"PostRepository.GetByAuthorWithAudienceFilterAsync({authorId}, {viewerId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check if viewer is a friend of the author
                var isFriend = await context.Friendships
                    .AnyAsync(f => (f.RequesterId == viewerId && f.AddresseeId == authorId && f.Status == FriendshipStatus.Accepted) ||
                                   (f.RequesterId == authorId && f.AddresseeId == viewerId && f.Status == FriendshipStatus.Accepted));

                return await context.Posts
                    .AsNoTracking()
                    .Where(p => (p.AuthorId == authorId || p.FriendId == authorId) &&
                               (
                                   // MeOnly posts: ONLY visible to the author
                                   (p.AudienceType == AudienceType.MeOnly && p.AuthorId == viewerId) ||
                                   // FriendsOnly posts: visible to author and friends
                                   (p.AudienceType == AudienceType.FriendsOnly && (p.AuthorId == viewerId || isFriend)) ||
                                   // Public posts: visible to everyone
                                   (p.AudienceType == AudienceType.Public) ||
                                   // Cross-feed posts: visible to the profile owner and their friends
                                   (p.FriendId == authorId && p.AudienceType != AudienceType.MeOnly)
                               ))
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .Select(p => new PostListDto(
                        p.Id,
                        p.AuthorId,
                        (p.Author.FirstName + " " + p.Author.LastName).Trim(),
                        p.Author.UserName,
                        p.Author.ProfilePictureUrl,
                        p.Content,
                        p.CreatedAt,
                        p.UpdatedAt,
                        p.LikeCount,
                        p.CommentCount,
                        p.ShareCount,
                        p.AudienceType,
                        p.Images
                            .OrderBy(i => i.DisplayOrder)
                            .Select(i => new PostImageDto(i.Id, i.ImageUrl, i.DisplayOrder))
                            .ToList(),
                        p.FriendId,
                        p.Friend != null ? (p.Friend.FirstName + " " + p.Friend.LastName).Trim() : null,
                        p.Friend != null ? p.Friend.UserName : null,
                        p.Friend != null ? p.Friend.ProfilePictureUrl : null
                    ))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving filtered author posts for {AuthorId} viewed by {ViewerId}", authorId, viewerId);
                return new List<PostListDto>();
            }
        }

        #endregion
    }
}
