namespace FreeSpeakWeb.Data.Abstractions
{
    /// <summary>
    /// Base interface for post image entities (PostImage, GroupPostImage).
    /// Provides common properties for all post image implementations, enabling polymorphic operations
    /// and shared repository logic across both feed post images and group post images.
    /// Supports multiple images per post with configurable display order.
    /// </summary>
    public interface IPostImage
    {
        /// <summary>
        /// Gets or sets the unique identifier for the image.
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the post this image is attached to.
        /// </summary>
        int PostId { get; set; }

        /// <summary>
        /// Gets or sets the URL or file path to the image.
        /// Can be a relative path or full URL depending on storage configuration.
        /// </summary>
        string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the display order for multiple images attached to the same post.
        /// Zero-based index where 0 is the first image displayed.
        /// </summary>
        int DisplayOrder { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the image was uploaded.
        /// </summary>
        DateTime UploadedAt { get; set; }
    }

    /// <summary>
    /// Generic interface for post images with strongly-typed navigation to the parent post entity.
    /// Enables type-safe operations while maintaining polymorphic behavior through the base interface.
    /// </summary>
    /// <typeparam name="TPost">The specific post entity type (Post or GroupPost).</typeparam>
    public interface IPostImage<TPost> : IPostImage
        where TPost : class, IPostEntity
    {
        /// <summary>
        /// Gets or sets the navigation property to the parent post.
        /// Provides strongly-typed access to the post this image belongs to.
        /// </summary>
        TPost Post { get; set; }
    }
}
