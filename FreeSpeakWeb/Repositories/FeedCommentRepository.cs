using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for feed post comments
    /// </summary>
    public class FeedCommentRepository : IFeedCommentRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<FeedCommentRepository> _logger;

        public FeedCommentRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FeedCommentRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<Comment?> GetByIdAsync(int commentId, bool includeAuthor = true, bool includeReplies = false)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var query = context.Comments.AsQueryable();

                if (includeAuthor)
                    query = query.Include(c => c.Author);

                if (includeReplies)
                    query = query.Include(c => c.Replies).ThenInclude(r => r.Author);

                return await query.FirstOrDefaultAsync(c => c.Id == commentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comment {CommentId}", commentId);
                return null;
            }
        }

        public async Task<(bool Success, string? ErrorMessage, Comment? Comment)> AddAsync(
            int postId,
            string authorId,
            string content,
            string? imageUrl = null,
            int? parentCommentId = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify post exists
                var postExists = await context.Posts.AnyAsync(p => p.Id == postId);
                if (!postExists)
                    return (false, "Post not found.", null);

                // Verify parent comment exists if provided
                if (parentCommentId.HasValue)
                {
                    var parentExists = await context.Comments.AnyAsync(c => c.Id == parentCommentId.Value);
                    if (!parentExists)
                        return (false, "Parent comment not found.", null);
                }

                var comment = new Comment
                {
                    PostId = postId,
                    AuthorId = authorId,
                    Content = content,
                    ImageUrl = imageUrl,
                    ParentCommentId = parentCommentId,
                    CreatedAt = DateTime.UtcNow
                };

                context.Comments.Add(comment);
                await context.SaveChangesAsync();

                _logger.LogInformation("Comment {CommentId} added to post {PostId} by user {AuthorId}",
                    comment.Id, postId, authorId);

                return (true, null, comment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment to post {PostId}", postId);
                return (false, "An error occurred while adding the comment.", null);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(int commentId, string userId, string newContent)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.Comments.FindAsync(commentId);
                if (comment == null)
                    return (false, "Comment not found.");

                if (comment.AuthorId != userId)
                    return (false, "You are not authorized to edit this comment.");

                comment.Content = newContent;
                await context.SaveChangesAsync();

                _logger.LogInformation("Comment {CommentId} updated by user {UserId}", commentId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating comment {CommentId}", commentId);
                return (false, "An error occurred while updating the comment.");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.Comments
                    .Include(c => c.Post)
                    .Include(c => c.Replies)
                    .FirstOrDefaultAsync(c => c.Id == commentId);

                if (comment == null)
                    return (false, "Comment not found.");

                // Check if user can delete (author or post author)
                if (comment.AuthorId != userId && comment.Post.AuthorId != userId)
                    return (false, "You are not authorized to delete this comment.");

                // Delete recursively (all replies)
                await DeleteCommentRecursivelyAsync(context, comment);
                await context.SaveChangesAsync();

                _logger.LogInformation("Comment {CommentId} and its replies deleted by user {UserId}", commentId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment {CommentId}", commentId);
                return (false, "An error occurred while deleting the comment.");
            }
        }

        private async Task DeleteCommentRecursivelyAsync(ApplicationDbContext context, Comment comment)
        {
            // Load all replies if not loaded
            if (!context.Entry(comment).Collection(c => c.Replies).IsLoaded)
            {
                await context.Entry(comment).Collection(c => c.Replies).LoadAsync();
            }

            // Recursively delete all replies
            foreach (var reply in comment.Replies.ToList())
            {
                await DeleteCommentRecursivelyAsync(context, reply);
            }

            context.Comments.Remove(comment);
        }

        public async Task<bool> CanUserDeleteAsync(int commentId, string userId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.Comments
                    .Include(c => c.Post)
                    .FirstOrDefaultAsync(c => c.Id == commentId);

                if (comment == null)
                    return false;

                return comment.AuthorId == userId || comment.Post.AuthorId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking delete permission for comment {CommentId}", commentId);
                return false;
            }
        }

        public async Task<List<Comment>> GetTopLevelCommentsAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Comments
                    .Include(c => c.Author)
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top-level comments for post {PostId}", postId);
                return new List<Comment>();
            }
        }

        public async Task<List<Comment>> GetRepliesAsync(int parentCommentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Comments
                    .Include(c => c.Author)
                    .Where(c => c.ParentCommentId == parentCommentId)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving replies for comment {ParentCommentId}", parentCommentId);
                return new List<Comment>();
            }
        }

        public async Task<List<Comment>> GetAllCommentsAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Comments
                    .Include(c => c.Author)
                    .Where(c => c.PostId == postId)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all comments for post {PostId}", postId);
                return new List<Comment>();
            }
        }

        public async Task<int> GetCommentCountAsync(int postId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Comments.CountAsync(c => c.PostId == postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting comments for post {PostId}", postId);
                return 0;
            }
        }

        public async Task<List<Comment>> GetByAuthorAsync(string authorId, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.Comments
                    .Include(c => c.Author)
                    .Include(c => c.Post)
                    .Where(c => c.AuthorId == authorId)
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comments by author {AuthorId}", authorId);
                return new List<Comment>();
            }
        }

        public async Task<bool> ExistsAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Comments.AnyAsync(c => c.Id == commentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of comment {CommentId}", commentId);
                return false;
            }
        }

        public async Task<int> GetDepthAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.Comments.FindAsync(commentId);
                if (comment == null)
                    return 0;

                int depth = 0;
                var currentId = comment.ParentCommentId;

                while (currentId.HasValue)
                {
                    depth++;
                    var parent = await context.Comments.FindAsync(currentId.Value);
                    currentId = parent?.ParentCommentId;
                }

                return depth;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating depth for comment {CommentId}", commentId);
                return 0;
            }
        }

        public async Task<Comment?> GetRootCommentAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.Comments.FindAsync(commentId);
                if (comment == null)
                    return null;

                while (comment.ParentCommentId.HasValue)
                {
                    comment = await context.Comments.FindAsync(comment.ParentCommentId.Value);
                    if (comment == null)
                        return null;
                }

                return comment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding root comment for comment {CommentId}", commentId);
                return null;
            }
        }

        public async Task<int?> GetPostIdAsync(int commentId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.Comments.FindAsync(commentId);
                return comment?.PostId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving post ID for comment {CommentId}", commentId);
                return null;
            }
        }
    }
}
