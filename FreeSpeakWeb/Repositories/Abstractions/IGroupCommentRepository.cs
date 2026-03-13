using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for group post comments
    /// </summary>
    public interface IGroupCommentRepository : ICommentRepository<GroupPost, GroupPostComment>
    {
        /// <summary>
        /// Get comments for a specific group
        /// </summary>
        Task<List<GroupPostComment>> GetGroupCommentsAsync(int groupId, int skip = 0, int take = 50);

        /// <summary>
        /// Get comment count for a specific group
        /// </summary>
        Task<int> GetGroupCommentCountAsync(int groupId);
    }
}
