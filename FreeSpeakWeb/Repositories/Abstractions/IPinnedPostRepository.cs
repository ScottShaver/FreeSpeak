using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions;

/// <summary>
/// Repository interface for managing pinned posts (PinnedPost entities).
/// Allows users to pin posts to their profile or feed for easy access.
/// Supports checking pin status and bulk operations for post cleanup.
/// </summary>
public interface IPinnedPostRepository : IRepository<PinnedPost>
{
    /// <summary>
    /// Checks whether a specific user has pinned a specific post.
    /// </summary>
    /// <param name="postId">The ID of the post.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>True if the user has pinned the post; otherwise, false.</returns>
    Task<bool> IsPostPinnedAsync(int postId, string userId);

    /// <summary>
    /// Retrieves the pinned post record for a specific user and post combination.
    /// </summary>
    /// <param name="postId">The ID of the post.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The pinned post record if found; otherwise, null.</returns>
    Task<PinnedPost?> GetPinnedPostAsync(int postId, string userId);

    /// <summary>
    /// Retrieves all pinned post records for a specific post.
    /// Shows all users who have pinned a particular post.
    /// </summary>
    /// <param name="postId">The ID of the post.</param>
    /// <returns>A list of all pinned post records for the specified post.</returns>
    Task<List<PinnedPost>> GetPinnedPostsByPostIdAsync(int postId);

    /// <summary>
    /// Retrieves all posts that a specific user has pinned.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A list of pinned post records for the user.</returns>
    Task<List<PinnedPost>> GetUserPinnedPostsAsync(string userId);

    /// <summary>
    /// Removes all pinned post records for a specific post.
    /// Called when a post is deleted to clean up associated pin records.
    /// </summary>
    /// <param name="postId">The ID of the post to remove pins for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemovePinnedPostsByPostIdAsync(int postId);
}
