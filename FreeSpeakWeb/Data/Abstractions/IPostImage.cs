namespace FreeSpeakWeb.Data.Abstractions
{
    /// <summary>
    /// Base interface for post image entities (PostImage, GroupPostImage)
    /// </summary>
    public interface IPostImage
    {
        /// <summary>
        /// Unique identifier for the image
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// The post this image belongs to
        /// </summary>
        int PostId { get; set; }

        /// <summary>
        /// URL or path to the image
        /// </summary>
        string ImageUrl { get; set; }

        /// <summary>
        /// Display order for multiple images
        /// </summary>
        int DisplayOrder { get; set; }

        /// <summary>
        /// When the image was uploaded
        /// </summary>
        DateTime UploadedAt { get; set; }
    }

    /// <summary>
    /// Interface for post images with navigation to Post entity
    /// </summary>
    public interface IPostImage<TPost> : IPostImage
        where TPost : class, IPostEntity
    {
        /// <summary>
        /// Navigation property for the parent post
        /// </summary>
        TPost Post { get; set; }
    }
}
