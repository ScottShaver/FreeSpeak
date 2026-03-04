namespace FreeSpeakWeb.Components.SocialFeed;

public class CommentDisplayModel
{
    public string UserName { get; set; } = "";
    public string? UserImageUrl { get; set; }
    public string CommentText { get; set; } = "";
    public string? ImageUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public List<CommentDisplayModel>? Replies { get; set; }
}
