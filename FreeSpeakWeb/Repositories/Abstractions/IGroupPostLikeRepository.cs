using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for group post likes
    /// </summary>
    public interface IGroupPostLikeRepository : IPostLikeRepository<GroupPost, GroupPostLike>
    {
        // Group-specific like operations can be added here if needed
    }
}
