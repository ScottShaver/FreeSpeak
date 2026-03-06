namespace FreeSpeakWeb;

public class SiteSettings
{
    public string SiteName { get; set; } = "FreeSpeak";
    public int MaxFeedPostCommentDepth { get; set; } = 4;
    public int MaxFeedPostDirectCommentCount { get; set; } = 1000;
}
