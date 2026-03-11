using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Components.SocialFeed;

public class PostViewModel
{
    public int PostId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = "Anonymous";
    public string? AuthorImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Content { get; set; } = string.Empty;
    public int LikeCount { get; set; } = 0;
    public int CommentCount { get; set; } = 0;
    public int ShareCount { get; set; } = 0;
    public int DirectCommentCount { get; set; } = 0;
    public AudienceType AudienceType { get; set; } = AudienceType.Public;
    public bool IsPinned { get; set; } = false;
    public List<PostImage> Images { get; set; } = new();
    public List<CommentDisplayModel> Comments { get; set; } = new();
    public Dictionary<LikeType, int>? ReactionBreakdown { get; set; }
    public LikeType? UserReaction { get; set; }
}
