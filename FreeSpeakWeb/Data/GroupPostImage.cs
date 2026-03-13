using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Data
{
    public class GroupPostImage : IPostImage<GroupPost>
    {
        public int Id { get; set; }

        /// <summary>
        /// The group post this image belongs to
        /// </summary>
        public required int PostId { get; set; }
        public GroupPost Post { get; set; } = null!;

        /// <summary>
        /// URL or path to the image
        /// </summary>
        public required string ImageUrl { get; set; }

        /// <summary>
        /// Display order for multiple images (0-based)
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// When the image was uploaded
        /// </summary>
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
