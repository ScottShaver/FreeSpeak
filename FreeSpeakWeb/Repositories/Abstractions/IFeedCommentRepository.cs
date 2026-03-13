using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing comments on feed posts.
    /// Inherits all standard comment operations from ICommentRepository.
    /// Specialized for Comment entities on Post entities in the main social feed.
    /// </summary>
    public interface IFeedCommentRepository : ICommentRepository<Post, Comment>
    {
        // Feed-specific comment operations can be added here if needed
    }
}
