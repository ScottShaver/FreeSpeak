using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions;

/// <summary>
/// Repository for managing post notification mute preferences
/// </summary>
public interface IPostNotificationMuteRepository : IRepository<PostNotificationMute>
{
    /// <summary>
    /// Check if a user has muted notifications for a specific post
    /// </summary>
    Task<bool> IsPostMutedAsync(int postId, string userId);

    /// <summary>
    /// Get a mute record for a specific post and user
    /// </summary>
    Task<PostNotificationMute?> GetMuteRecordAsync(int postId, string userId);

    /// <summary>
    /// Get all mute records for a specific post
    /// </summary>
    Task<List<PostNotificationMute>> GetMuteRecordsByPostIdAsync(int postId);

    /// <summary>
    /// Get all posts a user has muted
    /// </summary>
    Task<List<PostNotificationMute>> GetUserMutedPostsAsync(string userId);

    /// <summary>
    /// Remove all mute records for a specific post
    /// </summary>
    Task RemoveMuteRecordsByPostIdAsync(int postId);
}
