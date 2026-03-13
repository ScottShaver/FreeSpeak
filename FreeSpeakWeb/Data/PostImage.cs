using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents an image attached to a feed post.
    /// Supports multiple images per post with configurable display order.
    /// Implements IPostImage for repository pattern abstraction.
    /// </summary>
    public class PostImage : IPostImage<Post>
    {
        /// <summary>
        /// Gets or sets the unique identifier for the image.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the post this image is attached to.
        /// </summary>
        public required int PostId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the parent post.
        /// </summary>
        public Post Post { get; set; } = null!;

        /// <summary>
        /// Gets or sets the URL or path to the image file.
        /// Can be a relative path or full URL depending on storage configuration.
        /// </summary>
        public required string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the display order when multiple images are attached to a post.
        /// Zero-based index where 0 is the first image. Defaults to 0.
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// Gets or sets the timestamp when the image was uploaded.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
