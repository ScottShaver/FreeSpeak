using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for feed post comments
    /// </summary>
    public interface IFeedCommentRepository : ICommentRepository<Post, Comment>
    {
        // Feed-specific comment operations can be added here if needed
    }
}
