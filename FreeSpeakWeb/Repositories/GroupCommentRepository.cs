using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for group post comments
    /// </summary>
    public class GroupCommentRepository : IGroupCommentRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupCommentRepository> _logger;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ProfilerHelper _profiler;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupCommentRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        /// <param name="auditLogRepository">Repository for audit log operations.</param>
        /// <param name="profiler">Helper for profiling repository operations.</param>
        public GroupCommentRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupCommentRepository> logger,
            IAuditLogRepository auditLogRepository,
            ProfilerHelper profiler)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _auditLogRepository = auditLogRepository;
            _profiler = profiler;
        }

        public async Task<GroupPostComment?> GetByIdAsync(int commentId, bool includeAuthor = true, bool includeReplies = false)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetByIdAsync({commentId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var query = context.GroupPostComments.AsQueryable();

                if (includeAuthor)
                    query = query.Include(c => c.Author);

                if (includeReplies)
                    query = query.Include(c => c.Replies).ThenInclude(r => r.Author);

                return await query.FirstOrDefaultAsync(c => c.Id == commentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group comment {CommentId}", commentId);
                return null;
            }
        }

        public async Task<(bool Success, string? ErrorMessage, GroupPostComment? Comment)> AddAsync(
            int postId,
            string authorId,
            string content,
            string? imageUrl = null,
            int? parentCommentId = null)
        {
            using var step = _profiler.Step($"GroupCommentRepository.AddAsync(postId:{postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Verify post exists
                var postExists = await context.GroupPosts.AnyAsync(p => p.Id == postId);
                if (!postExists)
                    return (false, "Post not found.", null);

                // Verify parent comment exists if provided
                if (parentCommentId.HasValue)
                {
                    var parentExists = await context.GroupPostComments.AnyAsync(c => c.Id == parentCommentId.Value);
                    if (!parentExists)
                        return (false, "Parent comment not found.", null);
                }

                var comment = new GroupPostComment
                {
                    PostId = postId,
                    AuthorId = authorId,
                    Content = content,
                    ImageUrl = imageUrl,
                    ParentCommentId = parentCommentId,
                    CreatedAt = DateTime.UtcNow
                };

                context.GroupPostComments.Add(comment);
                await context.SaveChangesAsync();

                _logger.LogInformation("Group comment {CommentId} added to post {PostId} by user {AuthorId}",
                    comment.Id, postId, authorId);

                return (true, null, comment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment to group post {PostId}", postId);
                return (false, "An error occurred while adding the comment.", null);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(int commentId, string userId, string newContent)
        {
            using var step = _profiler.Step($"GroupCommentRepository.UpdateAsync({commentId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.GroupPostComments.FindAsync(commentId);
                if (comment == null)
                    return (false, "Comment not found.");

                if (comment.AuthorId != userId)
                    return (false, "You are not authorized to edit this comment.");

                comment.Content = newContent;
                await context.SaveChangesAsync();

                _logger.LogInformation("Group comment {CommentId} updated by user {UserId}", commentId, userId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group comment {CommentId}", commentId);
                return (false, "An error occurred while updating the comment.");
            }
        }

        /// <summary>
        /// Deletes a group comment and all its nested replies recursively, updating the post's comment count.
        /// Also deletes all associated likes (handled by cascade delete in database).
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment to delete.</param>
        /// <param name="userId">The ID of the user attempting the deletion (must be comment author or post author).</param>
        /// <returns>A tuple containing success status, error message if any, and the count of deleted comments.</returns>
        public async Task<(bool Success, string? ErrorMessage, int DeletedCount)> DeleteAsync(int commentId, string userId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.DeleteAsync({commentId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.GroupPostComments
                    .Include(c => c.Post)
                    .FirstOrDefaultAsync(c => c.Id == commentId);

                if (comment == null)
                    return (false, "Comment not found.", 0);

                // Check if user can delete (author or post author)
                if (comment.AuthorId != userId && comment.Post.AuthorId != userId)
                    return (false, "You are not authorized to delete this comment.", 0);

                // Store values for audit log before deletion
                var postId = comment.PostId;
                var groupId = comment.Post.GroupId;
                var parentCommentId = comment.ParentCommentId;

                // Collect all comment IDs to delete (parent + all nested replies)
                var commentsToDelete = await CollectCommentAndRepliesAsync(context, commentId);
                var totalDeletedCount = commentsToDelete.Count;

                // Delete all comments (EF Core will handle cascade for likes)
                context.GroupPostComments.RemoveRange(commentsToDelete);

                // Update post comment count
                comment.Post.CommentCount = Math.Max(0, comment.Post.CommentCount - totalDeletedCount);

                await context.SaveChangesAsync();

                // Log group comment deletion to audit log
                try
                {
                    await _auditLogRepository.LogActionAsync(userId, ActionCategory.UserComment, new UserGroupCommentDetails
                    {
                        CommentId = commentId,
                        PostId = postId,
                        GroupId = groupId,
                        OperationType = OperationTypeEnum.Delete.ToString(),
                        ParentCommentId = parentCommentId
                    });
                }
                catch
                {
                    // Audit logging should not fail the operation
                }

                _logger.LogInformation("Group comment {CommentId} and {ReplyCount} nested replies deleted by user {UserId}", 
                    commentId, totalDeletedCount - 1, userId);
                return (true, null, totalDeletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group comment {CommentId}", commentId);
                return (false, "An error occurred while deleting the comment.", 0);
            }
        }

        /// <summary>
        /// Recursively counts a group comment and all its nested replies.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="commentId">The ID of the comment to count.</param>
        /// <returns>The total count including the comment itself and all nested replies.</returns>
        private async Task<int> CountCommentAndRepliesAsync(ApplicationDbContext context, int commentId)
        {
            int count = 1; // Count the comment itself

            var replyIds = await context.GroupPostComments
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
        /// Recursively collects a group comment and all its nested replies for deletion.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="commentId">The ID of the root comment to collect.</param>
        /// <returns>A list of all comments to delete, including the root comment and all nested replies.</returns>
        private async Task<List<GroupPostComment>> CollectCommentAndRepliesAsync(ApplicationDbContext context, int commentId)
        {
            var result = new List<GroupPostComment>();

            var comment = await context.GroupPostComments.FindAsync(commentId);
            if (comment == null)
                return result;

            // First, recursively collect all replies
            var replyIds = await context.GroupPostComments
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
        /// Determines whether a user has permission to delete a specific group comment.
        /// A user can delete a comment if they are the comment author or the post author.
        /// </summary>
        /// <param name="commentId">The unique identifier of the comment.</param>
        /// <param name="userId">The ID of the user to check permissions for.</param>
        /// <returns>True if the user can delete the comment; otherwise, false.</returns>
        public async Task<bool> CanUserDeleteAsync(int commentId, string userId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.CanUserDeleteAsync({commentId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.GroupPostComments
                    .Include(c => c.Post)
                    .FirstOrDefaultAsync(c => c.Id == commentId);

                if (comment == null)
                    return false;

                return comment.AuthorId == userId || comment.Post.AuthorId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking delete permission for group comment {CommentId}", commentId);
                return false;
            }
        }

        public async Task<List<GroupPostComment>> GetTopLevelCommentsAsync(int postId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetTopLevelCommentsAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPostComments
                    .Include(c => c.Author)
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top-level comments for group post {PostId}", postId);
                return new List<GroupPostComment>();
            }
        }

        public async Task<List<GroupPostComment>> GetRepliesAsync(int parentCommentId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetRepliesAsync({parentCommentId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPostComments
                    .Include(c => c.Author)
                    .Where(c => c.ParentCommentId == parentCommentId)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving replies for group comment {ParentCommentId}", parentCommentId);
                return new List<GroupPostComment>();
            }
        }

        /// <summary>
        /// Retrieves multiple group post comments by their IDs in a single query.
        /// This batch method is useful for loading all comments in a tree structure efficiently.
        /// </summary>
        /// <param name="commentIds">Collection of comment IDs to retrieve.</param>
        /// <param name="includeAuthor">Whether to include author information.</param>
        /// <returns>A list of comments matching the provided IDs.</returns>
        public async Task<List<GroupPostComment>> GetByIdsAsync(IEnumerable<int> commentIds, bool includeAuthor = true)
        {
            using var step = _profiler.Step("GroupCommentRepository.GetByIdsAsync");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var commentIdList = commentIds.ToList();

                var query = context.GroupPostComments.Where(c => commentIdList.Contains(c.Id));

                if (includeAuthor)
                {
                    query = query.Include(c => c.Author);
                }

                return await query.OrderBy(c => c.CreatedAt).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group comments by IDs");
                return new List<GroupPostComment>();
            }
        }

        public async Task<List<GroupPostComment>> GetAllCommentsAsync(int postId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetAllCommentsAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPostComments
                    .Include(c => c.Author)
                    .Where(c => c.PostId == postId)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all comments for group post {PostId}", postId);
                return new List<GroupPostComment>();
            }
        }

        public async Task<int> GetCommentCountAsync(int postId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetCommentCountAsync({postId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostComments.CountAsync(c => c.PostId == postId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting comments for group post {PostId}", postId);
                return 0;
            }
        }

        public async Task<List<GroupPostComment>> GetByAuthorAsync(string authorId, int skip = 0, int take = 20)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetByAuthorAsync({authorId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPostComments
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
                _logger.LogError(ex, "Error retrieving group comments by author {AuthorId}", authorId);
                return new List<GroupPostComment>();
            }
        }

        public async Task<bool> ExistsAsync(int commentId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.ExistsAsync({commentId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupPostComments.AnyAsync(c => c.Id == commentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of group comment {CommentId}", commentId);
                return false;
            }
        }

        public async Task<int> GetDepthAsync(int commentId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetDepthAsync({commentId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.GroupPostComments.FindAsync(commentId);
                if (comment == null)
                    return 0;

                int depth = 0;
                var currentId = comment.ParentCommentId;

                while (currentId.HasValue)
                {
                    depth++;
                    var parent = await context.GroupPostComments.FindAsync(currentId.Value);
                    currentId = parent?.ParentCommentId;
                }

                return depth;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating depth for group comment {CommentId}", commentId);
                return 0;
            }
        }

        public async Task<GroupPostComment?> GetRootCommentAsync(int commentId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetRootCommentAsync({commentId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.GroupPostComments.FindAsync(commentId);
                if (comment == null)
                    return null;

                while (comment.ParentCommentId.HasValue)
                {
                    comment = await context.GroupPostComments.FindAsync(comment.ParentCommentId.Value);
                    if (comment == null)
                        return null;
                }

                return comment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding root comment for group comment {CommentId}", commentId);
                return null;
            }
        }

        public async Task<int?> GetPostIdAsync(int commentId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetPostIdAsync({commentId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var comment = await context.GroupPostComments.FindAsync(commentId);
                return comment?.PostId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving post ID for group comment {CommentId}", commentId);
                return null;
            }
        }

        public async Task<List<GroupPostComment>> GetGroupCommentsAsync(int groupId, int skip = 0, int take = 50)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetGroupCommentsAsync({groupId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPostComments
                    .Include(c => c.Author)
                    .Include(c => c.Post)
                    .Where(c => c.Post.GroupId == groupId)
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comments for group {GroupId}", groupId);
                return new List<GroupPostComment>();
            }
        }

        public async Task<int> GetGroupCommentCountAsync(int groupId)
        {
            using var step = _profiler.Step($"GroupCommentRepository.GetGroupCommentCountAsync({groupId})");
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                return await context.GroupPostComments
                    .Where(c => c.Post.GroupId == groupId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting comments for group {GroupId}", groupId);
                return 0;
            }
        }
    }
}
