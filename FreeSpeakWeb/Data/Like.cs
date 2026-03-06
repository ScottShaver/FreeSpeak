namespace FreeSpeakWeb.Data
{
    public class Like
    {
        public int Id { get; set; }

        /// <summary>
        /// The post that was liked
        /// </summary>
        public required int PostId { get; set; }
        public Post Post { get; set; } = null!;

        /// <summary>
        /// The user who liked the post
        /// </summary>
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// When the like was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
