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
        // Feed-specific like operations can be added here if needed
    }
}
