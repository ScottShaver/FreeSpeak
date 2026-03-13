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
}
