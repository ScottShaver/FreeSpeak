using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions;

/// <summary>
/// Repository for managing pinned posts
/// </summary>
public interface IPinnedPostRepository : IRepository<PinnedPost>
{
    /// <summary>
    /// Check if a user has pinned a specific post
    /// </summary>
    Task<bool> IsPostPinnedAsync(int postId, string userId);

    /// <summary>
    /// Get a pinned post by user and post IDs
    /// </summary>
    Task<PinnedPost?> GetPinnedPostAsync(int postId, string userId);

    /// <summary>
    /// Get all pinned posts for a specific post ID
    /// </summary>
    Task<List<PinnedPost>> GetPinnedPostsByPostIdAsync(int postId);

    /// <summary>
    /// Get all pinned posts for a specific user
    /// </summary>
    Task<List<PinnedPost>> GetUserPinnedPostsAsync(string userId);

    /// <summary>
    /// Remove all pinned post records for a specific post
    /// </summary>
    Task RemovePinnedPostsByPostIdAsync(int postId);
}
