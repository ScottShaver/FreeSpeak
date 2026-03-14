using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing likes/reactions on feed posts.
    /// Inherits all standard like operations from IPostLikeRepository.
    /// Specialized for Like entities on Post entities in the main social feed.
    /// </summary>
    public interface IFeedPostLikeRepository : IPostLikeRepository<Post, Like>
    {
        /// <summary>
        /// Gets the count of likes grouped by reaction type for multiple posts in a single query.
        /// </summary>
        /// <param name="postIds">The list of post identifiers to query.</param>
        /// <returns>A dictionary mapping post IDs to dictionaries of like types and their counts.</returns>
        Task<Dictionary<int, Dictionary<LikeType, int>>> GetCountsByTypeForPostsAsync(List<int> postIds);

        /// <summary>
        /// Gets user reactions for multiple posts in a single query.
        /// </summary>
        /// <param name="postIds">The list of post identifiers to query.</param>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A dictionary mapping post IDs to the user's reaction type (or null if no reaction).</returns>
        Task<Dictionary<int, LikeType?>> GetUserReactionsForPostsAsync(List<int> postIds, string userId);
    }
}
