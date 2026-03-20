namespace FreeSpeakWeb.DTOs
{
    /// <summary>
    /// Mutable view model for group posts that wraps the immutable GroupPostListDto.
    /// Enables UI state updates (like/comment counts) while preserving efficient database projections.
    /// Use this class in Blazor components that need to modify post state after loading.
    /// </summary>
    public class GroupPostViewModel
    {
        /// <summary>
        /// Gets the unique identifier of the group post.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the unique identifier of the group this post belongs to.
        /// </summary>
        public int GroupId { get; }

        /// <summary>
        /// Gets the name of the group this post belongs to.
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        /// Gets the unique identifier of the post author.
        /// </summary>
        public string AuthorId { get; }

        /// <summary>
        /// Gets or sets the display name of the post author.
        /// Mutable to allow reformatting based on user preferences after loading.
        /// </summary>
        public string AuthorName { get; set; }

        /// <summary>
        /// Gets the author's first name (for preference-based formatting).
        /// </summary>
        public string AuthorFirstName { get; }

        /// <summary>
        /// Gets the author's last name (for preference-based formatting).
        /// </summary>
        public string AuthorLastName { get; }

        /// <summary>
        /// Gets the author's username (for preference-based formatting).
        /// </summary>
        public string AuthorUserName { get; }

        /// <summary>
        /// Gets the URL for the post author's profile picture.
        /// </summary>
        public string? AuthorImageUrl { get; }

        /// <summary>
        /// Gets the text content of the post.
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// Gets the timestamp when the post was created.
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets the timestamp when the post was last updated, if any.
        /// </summary>
        public DateTime? UpdatedAt { get; }

        /// <summary>
        /// Gets or sets the count of likes on this post.
        /// Mutable to support UI updates without re-fetching from database.
        /// </summary>
        public int LikeCount { get; set; }

        /// <summary>
        /// Gets or sets the count of comments on this post.
        /// Mutable to support UI updates without re-fetching from database.
        /// </summary>
        public int CommentCount { get; set; }

        /// <summary>
        /// Gets or sets the count of shares on this post.
        /// Mutable to support UI updates without re-fetching from database.
        /// </summary>
        public int ShareCount { get; set; }

        /// <summary>
        /// Gets the author's accumulated points in this group.
        /// </summary>
        public int AuthorGroupPoints { get; }

        /// <summary>
        /// Gets the collection of images attached to this post.
        /// </summary>
        public List<PostImageDto> Images { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupPostViewModel"/> class.
        /// </summary>
        /// <param name="id">The unique identifier of the post.</param>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="groupName">The group name.</param>
        /// <param name="authorId">The author identifier.</param>
        /// <param name="authorName">The author display name.</param>
        /// <param name="authorFirstName">The author's first name.</param>
        /// <param name="authorLastName">The author's last name.</param>
        /// <param name="authorUserName">The author's username.</param>
        /// <param name="authorImageUrl">The author's profile picture URL.</param>
        /// <param name="content">The post content.</param>
        /// <param name="createdAt">The creation timestamp.</param>
        /// <param name="updatedAt">The update timestamp, if any.</param>
        /// <param name="likeCount">The initial like count.</param>
        /// <param name="commentCount">The initial comment count.</param>
        /// <param name="shareCount">The initial share count.</param>
        /// <param name="authorGroupPoints">The author's group points.</param>
        /// <param name="images">The post images.</param>
        public GroupPostViewModel(
            int id,
            int groupId,
            string groupName,
            string authorId,
            string authorName,
            string authorFirstName,
            string authorLastName,
            string authorUserName,
            string? authorImageUrl,
            string content,
            DateTime createdAt,
            DateTime? updatedAt,
            int likeCount,
            int commentCount,
            int shareCount,
            int authorGroupPoints,
            List<PostImageDto> images)
        {
            Id = id;
            GroupId = groupId;
            GroupName = groupName;
            AuthorId = authorId;
            AuthorName = authorName;
            AuthorFirstName = authorFirstName;
            AuthorLastName = authorLastName;
            AuthorUserName = authorUserName;
            AuthorImageUrl = authorImageUrl;
            Content = content;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            LikeCount = likeCount;
            CommentCount = commentCount;
            ShareCount = shareCount;
            AuthorGroupPoints = authorGroupPoints;
            Images = images;
        }

        /// <summary>
        /// Creates a GroupPostViewModel from a GroupPostListDto.
        /// </summary>
        /// <param name="dto">The DTO to convert.</param>
        /// <returns>A new GroupPostViewModel instance.</returns>
        public static GroupPostViewModel FromDto(GroupPostListDto dto)
        {
            return new GroupPostViewModel(
                dto.Id,
                dto.GroupId,
                dto.GroupName,
                dto.AuthorId,
                dto.AuthorName,
                dto.AuthorFirstName,
                dto.AuthorLastName,
                dto.AuthorUserName,
                dto.AuthorImageUrl,
                dto.Content,
                dto.CreatedAt,
                dto.UpdatedAt,
                dto.LikeCount,
                dto.CommentCount,
                dto.ShareCount,
                dto.AuthorGroupPoints,
                dto.Images
            );
        }

        /// <summary>
        /// Creates a list of GroupPostViewModels from a list of GroupPostListDtos.
        /// </summary>
        /// <param name="dtos">The DTOs to convert.</param>
        /// <returns>A new list of GroupPostViewModel instances.</returns>
        public static List<GroupPostViewModel> FromDtos(IEnumerable<GroupPostListDto> dtos)
        {
            return dtos.Select(FromDto).ToList();
        }
    }
}
