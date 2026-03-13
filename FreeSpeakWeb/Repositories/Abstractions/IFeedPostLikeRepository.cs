using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for feed post likes
    /// </summary>
    public interface IFeedPostLikeRepository : IPostLikeRepository<Post, Like>
    {
        // Feed-specific like operations can be added here if needed
    }
}
