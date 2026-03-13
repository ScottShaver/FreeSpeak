using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing likes/reactions on feed post comments.
    /// Inherits all standard comment like operations from ICommentLikeRepository.
    /// Specialized for CommentLike entities on Comment entities in the main feed.
    /// </summary>
    public interface IFeedCommentLikeRepository : ICommentLikeRepository<Comment, CommentLike>
    {
        // Feed comment-specific like operations can be added here if needed
    }
}
