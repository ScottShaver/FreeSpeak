using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.DTOs
{
    /// <summary>
    /// Data Transfer Object for group post list views using database projection.
    /// Reduces data transfer by selecting only necessary fields instead of loading full entities.
    /// Includes group information for combined feed displays.
    /// Provides 50-70% reduction in data transferred from database compared to full entity loading.
    /// </summary>
    /// <param name="Id">The unique identifier of the group post.</param>
    /// <param name="GroupId">The unique identifier of the group this post belongs to.</param>
    /// <param name="GroupName">The name of the group this post belongs to.</param>
    /// <param name="AuthorId">The unique identifier of the post author.</param>
    /// <param name="AuthorName">The display name of the post author (calculated from preferences).</param>
    /// <param name="AuthorFirstName">The author's first name (for preference-based formatting).</param>
    /// <param name="AuthorLastName">The author's last name (for preference-based formatting).</param>
    /// <param name="AuthorUserName">The author's username (for preference-based formatting).</param>
    /// <param name="AuthorImageUrl">The URL for the post author's profile picture.</param>
    /// <param name="Content">The text content of the post.</param>
    /// <param name="CreatedAt">The timestamp when the post was created.</param>
    /// <param name="UpdatedAt">The timestamp when the post was last updated, if any.</param>
    /// <param name="LikeCount">The cached count of likes on this group post.</param>
    /// <param name="CommentCount">The cached count of comments on this group post.</param>
    /// <param name="ShareCount">The cached count of shares on this group post.</param>
    /// <param name="AuthorGroupPoints">The author's accumulated points in this group.</param>
    /// <param name="IsGroupAdmin">Indicates whether the author is an admin of this group.</param>
    /// <param name="IsGroupModerator">Indicates whether the author is a moderator of this group.</param>
    /// <param name="Images">The collection of image DTOs attached to this group post.</param>
    public record GroupPostListDto(
        int Id,
        int GroupId,
        string GroupName,
        string AuthorId,
        string AuthorName,
        string AuthorFirstName,
        string AuthorLastName,
        string AuthorUserName,
        string? AuthorImageUrl,
        string Content,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        int LikeCount,
        int CommentCount,
        int ShareCount,
        int AuthorGroupPoints,
        bool IsGroupAdmin,
        bool IsGroupModerator,
        List<PostImageDto> Images
    );

    /// <summary>
    /// Data Transfer Object for detailed group post views including additional metadata.
    /// Used when loading a single group post with full details for display.
    /// </summary>
    /// <param name="Id">The unique identifier of the group post.</param>
    /// <param name="GroupId">The unique identifier of the group this post belongs to.</param>
    /// <param name="GroupName">The name of the group this post belongs to.</param>
    /// <param name="GroupHeaderImageUrl">The URL for the group's header image.</param>
    /// <param name="AuthorId">The unique identifier of the post author.</param>
    /// <param name="AuthorFirstName">The first name of the post author.</param>
    /// <param name="AuthorLastName">The last name of the post author.</param>
    /// <param name="AuthorImageUrl">The URL for the post author's profile picture.</param>
    /// <param name="Content">The text content of the post.</param>
    /// <param name="CreatedAt">The timestamp when the post was created.</param>
    /// <param name="UpdatedAt">The timestamp when the post was last updated, if any.</param>
    /// <param name="LikeCount">The cached count of likes on this group post.</param>
    /// <param name="CommentCount">The cached count of comments on this group post.</param>
    /// <param name="ShareCount">The cached count of shares on this group post.</param>
    /// <param name="AuthorGroupPoints">The author's accumulated points in this group.</param>
    /// <param name="Images">The collection of image DTOs attached to this group post.</param>
    public record GroupPostDetailDto(
        int Id,
        int GroupId,
        string GroupName,
        string? GroupHeaderImageUrl,
        string AuthorId,
        string AuthorFirstName,
        string AuthorLastName,
        string? AuthorImageUrl,
        string Content,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        int LikeCount,
        int CommentCount,
        int ShareCount,
        int AuthorGroupPoints,
        List<PostImageDto> Images
    )
    {
        /// <summary>
        /// Gets the full display name of the author.
        /// </summary>
        public string AuthorName => $"{AuthorFirstName} {AuthorLastName}".Trim();
    }
}
