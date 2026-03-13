namespace FreeSpeakWeb.Data;

/// <summary>
/// Represents a user's preference to mute notifications for a specific group post.
/// When muted, the user will not receive notifications about new comments or interactions on that group post.
/// </summary>
public class GroupPostNotificationMute
{
    /// <summary>
    /// Gets or sets the unique identifier for this notification mute record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the group post for which notifications are muted.
    /// </summary>
    public int PostId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the group post.
    /// </summary>
    public GroupPost Post { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ID of the user who muted notifications for this group post.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the user.
    /// </summary>
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the timestamp when notifications were muted.
    /// Defaults to UTC now.
    /// </summary>
    public DateTime MutedAt { get; set; } = DateTime.UtcNow;
}
