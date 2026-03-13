using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Generic repository interface for post entities
    /// Defines common operations for Post and GroupPost repositories
    /// </summary>
    /// <typeparam name="TPost">The post entity type</typeparam>
    /// <typeparam name="TImage">The post image entity type</typeparam>
    public interface IPostRepository<TPost, TImage>
        where TPost : class, IPostEntity
        where TImage : class, IPostImage
    {
        #region Post CRUD Operations

        /// <summary>
        /// Get a post by its ID with optional includes
        /// </summary>
        Task<TPost?> GetByIdAsync(int postId, bool includeAuthor = true, bool includeImages = true);

        /// <summary>
        /// Create a new post
        /// </summary>
        Task<(bool Success, string? ErrorMessage, TPost? Post)> CreateAsync(TPost post);

        /// <summary>
        /// Update an existing post's content
        /// </summary>
        Task<(bool Success, string? ErrorMessage)> UpdateContentAsync(int postId, string userId, string newContent);

        /// <summary>
        /// Delete a post and all related data
        /// </summary>
        Task<(bool Success, string? ErrorMessage)> DeleteAsync(int postId, string userId);

        /// <summary>
        /// Check if a user can delete a specific post
        /// </summary>
        Task<bool> CanUserDeleteAsync(int postId, string userId);

        #endregion

        #region Image Operations

        /// <summary>
        /// Add images to an existing post
        /// </summary>
        Task<(bool Success, string? ErrorMessage, List<TImage>? Images)> AddImagesAsync(int postId, string userId, List<string> imageUrls);

        /// <summary>
        /// Remove images from a post
        /// </summary>
        Task<(bool Success, string? ErrorMessage)> RemoveImagesAsync(int postId, string userId, List<int> imageIds);

        /// <summary>
        /// Get all images for a post
        /// </summary>
        Task<List<TImage>> GetImagesAsync(int postId);

        #endregion

        #region Query Operations

        /// <summary>
        /// Get posts by author with pagination
        /// </summary>
        Task<List<TPost>> GetByAuthorAsync(string authorId, int skip = 0, int take = 20);

        /// <summary>
        /// Get the count of posts by an author
        /// </summary>
        Task<int> GetCountByAuthorAsync(string authorId);

        /// <summary>
        /// Check if a post exists
        /// </summary>
        Task<bool> ExistsAsync(int postId);

        #endregion

        #region Count Operations

        /// <summary>
        /// Increment the like count for a post
        /// </summary>
        Task IncrementLikeCountAsync(int postId);

        /// <summary>
        /// Decrement the like count for a post
        /// </summary>
        Task DecrementLikeCountAsync(int postId);

        /// <summary>
        /// Increment the comment count for a post
        /// </summary>
        Task IncrementCommentCountAsync(int postId);

        /// <summary>
        /// Decrement the comment count for a post
        /// </summary>
        Task DecrementCommentCountAsync(int postId, int count = 1);

        /// <summary>
        /// Increment the share count for a post
        /// </summary>
        Task IncrementShareCountAsync(int postId);

        #endregion
    }

    /// <summary>
    /// Extended repository interface for feed posts (non-group posts)
    /// </summary>
    /// <typeparam name="TPost">The post entity type (must implement IFeedPostEntity)</typeparam>
    /// <typeparam name="TImage">The post image entity type</typeparam>
    public interface IFeedPostRepository<TPost, TImage> : IPostRepository<TPost, TImage>
        where TPost : class, IFeedPostEntity
        where TImage : class, IPostImage
    {
        /// <summary>
        /// Update the audience type of a post
        /// </summary>
        Task<(bool Success, string? ErrorMessage)> UpdateAudienceAsync(int postId, string userId, AudienceType audienceType);

        /// <summary>
        /// Get posts for a user's feed (from friends and self)
        /// </summary>
        Task<List<TPost>> GetFeedPostsAsync(string userId, int skip = 0, int take = 20);

        /// <summary>
        /// Get the count of posts in a user's feed
        /// </summary>
        Task<int> GetFeedPostsCountAsync(string userId);

        /// <summary>
        /// Get public posts with pagination
        /// </summary>
        Task<(List<TPost> Posts, bool HasMore)> GetPublicPostsAsync(int pageNumber = 1, int pageSize = 10);
    }

    /// <summary>
    /// Extended repository interface for group posts
    /// </summary>
    /// <typeparam name="TPost">The post entity type (must implement IGroupPostEntity)</typeparam>
    /// <typeparam name="TImage">The post image entity type</typeparam>
    public interface IGroupPostRepository<TPost, TImage> : IPostRepository<TPost, TImage>
        where TPost : class, IGroupPostEntity
        where TImage : class, IPostImage
    {
        /// <summary>
        /// Get posts for a specific group with pagination
        /// </summary>
        Task<List<TPost>> GetByGroupAsync(int groupId, int skip = 0, int take = 20);

        /// <summary>
        /// Get the count of posts in a group
        /// </summary>
        Task<int> GetCountByGroupAsync(int groupId);

        /// <summary>
        /// Get posts by a specific user in a group
        /// </summary>
        Task<List<TPost>> GetByGroupAndAuthorAsync(int groupId, string authorId, int skip = 0, int take = 20);

        /// <summary>
        /// Get posts from all groups a user is a member of
        /// </summary>
        Task<List<TPost>> GetAllGroupPostsForUserAsync(string userId, int skip = 0, int take = 20);

        /// <summary>
        /// Check if a user can post in a specific group
        /// </summary>
        Task<(bool CanPost, string? ErrorMessage)> CanUserPostAsync(int groupId, string userId);
    }
}
