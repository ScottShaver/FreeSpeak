using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Components.SocialFeed;

/// <summary>
/// View model for rendering posts in the social feed UI.
/// Provides flattened, UI-ready data from the Post entity including
/// author information, engagement metrics, and nested comments.
/// </summary>
public class PostViewModel
{
    /// <summary>
    /// Gets or sets the unique identifier of the post.
    /// </summary>
    public int PostId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the post author.
    /// </summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the post author.
    /// </summary>
    public string AuthorName { get; set; } = "Anonymous";

    /// <summary>
    /// Gets or sets the URL for the post author's profile picture.
    /// </summary>
    public string? AuthorImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the post was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets the text content of the post.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of likes/reactions on this post.
    /// </summary>
    public int LikeCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the total number of comments on this post (including replies).
    /// </summary>
    public int CommentCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of times this post has been shared.
    /// </summary>
    public int ShareCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the count of direct comments (excluding replies).
    /// </summary>
    public int DirectCommentCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the audience visibility type for this post.
    /// </summary>
    public AudienceType AudienceType { get; set; } = AudienceType.Public;

    /// <summary>
    /// Gets or sets whether this post is pinned to the top of the feed.
    /// </summary>
    public bool IsPinned { get; set; } = false;

    /// <summary>
    /// Gets or sets the list of images attached to this post.
    /// </summary>
    public List<PostImage> Images { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of comments on this post.
    /// </summary>
    public List<CommentDisplayModel> Comments { get; set; } = new();

    /// <summary>
    /// Gets or sets a breakdown of reactions by type and count.
    /// </summary>
    public Dictionary<LikeType, int>? ReactionBreakdown { get; set; }

    /// <summary>
    /// Gets or sets the current user's reaction to this post, if any.
    /// </summary>
    public LikeType? UserReaction { get; set; }
}
