using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing comments on group posts.
    /// Extends ICommentRepository with group-specific comment operations.
    /// Specialized for GroupPostComment entities on GroupPost entities.
    /// </summary>
    public interface IGroupCommentRepository : ICommentRepository<GroupPost, GroupPostComment>
    {
        /// <summary>
        /// Retrieves comments from a specific group with pagination.
        /// Returns comments from all posts within the specified group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="skip">Number of comments to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of comments to retrieve. Default is 50.</param>
        /// <returns>A list of comments from the specified group.</returns>
        Task<List<GroupPostComment>> GetGroupCommentsAsync(int groupId, int skip = 0, int take = 50);

        /// <summary>
        /// Gets the total count of comments in a specific group.
        /// Counts comments across all posts within the group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>The total number of comments in the group.</returns>
        Task<int> GetGroupCommentCountAsync(int groupId);
    }
}
