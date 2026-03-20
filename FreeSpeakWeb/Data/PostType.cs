namespace FreeSpeakWeb.Data;

/// <summary>
/// Defines the type of post for the unified article component.
/// </summary>
public enum PostType
{
    /// <summary>
    /// A standard user feed post that appears on user profiles and the main feed.
    /// </summary>
    UserPost,

    /// <summary>
    /// A group post that appears within a specific group's feed.
    /// </summary>
    GroupPost
}
