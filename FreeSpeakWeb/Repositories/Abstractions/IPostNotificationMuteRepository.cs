using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions;

/// <summary>
/// Repository interface for managing post notification mute preferences (PostNotificationMute entities).
/// Allows users to mute notifications for specific posts they no longer want to receive updates about.
/// Supports checking mute status and bulk operations for post cleanup.
/// </summary>
public interface IPostNotificationMuteRepository : IRepository<PostNotificationMute>
{
    /// <summary>
    /// Checks whether a user has muted notifications for a specific post.
    /// </summary>
    /// <param name="postId">The ID of the post.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>True if the user has muted notifications for the post; otherwise, false.</returns>
    Task<bool> IsPostMutedAsync(int postId, string userId);

    /// <summary>
    /// Retrieves the mute record for a specific post and user combination.
    /// </summary>
    /// <param name="postId">The ID of the post.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The mute record if found; otherwise, null.</returns>
    Task<PostNotificationMute?> GetMuteRecordAsync(int postId, string userId);

    /// <summary>
    /// Retrieves all mute records for a specific post.
    /// Shows all users who have muted notifications for a particular post.
    /// </summary>
    /// <param name="postId">The ID of the post.</param>
    /// <returns>A list of all mute records for the specified post.</returns>
    Task<List<PostNotificationMute>> GetMuteRecordsByPostIdAsync(int postId);

    /// <summary>
    /// Retrieves all posts that a specific user has muted.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A list of mute records for the user.</returns>
    Task<List<PostNotificationMute>> GetUserMutedPostsAsync(string userId);

    /// <summary>
    /// Removes all mute records for a specific post.
    /// Called when a post is deleted to clean up associated mute preferences.
    /// </summary>
    /// <param name="postId">The ID of the post to remove mutes for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveMuteRecordsByPostIdAsync(int postId);
}
