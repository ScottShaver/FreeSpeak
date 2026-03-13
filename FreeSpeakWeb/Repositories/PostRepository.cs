using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.Abstractions;
using FreeSpeakWeb.Repositories.Abstractions;
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

        public PostRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<PostRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        #region Post CRUD Operations

        public async Task<Post?> GetByIdAsync(int postId, bool includeAuthor = true, bool includeImages = true)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.Posts.AsQueryable();

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

        public async Task<(bool Success, string? ErrorMessage, Post? Post)> CreateAsync(Post post)
        {
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

        public async Task<(bool Success, string? ErrorMessage)> UpdateContentAsync(int postId, string userId, string newContent)
        {
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

        public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(int postId, string userId)
        {
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

        public async Task<bool> CanUserDeleteAsync(int postId, string userId)
        {
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

        public async Task<List<PostImage>> GetImagesAsync(int postId)
        {
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

        public async Task<List<Post>> GetByAuthorAsync(string authorId, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Posts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Where(p => p.AuthorId == authorId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts for author {AuthorId}", authorId);
                return new List<Post>();
            }
        }

        public async Task<int> GetCountByAuthorAsync(string authorId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Posts.CountAsync(p => p.AuthorId == authorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting posts for author {AuthorId}", authorId);
                return 0;
            }
        }

        public async Task<bool> ExistsAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Posts.AnyAsync(p => p.Id == postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of post {PostId}", postId);
                return false;
            }
        }

        #endregion

        #region Count Operations

        public async Task IncrementLikeCountAsync(int postId)
        {
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

        public async Task DecrementLikeCountAsync(int postId)
        {
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

        public async Task IncrementCommentCountAsync(int postId)
        {
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

        public async Task DecrementCommentCountAsync(int postId, int count = 1)
        {
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

        public async Task IncrementShareCountAsync(int postId)
        {
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

        public async Task<(bool Success, string? ErrorMessage)> UpdateAudienceAsync(int postId, string userId, AudienceType audienceType)
        {
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

        public async Task<List<Post>> GetFeedPostsAsync(string userId, int skip = 0, int take = 20)
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

                return await context.Posts
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Where(p => authorIds.Contains(p.AuthorId) &&
                               (p.AuthorId == userId ||
                                p.AudienceType == AudienceType.Public ||
                                p.AudienceType == AudienceType.FriendsOnly))
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feed for user {UserId}", userId);
                return new List<Post>();
            }
        }

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
                    .Where(p => authorIds.Contains(p.AuthorId) &&
                               (p.AuthorId == userId ||
                                p.AudienceType == AudienceType.Public ||
                                p.AudienceType == AudienceType.FriendsOnly))
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feed posts count for user {UserId}", userId);
                return 0;
            }
        }

        public async Task<(List<Post> Posts, bool HasMore)> GetPublicPostsAsync(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var posts = await context.Posts
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
    }
}
