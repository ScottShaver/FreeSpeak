namespace FreeSpeakWeb.Data
{
    public class GroupPostCommentLike
    {
        public int Id { get; set; }

        /// <summary>
        /// The group post comment that was liked
        /// </summary>
        public required int CommentId { get; set; }
        public GroupPostComment Comment { get; set; } = null!;

        /// <summary>
        /// The user who liked the comment
        /// </summary>
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// The type of reaction/like (Like, Love, Care, etc.)
        /// </summary>
        public LikeType Type { get; set; } = LikeType.Like;

        /// <summary>
        /// When the like was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
