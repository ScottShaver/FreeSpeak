using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Generic repository interface for post like entities
    /// </summary>
    /// <typeparam name="TPost">The post entity type</typeparam>
    /// <typeparam name="TLike">The post like entity type</typeparam>
    public interface IPostLikeRepository<TPost, TLike>
        where TPost : class, IPostEntity
        where TLike : class, IPostLike
    {
        #region Like Operations

        /// <summary>
        /// Add or update a like on a post
        /// </summary>
        Task<(bool Success, string? ErrorMessage, TLike? Like)> AddOrUpdateAsync(int postId, string userId, LikeType likeType);

        /// <summary>
        /// Remove a like from a post
        /// </summary>
        Task<(bool Success, string? ErrorMessage)> RemoveAsync(int postId, string userId);

        /// <summary>
        /// Get the current user's like on a post (null if not liked)
        /// </summary>
        Task<TLike?> GetUserLikeAsync(int postId, string userId);

        /// <summary>
        /// Check if a user has liked a post
        /// </summary>
        Task<bool> HasUserLikedAsync(int postId, string userId);

        #endregion

        #region Query Operations

        /// <summary>
        /// Get all likes for a post
        /// </summary>
        Task<List<TLike>> GetByPostAsync(int postId);

        /// <summary>
        /// Get the count of likes for a post
        /// </summary>
        Task<int> GetCountAsync(int postId);

        /// <summary>
        /// Get like counts grouped by type
        /// </summary>
        Task<Dictionary<LikeType, int>> GetCountsByTypeAsync(int postId);

        /// <summary>
        /// Get users who liked a post
        /// </summary>
        Task<List<ApplicationUser>> GetLikedByUsersAsync(int postId, int skip = 0, int take = 20);

        /// <summary>
        /// Get posts liked by a user
        /// </summary>
        Task<List<int>> GetPostIdsLikedByUserAsync(string userId, int skip = 0, int take = 50);

        #endregion

        #region Batch Operations

        /// <summary>
        /// Get user's likes for multiple posts at once (for feed display)
        /// </summary>
        Task<Dictionary<int, TLike?>> GetUserLikesForPostsAsync(string userId, IEnumerable<int> postIds);

        #endregion
    }

    /// <summary>
    /// Generic repository interface for comment like entities
    /// </summary>
    /// <typeparam name="TComment">The comment entity type</typeparam>
    /// <typeparam name="TLike">The comment like entity type</typeparam>
    public interface ICommentLikeRepository<TComment, TLike>
        where TComment : class, IPostComment
        where TLike : class, ICommentLike
    {
        #region Like Operations

        /// <summary>
        /// Add or update a like on a comment
        /// </summary>
        Task<(bool Success, string? ErrorMessage, TLike? Like)> AddOrUpdateAsync(int commentId, string userId, LikeType likeType);

        /// <summary>
        /// Remove a like from a comment
        /// </summary>
        Task<(bool Success, string? ErrorMessage)> RemoveAsync(int commentId, string userId);

        /// <summary>
        /// Get the current user's like on a comment (null if not liked)
        /// </summary>
        Task<TLike?> GetUserLikeAsync(int commentId, string userId);

        /// <summary>
        /// Check if a user has liked a comment
        /// </summary>
        Task<bool> HasUserLikedAsync(int commentId, string userId);

        #endregion

        #region Query Operations

        /// <summary>
        /// Get all likes for a comment
        /// </summary>
        Task<List<TLike>> GetByCommentAsync(int commentId);

        /// <summary>
        /// Get the count of likes for a comment
        /// </summary>
        Task<int> GetCountAsync(int commentId);

        /// <summary>
        /// Get like counts grouped by type
        /// </summary>
        Task<Dictionary<LikeType, int>> GetCountsByTypeAsync(int commentId);

        #endregion

        #region Batch Operations

        /// <summary>
        /// Get user's likes for multiple comments at once (for feed display)
        /// </summary>
        Task<Dictionary<int, TLike?>> GetUserLikesForCommentsAsync(string userId, IEnumerable<int> commentIds);

        /// <summary>
        /// Get like counts for multiple comments at once (for feed display)
        /// Eliminates N+1 query problem when displaying comment lists
        /// </summary>
        /// <param name="commentIds">List of comment IDs to get counts for</param>
        /// <returns>Dictionary mapping comment ID to total like count</returns>
        Task<Dictionary<int, int>> GetCountsForCommentsAsync(IEnumerable<int> commentIds);

        /// <summary>
        /// Get reaction breakdowns for multiple comments at once (for feed display)
        /// Returns like counts grouped by reaction type for each comment
        /// </summary>
        /// <param name="commentIds">List of comment IDs to get breakdowns for</param>
        /// <returns>Dictionary mapping comment ID to its reaction breakdown</returns>
        Task<Dictionary<int, Dictionary<LikeType, int>>> GetReactionBreakdownsForCommentsAsync(IEnumerable<int> commentIds);

        #endregion
    }
}
