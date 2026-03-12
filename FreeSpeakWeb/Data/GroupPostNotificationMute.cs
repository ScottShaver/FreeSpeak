namespace FreeSpeakWeb.Data;

/// <summary>
/// Represents a user's preference to mute notifications for a specific group post
/// </summary>
public class GroupPostNotificationMute
{
    public int Id { get; set; }

    /// <summary>
    /// The ID of the group post for which notifications are muted
    /// </summary>
    public int PostId { get; set; }

    /// <summary>
    /// Navigation property to the group post
    /// </summary>
    public GroupPost Post { get; set; } = null!;

    /// <summary>
    /// The ID of the user who muted notifications for this group post
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Navigation property to the user
    /// </summary>
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// When the notification was muted
    /// </summary>
    public DateTime MutedAt { get; set; } = DateTime.UtcNow;
}
