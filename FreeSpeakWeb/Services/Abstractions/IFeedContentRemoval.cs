namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// Removes feed content on behalf of a system moderator. Unlike author deletion, no ownership check is made
    /// and no user audit entry is written; the caller is responsible for authorization and auditing.
    /// </summary>
    public interface IFeedContentRemoval
    {
        /// <summary>
        /// Deletes a feed post and all of its related data.
        /// </summary>
        /// <param name="postId">The post ID.</param>
        /// <param name="moderatorId">The acting moderator's user ID (for logging).</param>
        /// <returns>A tuple containing success status and an error message if failed.</returns>
        Task<(bool Success, string? ErrorMessage)> RemovePostAsModeratorAsync(int postId, string moderatorId);

        /// <summary>
        /// Deletes a feed comment and all of its nested replies.
        /// </summary>
        /// <param name="commentId">The comment ID.</param>
        /// <param name="moderatorId">The acting moderator's user ID (for logging).</param>
        /// <returns>A tuple containing success status, an error message if failed, and the number of deleted comments.</returns>
        Task<(bool Success, string? ErrorMessage, int DeletedCount)> RemoveCommentAsModeratorAsync(int commentId, string moderatorId);
    }
}
