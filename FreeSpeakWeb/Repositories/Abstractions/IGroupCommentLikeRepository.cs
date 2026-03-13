using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for group comment likes
    /// </summary>
    public interface IGroupCommentLikeRepository : ICommentLikeRepository<GroupPostComment, GroupPostCommentLike>
    {
        // Group comment-specific like operations can be added here if needed
    }
}
