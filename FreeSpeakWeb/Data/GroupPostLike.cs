using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Data
{
    public class GroupPostLike : IPostLike<GroupPost>
    {
        public int Id { get; set; }

        /// <summary>
        /// The group post that was liked
        /// </summary>
        public required int PostId { get; set; }
        public GroupPost Post { get; set; } = null!;

        /// <summary>
        /// The user who liked the post
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
