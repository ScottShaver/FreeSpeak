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
        // Group-specific like operations can be added here if needed
    }
}
