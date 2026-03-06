namespace FreeSpeakWeb.Components.SocialFeed;

public class CommentDisplayModel
{
    public int CommentId { get; set; }
    public string UserName { get; set; } = "";
    public string? UserImageUrl { get; set; }
    public string? CommentAuthorId { get; set; }
    public string CommentText { get; set; } = "";
    public string? ImageUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public List<CommentDisplayModel>? Replies { get; set; }
    public int LikeCount { get; set; } = 0;
    public FreeSpeakWeb.Data.LikeType? UserReaction { get; set; }
    public Dictionary<FreeSpeakWeb.Data.LikeType, int>? ReactionBreakdown { get; set; }
}
