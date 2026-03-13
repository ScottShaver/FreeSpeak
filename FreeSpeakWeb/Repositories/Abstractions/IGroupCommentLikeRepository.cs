using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing likes/reactions on group post comments.
    /// Inherits all standard comment like operations from ICommentLikeRepository.
    /// Specialized for GroupPostCommentLike entities on GroupPostComment entities.
    /// </summary>
    public interface IGroupCommentLikeRepository : ICommentLikeRepository<GroupPostComment, GroupPostCommentLike>
    {
        // Group comment-specific like operations can be added here if needed
    }
}
