using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing likes/reactions on group posts.
    /// Inherits all standard like operations from IPostLikeRepository.
    /// Specialized for GroupPostLike entities on GroupPost entities.
    /// </summary>
    public interface IGroupPostLikeRepository : IPostLikeRepository<GroupPost, GroupPostLike>
    {
        /// <summary>
        /// Gets the count of likes grouped by reaction type for multiple group posts in a single query.
        /// </summary>
        /// <param name="postIds">The list of post identifiers to query.</param>
        /// <returns>A dictionary mapping post IDs to dictionaries of like types and their counts.</returns>
        Task<Dictionary<int, Dictionary<LikeType, int>>> GetCountsByTypeForPostsAsync(List<int> postIds);

        /// <summary>
        /// Gets user reactions for multiple group posts in a single query.
        /// </summary>
        /// <param name="postIds">The list of post identifiers to query.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A dictionary mapping post IDs to the user's reaction type (or null if no reaction).</returns>
        Task<Dictionary<int, LikeType?>> GetUserReactionsForPostsAsync(List<int> postIds, string userId);
    }
}
