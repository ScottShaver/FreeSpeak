using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for feed comment likes
    /// </summary>
    public interface IFeedCommentLikeRepository : ICommentLikeRepository<Comment, CommentLike>
    {
        // Feed comment-specific like operations can be added here if needed
    }
}
