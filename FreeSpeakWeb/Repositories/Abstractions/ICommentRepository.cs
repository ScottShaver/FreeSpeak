using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Generic repository interface for comment entities
    /// </summary>
    /// <typeparam name="TPost">The post entity type</typeparam>
    /// <typeparam name="TComment">The comment entity type</typeparam>
    public interface ICommentRepository<TPost, TComment>
        where TPost : class, IPostEntity
        where TComment : class, IPostComment
    {
        #region Comment CRUD Operations

        /// <summary>
        /// Get a comment by its ID
        /// </summary>
        Task<TComment?> GetByIdAsync(int commentId, bool includeAuthor = true, bool includeReplies = false);

        /// <summary>
        /// Add a new comment to a post
        /// </summary>
        Task<(bool Success, string? ErrorMessage, TComment? Comment)> AddAsync(
            int postId,
            string authorId,
            string content,
            string? imageUrl = null,
            int? parentCommentId = null);

        /// <summary>
        /// Update an existing comment
        /// </summary>
        Task<(bool Success, string? ErrorMessage)> UpdateAsync(int commentId, string userId, string newContent);

        /// <summary>
        /// Delete a comment and all its nested replies, returning the total count of deleted comments.
        /// Also updates the post's comment count in the database.
        /// </summary>
        /// <param name="commentId">The ID of the comment to delete.</param>
        /// <param name="userId">The ID of the user attempting the deletion.</param>
        /// <returns>A tuple with success status, error message if any, and the count of deleted comments (including the main comment and all nested replies).</returns>
        Task<(bool Success, string? ErrorMessage, int DeletedCount)> DeleteAsync(int commentId, string userId);

        /// <summary>
        /// Check if a user can delete a specific comment
        /// </summary>
        Task<bool> CanUserDeleteAsync(int commentId, string userId);

        #endregion

        #region Query Operations

        /// <summary>
        /// Get top-level comments for a post (not replies)
        /// </summary>
        Task<List<TComment>> GetTopLevelCommentsAsync(int postId);

        /// <summary>
        /// Get replies to a specific comment
        /// </summary>
        Task<List<TComment>> GetRepliesAsync(int parentCommentId);

        /// <summary>
        /// Get all comments for a post (including replies) in a flat structure
        /// </summary>
        Task<List<TComment>> GetAllCommentsAsync(int postId);

        /// <summary>
        /// Get the count of comments for a post
        /// </summary>
        Task<int> GetCommentCountAsync(int postId);

        /// <summary>
        /// Get comments by author
        /// </summary>
        Task<List<TComment>> GetByAuthorAsync(string authorId, int skip = 0, int take = 20);

        /// <summary>
        /// Check if a comment exists
        /// </summary>
        Task<bool> ExistsAsync(int commentId);

        /// <summary>
        /// Get multiple comments by their IDs in a single query.
        /// Useful for batch loading comments when building comment trees.
        /// </summary>
        /// <param name="commentIds">Collection of comment IDs to retrieve.</param>
        /// <param name="includeAuthor">Whether to include author information.</param>
        /// <returns>A list of comments matching the provided IDs.</returns>
        Task<List<TComment>> GetByIdsAsync(IEnumerable<int> commentIds, bool includeAuthor = true);

        #endregion

        #region Hierarchy Operations

        /// <summary>
        /// Get the depth of a comment in the reply hierarchy
        /// </summary>
        Task<int> GetDepthAsync(int commentId);

        /// <summary>
        /// Get the root comment of a reply chain
        /// </summary>
        Task<TComment?> GetRootCommentAsync(int commentId);

        /// <summary>
        /// Get the post ID for a comment (useful for nested replies)
        /// </summary>
        Task<int?> GetPostIdAsync(int commentId);

        #endregion
    }
}
