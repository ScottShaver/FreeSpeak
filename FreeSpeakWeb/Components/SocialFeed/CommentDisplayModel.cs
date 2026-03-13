namespace FreeSpeakWeb.Components.SocialFeed;

/// <summary>
/// Display model for rendering comments and replies in the social feed UI.
/// Provides flattened, UI-ready data from the Comment entity.
/// </summary>
public class CommentDisplayModel
{
    /// <summary>
    /// Gets or sets the unique identifier of the comment.
    /// </summary>
    public int CommentId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the comment author.
    /// </summary>
    public string UserName { get; set; } = "";

    /// <summary>
    /// Gets or sets the URL for the comment author's profile picture.
    /// </summary>
    public string? UserImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the comment author.
    /// </summary>
    public string? CommentAuthorId { get; set; }

    /// <summary>
    /// Gets or sets the text content of the comment.
    /// </summary>
    public string CommentText { get; set; } = "";

    /// <summary>
    /// Gets or sets the URL for an optional image attached to the comment.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the comment was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets the list of reply comments nested under this comment.
    /// </summary>
    public List<CommentDisplayModel>? Replies { get; set; }

    /// <summary>
    /// Gets or sets the total number of likes/reactions on this comment.
    /// </summary>
    public int LikeCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the current user's reaction to this comment, if any.
    /// </summary>
    public FreeSpeakWeb.Data.LikeType? UserReaction { get; set; }

    /// <summary>
    /// Gets or sets a breakdown of reactions by type and count.
    /// </summary>
    public Dictionary<FreeSpeakWeb.Data.LikeType, int>? ReactionBreakdown { get; set; }
}
