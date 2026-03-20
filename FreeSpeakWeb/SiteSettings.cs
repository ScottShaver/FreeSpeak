namespace FreeSpeakWeb;

/// <summary>
/// Configuration settings for the FreeSpeak site, typically bound from appsettings.json.
/// Controls site branding and social feed behavior limits.
/// </summary>
public class SiteSettings
{
    /// <summary>
    /// Gets or sets the display name of the site used in branding and titles.
    /// </summary>
    public string SiteName { get; set; } = "FreeSpeak";

    /// <summary>
    /// Gets or sets the maximum nesting depth for comments on feed posts.
    /// Comments beyond this depth cannot have replies added.
    /// </summary>
    public int MaxFeedPostCommentDepth { get; set; } = 4;

    /// <summary>
    /// Gets or sets the maximum number of direct (top-level) comments allowed on a single post.
    /// </summary>
    public int MaxFeedPostDirectCommentCount { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether any authenticated user can create a new group.
    /// When true, all users can create groups. When false, only users with
    /// SystemAdministrator or GroupAdministrator roles can create groups.
    /// </summary>
    public bool AllowOpenGroupCreation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether post attachments (image uploads) are enabled.
    /// When true, users can upload images to posts and access the My Uploads page.
    /// When false, all image upload functionality and My Uploads page are hidden.
    /// </summary>
    public bool AllowPostAttachments { get; set; } = true;
}
