namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// Removes group posts and comments on behalf of a moderator. The implementation enforces that the caller is
    /// authorized to moderate the group (group admin or moderator, system moderator or system administrator).
    /// </summary>
    public interface IGroupContentRemoval
    {
        /// <summary>
        /// Deletes a group post together with its comments, images and related records.
        /// </summary>
        /// <param name="postId">The post to delete.</param>
        /// <param name="moderatorId">The acting moderator's user ID.</param>
        /// <returns>Whether the post was deleted, and an error message when it was not.</returns>
        Task<(bool Success, string? ErrorMessage)> RemovePostAsModeratorAsync(int postId, string moderatorId);

        /// <summary>
        /// Deletes a group comment together with its replies.
        /// </summary>
        /// <param name="commentId">The comment to delete.</param>
        /// <param name="moderatorId">The acting moderator's user ID.</param>
        /// <returns>Whether the comment was deleted, and an error message when it was not.</returns>
        Task<(bool Success, string? ErrorMessage)> RemoveCommentAsModeratorAsync(int commentId, string moderatorId);
    }
}
