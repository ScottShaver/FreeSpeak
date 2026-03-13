using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.Abstractions;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Generic repository interface for post entities (Post, GroupPost).
    /// Provides common CRUD operations, image management, query methods, and cached count updates.
    /// Enables polymorphic data access across both feed posts and group posts through the IPostEntity abstraction.
    /// </summary>
    /// <typeparam name="TPost">The post entity type implementing IPostEntity.</typeparam>
    /// <typeparam name="TImage">The post image entity type implementing IPostImage.</typeparam>
    public interface IPostRepository<TPost, TImage>
        where TPost : class, IPostEntity
        where TImage : class, IPostImage
    {
        #region Post CRUD Operations

        /// <summary>
        /// Retrieves a post by its unique identifier with optional navigation property loading.
        /// </summary>
        /// <param name="postId">The ID of the post to retrieve.</param>
        /// <param name="includeAuthor">Whether to include the author navigation property. Default is true.</param>
        /// <param name="includeImages">Whether to include the images collection. Default is true.</param>
        /// <returns>The post if found; otherwise, null.</returns>
        Task<TPost?> GetByIdAsync(int postId, bool includeAuthor = true, bool includeImages = true);

        /// <summary>
        /// Creates a new post in the database.
        /// </summary>
        /// <param name="post">The post entity to create.</param>
        /// <returns>A tuple containing success status, optional error message, and the created post.</returns>
        Task<(bool Success, string? ErrorMessage, TPost? Post)> CreateAsync(TPost post);

        /// <summary>
        /// Updates the text content of an existing post.
        /// Validates that the user is authorized to edit the post.
        /// </summary>
        /// <param name="postId">The ID of the post to update.</param>
        /// <param name="userId">The ID of the user attempting the update.</param>
        /// <param name="newContent">The new content text for the post.</param>
        /// <returns>A tuple containing success status and optional error message.</returns>
        Task<(bool Success, string? ErrorMessage)> UpdateContentAsync(int postId, string userId, string newContent);

        /// <summary>
        /// Deletes a post and all related data (comments, likes, images).
        /// Validates that the user is authorized to delete the post.
        /// </summary>
        /// <param name="postId">The ID of the post to delete.</param>
        /// <param name="userId">The ID of the user attempting the deletion.</param>
        /// <returns>A tuple containing success status and optional error message.</returns>
        Task<(bool Success, string? ErrorMessage)> DeleteAsync(int postId, string userId);

        /// <summary>
        /// Checks whether a specific user has permission to delete a post.
        /// Typically true if the user is the author or has administrative privileges.
        /// </summary>
        /// <param name="postId">The ID of the post.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user can delete the post; otherwise, false.</returns>
        Task<bool> CanUserDeleteAsync(int postId, string userId);

        #endregion

        #region Image Operations

        /// <summary>
        /// Adds one or more images to an existing post.
        /// Validates that the user is authorized to modify the post.
        /// </summary>
        /// <param name="postId">The ID of the post to add images to.</param>
        /// <param name="userId">The ID of the user adding the images.</param>
        /// <param name="imageUrls">List of image URLs or paths to add.</param>
        /// <returns>A tuple containing success status, optional error message, and the created image entities.</returns>
        Task<(bool Success, string? ErrorMessage, List<TImage>? Images)> AddImagesAsync(int postId, string userId, List<string> imageUrls);

        /// <summary>
        /// Removes specified images from a post.
        /// Validates that the user is authorized to modify the post.
        /// </summary>
        /// <param name="postId">The ID of the post.</param>
        /// <param name="userId">The ID of the user removing the images.</param>
        /// <param name="imageIds">List of image IDs to remove.</param>
        /// <returns>A tuple containing success status and optional error message.</returns>
        Task<(bool Success, string? ErrorMessage)> RemoveImagesAsync(int postId, string userId, List<int> imageIds);

        /// <summary>
        /// Retrieves all images associated with a specific post.
        /// Images are ordered by their display order.
        /// </summary>
        /// <param name="postId">The ID of the post.</param>
        /// <returns>A list of images attached to the post.</returns>
        Task<List<TImage>> GetImagesAsync(int postId);

        #endregion

        #region Query Operations

        /// <summary>
        /// Retrieves posts created by a specific author with pagination support.
        /// </summary>
        /// <param name="authorId">The ID of the author.</param>
        /// <param name="skip">Number of posts to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of posts to retrieve. Default is 20.</param>
        /// <returns>A list of posts by the specified author.</returns>
        Task<List<TPost>> GetByAuthorAsync(string authorId, int skip = 0, int take = 20);

        /// <summary>
        /// Gets the total count of posts created by a specific author.
        /// </summary>
        /// <param name="authorId">The ID of the author.</param>
        /// <returns>The total number of posts by the author.</returns>
        Task<int> GetCountByAuthorAsync(string authorId);

        /// <summary>
        /// Checks whether a post with the specified ID exists in the database.
        /// </summary>
        /// <param name="postId">The ID of the post to check.</param>
        /// <returns>True if the post exists; otherwise, false.</returns>
        Task<bool> ExistsAsync(int postId);

        #endregion

        #region Count Operations

        /// <summary>
        /// Increments the cached like count for a post by 1.
        /// Called when a new like is added to the post.
        /// </summary>
        /// <param name="postId">The ID of the post.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task IncrementLikeCountAsync(int postId);

        /// <summary>
        /// Decrements the cached like count for a post by 1.
        /// Called when a like is removed from the post.
        /// </summary>
        /// <param name="postId">The ID of the post.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DecrementLikeCountAsync(int postId);

        /// <summary>
        /// Increments the cached comment count for a post by 1.
        /// Called when a new comment is added to the post.
        /// </summary>
        /// <param name="postId">The ID of the post.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task IncrementCommentCountAsync(int postId);

        /// <summary>
        /// Decrements the cached comment count for a post.
        /// Called when comments are deleted from the post.
        /// </summary>
        /// <param name="postId">The ID of the post.</param>
        /// <param name="count">The number to decrement by. Default is 1.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DecrementCommentCountAsync(int postId, int count = 1);

        /// <summary>
        /// Increments the cached share count for a post by 1.
        /// Called when the post is shared or reposted.
        /// </summary>
        /// <param name="postId">The ID of the post.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task IncrementShareCountAsync(int postId);

        #endregion
    }

    /// <summary>
    /// Extended repository interface for feed posts with audience visibility controls.
    /// Adds methods specific to the main user feed including privacy settings and feed generation.
    /// Feed posts support Public, FriendsOnly, and MeOnly audience types.
    /// </summary>
    /// <typeparam name="TPost">The post entity type implementing IFeedPostEntity.</typeparam>
    /// <typeparam name="TImage">The post image entity type.</typeparam>
    public interface IFeedPostRepository<TPost, TImage> : IPostRepository<TPost, TImage>
        where TPost : class, IFeedPostEntity
        where TImage : class, IPostImage
    {
        /// <summary>
        /// Updates the audience visibility setting for a post.
        /// Validates that the user is authorized to modify the post.
        /// </summary>
        /// <param name="postId">The ID of the post to update.</param>
        /// <param name="userId">The ID of the user making the change.</param>
        /// <param name="audienceType">The new audience visibility level.</param>
        /// <returns>A tuple containing success status and optional error message.</returns>
        Task<(bool Success, string? ErrorMessage)> UpdateAudienceAsync(int postId, string userId, AudienceType audienceType);

        /// <summary>
        /// Retrieves posts for a user's personalized feed with pagination.
        /// Includes posts from the user's friends and their own posts, filtered by audience type permissions.
        /// </summary>
        /// <param name="userId">The ID of the user viewing the feed.</param>
        /// <param name="skip">Number of posts to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of posts to retrieve. Default is 20.</param>
        /// <returns>A list of posts for the user's feed, ordered by creation date descending.</returns>
        Task<List<TPost>> GetFeedPostsAsync(string userId, int skip = 0, int take = 20);

        /// <summary>
        /// Gets the total count of posts available in a user's feed.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The total number of posts in the user's feed.</returns>
        Task<int> GetFeedPostsCountAsync(string userId);

        /// <summary>
        /// Retrieves public posts with pagination support.
        /// Returns only posts with AudienceType.Public for discovery and public browsing.
        /// </summary>
        /// <param name="pageNumber">The page number to retrieve (1-based). Default is 1.</param>
        /// <param name="pageSize">The number of posts per page. Default is 10.</param>
        /// <returns>A tuple containing the list of public posts and a flag indicating if more posts are available.</returns>
        Task<(List<TPost> Posts, bool HasMore)> GetPublicPostsAsync(int pageNumber = 1, int pageSize = 10);
    }

    /// <summary>
    /// Extended repository interface for group posts with group-specific operations.
    /// Adds methods for querying posts within groups, validating posting permissions,
    /// and retrieving group activity across multiple groups.
    /// </summary>
    /// <typeparam name="TPost">The post entity type implementing IGroupPostEntity.</typeparam>
    /// <typeparam name="TImage">The post image entity type.</typeparam>
    public interface IGroupPostRepository<TPost, TImage> : IPostRepository<TPost, TImage>
        where TPost : class, IGroupPostEntity
        where TImage : class, IPostImage
    {
        /// <summary>
        /// Retrieves posts from a specific group with pagination.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="skip">Number of posts to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of posts to retrieve. Default is 20.</param>
        /// <returns>A list of posts from the specified group, ordered by creation date descending.</returns>
        Task<List<TPost>> GetByGroupAsync(int groupId, int skip = 0, int take = 20);

        /// <summary>
        /// Gets the total count of posts in a specific group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>The total number of posts in the group.</returns>
        Task<int> GetCountByGroupAsync(int groupId);

        /// <summary>
        /// Retrieves posts created by a specific user within a specific group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="authorId">The ID of the author.</param>
        /// <param name="skip">Number of posts to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of posts to retrieve. Default is 20.</param>
        /// <returns>A list of posts by the specified author in the specified group.</returns>
        Task<List<TPost>> GetByGroupAndAuthorAsync(int groupId, string authorId, int skip = 0, int take = 20);

        /// <summary>
        /// Retrieves posts from all groups that a user is a member of.
        /// Useful for generating a personalized group activity feed.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="skip">Number of posts to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of posts to retrieve. Default is 20.</param>
        /// <returns>A list of posts from all groups the user is a member of.</returns>
        Task<List<TPost>> GetAllGroupPostsForUserAsync(string userId, int skip = 0, int take = 20);

        /// <summary>
        /// Checks whether a user has permission to create posts in a specific group.
        /// Validates group membership and ban status.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A tuple containing a flag indicating if the user can post and an optional error message.</returns>
        Task<(bool CanPost, string? ErrorMessage)> CanUserPostAsync(int groupId, string userId);
    }
}
