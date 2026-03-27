using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for feed post comments.
    /// Provides CRUD operations and queries for comments on feed posts.
    /// </summary>
    public class FeedCommentRepository : IFeedCommentRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<FeedCommentRepository> _logger;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ProfilerHelper _profiler;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedCommentRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        /// <param name="auditLogRepository">Repository for audit log operations.</param>
        /// <param name="profiler">Helper for profiling repository operations.</param>
        public FeedCommentRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<FeedCommentRepository> logger,
            IAuditLogRepository auditLogRepository,
            ProfilerHelper profiler)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _auditLogRepository = auditLogRepository;
            _profiler = profiler;
        }

        /// <summary>
        /// Retrieves a comment by its unique identifier.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="includeAuthor">Whether to include the author's information.</param>
        /// <param name="includeReplies">Whether to include replies to the comment.</param>
        /// <returns>The comment if found; otherwise, null.</returns>
        public async Task<Comment?> GetByIdAsync(int commentId, bool includeAuthor = true, bool includeReplies = false)
        {
            using var step = _profiler.Step($"FeedCommentRepository.GetByIdAsync({commentId})");
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

        /// <summary>
        /// Adds a new comment to a post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post to comment on.</param>
        /// <param name="authorId">The unique identifier of the comment author.</param>
        /// <param name="content">The text content of the comment.</param>
        /// <param name="imageUrl">Optional URL of an image attached to the comment.</param>
        /// <param name="parentCommentId">Optional ID of the parent comment if this is a reply.</param>
        /// <returns>A tuple containing success status, error message if any, and the created comment.</returns>
        public async Task<(bool Success, string? ErrorMessage, Comment? Comment)> AddAsync(
            int postId,
            string authorId,
            string content,
            string? imageUrl = null,
            int? parentCommentId = null)
        {
            using var step = _profiler.Step($"FeedCommentRepository.AddAsync(postId:{postId})");
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

        /// <summary>
        /// Updates the content of an existing comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment to update.</param>
        /// <param name="userId">The ID of the user attempting the update (must be the author).</param>
        /// <param name="newContent">The new content for the comment.</param>
        /// <returns>A tuple containing success status and error message if any.</returns>
        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(int commentId, string userId, string newContent)
        {
            using var step = _profiler.Step($"FeedCommentRepository.UpdateAsync({commentId})");
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

        /// <summary>
        /// Deletes a comment and all its nested replies recursively, updating the post's comment count.
        /// Also deletes all associated likes (handled by cascade delete in database).
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment to delete.</param>
        /// <param name="userId">The ID of the user attempting the deletion (must be comment author or post author).</param>
        /// <returns>A tuple containing success status, error message if any, and the count of deleted comments.</returns>
        public async Task<(bool Success, string? ErrorMessage, int DeletedCount)> DeleteAsync(int commentId, string userId)
        {
            using var step = _profiler.Step($"FeedCommentRepository.DeleteAsync({commentId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.Comments
                    .Include(c => c.Post)
                    .FirstOrDefaultAsync(c => c.Id == commentId);

                if (comment == null)
                    return (false, "Comment not found.", 0);

                // Check if user can delete (author or post author)
                if (comment.AuthorId != userId && comment.Post.AuthorId != userId)
                    return (false, "You are not authorized to delete this comment.", 0);

                // Store values for audit log before deletion
                var postId = comment.PostId;
                var parentCommentId = comment.ParentCommentId;

                // Collect all comment IDs to delete (parent + all nested replies)
                var commentsToDelete = await CollectCommentAndRepliesAsync(context, commentId);
                var totalDeletedCount = commentsToDelete.Count;

                // Delete all comments (EF Core will handle cascade for likes)
                context.Comments.RemoveRange(commentsToDelete);

                // Update post comment count
                comment.Post.CommentCount = Math.Max(0, comment.Post.CommentCount - totalDeletedCount);

                await context.SaveChangesAsync();

                // Log comment deletion to audit log
                try
                {
                    await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserComment, new UserCommentDetails
                    {
                        CommentId = commentId,
                        PostId = postId,
                        OperationType = OperationTypeEnum.Delete.ToString(),
                        ParentCommentId = parentCommentId
                    });
                }
                catch
                {
                    // Audit logging should not fail the operation
                }

                _logger.LogInformation("Comment {CommentId} and {ReplyCount} nested replies deleted by user {UserId}", 
                    commentId, totalDeletedCount - 1, userId);
                return (true, null, totalDeletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment {CommentId}", commentId);
                return (false, "An error occurred while deleting the comment.", 0);
            }
        }

        /// <summary>
        /// Recursively counts a comment and all its nested replies.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="commentId">The ID of the comment to count.</param>
        /// <returns>The total count including the comment itself and all nested replies.</returns>
        private async Task<int> CountCommentAndRepliesAsync(ApplicationDbContext context, int commentId)
        {
            int count = 1; // Count the comment itself

            var replyIds = await context.Comments
                .Where(c => c.ParentCommentId == commentId)
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var replyId in replyIds)
            {
                count += await CountCommentAndRepliesAsync(context, replyId);
            }

            return count;
        }

        /// <summary>
        /// Recursively collects a comment and all its nested replies for deletion.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="commentId">The ID of the root comment to collect.</param>
        /// <returns>A list of all comments to delete, including the root comment and all nested replies.</returns>
        private async Task<List<Comment>> CollectCommentAndRepliesAsync(ApplicationDbContext context, int commentId)
        {
            var result = new List<Comment>();

            var comment = await context.Comments.FindAsync(commentId);
            if (comment == null)
                return result;

            // First, recursively collect all replies
            var replyIds = await context.Comments
                .Where(c => c.ParentCommentId == commentId)
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var replyId in replyIds)
            {
                var nestedReplies = await CollectCommentAndRepliesAsync(context, replyId);
                result.AddRange(nestedReplies);
            }

            // Add the current comment last (so children are deleted first)
            result.Add(comment);

            return result;
        }

        /// <summary>
        /// Determines whether a user has permission to delete a specific comment.
        /// A user can delete a comment if they are the comment author or the post author.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The ID of the user to check permissions for.</param>
        /// <returns>True if the user can delete the comment; otherwise, false.</returns>
        public async Task<bool> CanUserDeleteAsync(int commentId, string userId)
        {
            using var step = _profiler.Step($"FeedCommentRepository.CanUserDeleteAsync({commentId})");
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

        /// <summary>
        /// Retrieves all top-level comments (non-replies) for a post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of top-level comments ordered by creation date descending.</returns>
        public async Task<List<Comment>> GetTopLevelCommentsAsync(int postId)
        {
            using var step = _profiler.Step($"FeedCommentRepository.GetTopLevelCommentsAsync({postId})");
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

        /// <summary>
        /// Retrieves all direct replies to a specific comment.
        /// </summary>
        /// <param name="parentCommentId">The unique identifier of the parent comment.</param>
        /// <returns>A list of replies ordered by creation date ascending.</returns>
        public async Task<List<Comment>> GetRepliesAsync(int parentCommentId)
        {
            using var step = _profiler.Step($"FeedCommentRepository.GetRepliesAsync({parentCommentId})");
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

        /// <summary>
        /// Retrieves multiple comments by their IDs in a single query.
        /// This batch method is useful for loading all comments in a tree structure efficiently.
        /// </summary>
        /// <param name="commentIds">Collection of comment IDs to retrieve.</param>
        /// <param name="includeAuthor">Whether to include author information.</param>
        /// <returns>A list of comments matching the provided IDs.</returns>
        public async Task<List<Comment>> GetByIdsAsync(IEnumerable<int> commentIds, bool includeAuthor = true)
        {
            using var step = _profiler.Step("FeedCommentRepository.GetByIdsAsync");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var commentIdList = commentIds.ToList();

                var query = context.Comments.Where(c => commentIdList.Contains(c.Id));

                if (includeAuthor)
                {
                    query = query.Include(c => c.Author);
                }

                return await query.OrderBy(c => c.CreatedAt).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comments by IDs");
                return new List<Comment>();
            }
        }

        /// <summary>
        /// Retrieves all comments for a post, including both top-level comments and replies.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>A list of all comments ordered by creation date ascending.</returns>
        public async Task<List<Comment>> GetAllCommentsAsync(int postId)
        {
            using var step = _profiler.Step($"FeedCommentRepository.GetAllCommentsAsync({postId})");
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

        /// <summary>
        /// Gets the total count of comments for a specific post.
        /// </summary>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>The total number of comments on the post.</returns>
        public async Task<int> GetCommentCountAsync(int postId)
        {
            using var step = _profiler.Step($"FeedCommentRepository.GetCommentCountAsync({postId})");
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

        /// <summary>
        /// Retrieves comments by a specific author with pagination support.
        /// </summary>
        /// <param name="authorId">The unique identifier of the author.</param>
        /// <param name="skip">Number of comments to skip for pagination.</param>
        /// <param name="take">Number of comments to return.</param>
        /// <returns>A list of comments by the author ordered by creation date descending.</returns>
        public async Task<List<Comment>> GetByAuthorAsync(string authorId, int skip = 0, int take = 20)
        {
            using var step = _profiler.Step($"FeedCommentRepository.GetByAuthorAsync({authorId})");
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

        /// <summary>
        /// Checks whether a comment exists in the database.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment to check.</param>
        /// <returns>True if the comment exists; otherwise, false.</returns>
        public async Task<bool> ExistsAsync(int commentId)
        {
            using var step = _profiler.Step($"FeedCommentRepository.ExistsAsync({commentId})");
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

        /// <summary>
        /// Calculates the nesting depth of a comment in the reply hierarchy.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>The depth level (0 for top-level comments, 1 for first-level replies, etc.).</returns>
        public async Task<int> GetDepthAsync(int commentId)
        {
            using var step = _profiler.Step($"FeedCommentRepository.GetDepthAsync({commentId})");
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

        /// <summary>
        /// Traverses up the comment hierarchy to find the top-level (root) comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>The root comment in the reply chain, or null if not found.</returns>
        public async Task<Comment?> GetRootCommentAsync(int commentId)
        {
            using var step = _profiler.Step($"FeedCommentRepository.GetRootCommentAsync({commentId})");
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

        /// <summary>
        /// Gets the post ID associated with a specific comment.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <returns>The post ID if found; otherwise, null.</returns>
        public async Task<int?> GetPostIdAsync(int commentId)
        {
            using var step = _profiler.Step($"FeedCommentRepository.GetPostIdAsync({commentId})");
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
