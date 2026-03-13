using FreeSpeakWeb.Components.SocialFeed;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Mapping
{
    /// <summary>
    /// Extension methods for mapping between DTOs, entities, and view models.
    /// Provides a clean mapping layer to convert projection-based DTOs to view models
    /// compatible with existing Blazor components, enabling gradual adoption of
    /// optimized database queries without breaking existing UI components.
    /// </summary>
    public static class PostMappingExtensions
    {
        /// <summary>
        /// Converts a PostListDto to a PostViewModel compatible with existing components.
        /// </summary>
        /// <param name="dto">The PostListDto from a projection query.</param>
        /// <returns>A PostViewModel that can be used with FeedArticle and other components.</returns>
        public static PostViewModel ToViewModel(this PostListDto dto)
        {
            return new PostViewModel
            {
                PostId = dto.Id,
                AuthorId = dto.AuthorId,
                AuthorName = dto.AuthorName,
                AuthorImageUrl = dto.AuthorImageUrl,
                Content = dto.Content,
                CreatedAt = dto.CreatedAt,
                LikeCount = dto.LikeCount,
                CommentCount = dto.CommentCount,
                ShareCount = dto.ShareCount,
                AudienceType = dto.AudienceType,
                Images = dto.Images.Select(i => i.ToPostImage()).ToList()
            };
        }

        /// <summary>
        /// Converts a collection of PostListDto to PostViewModel list.
        /// </summary>
        /// <param name="dtos">The collection of PostListDto objects.</param>
        /// <returns>A list of PostViewModel objects.</returns>
        public static List<PostViewModel> ToViewModels(this IEnumerable<PostListDto> dtos)
        {
            return dtos.Select(dto => dto.ToViewModel()).ToList();
        }

        /// <summary>
        /// Converts a PostDetailDto to a PostViewModel compatible with existing components.
        /// </summary>
        /// <param name="dto">The PostDetailDto from a projection query.</param>
        /// <returns>A PostViewModel that can be used with FeedArticle and other components.</returns>
        public static PostViewModel ToViewModel(this PostDetailDto dto)
        {
            return new PostViewModel
            {
                PostId = dto.Id,
                AuthorId = dto.AuthorId,
                AuthorName = $"{dto.AuthorFirstName} {dto.AuthorLastName}".Trim(),
                AuthorImageUrl = dto.AuthorImageUrl,
                Content = dto.Content,
                CreatedAt = dto.CreatedAt,
                LikeCount = dto.LikeCount,
                CommentCount = dto.CommentCount,
                ShareCount = dto.ShareCount,
                AudienceType = dto.AudienceType,
                Images = dto.Images.Select(i => i.ToPostImage()).ToList()
            };
        }

        /// <summary>
        /// Converts a PostImageDto to a PostImage entity for compatibility with existing components.
        /// </summary>
        /// <param name="dto">The PostImageDto from a projection query.</param>
        /// <returns>A PostImage entity that can be used with FeedArticleImages and other components.</returns>
        public static PostImage ToPostImage(this PostImageDto dto)
        {
            return new PostImage
            {
                Id = dto.Id,
                ImageUrl = dto.ImageUrl,
                DisplayOrder = dto.DisplayOrder,
                PostId = 0 // Not needed for display purposes
            };
        }

        /// <summary>
        /// Converts a Post entity to a PostViewModel.
        /// Useful when you have a full entity but want to use the unified view model interface.
        /// </summary>
        /// <param name="post">The Post entity.</param>
        /// <param name="authorName">Optional pre-formatted author name. If not provided, uses Author navigation property.</param>
        /// <returns>A PostViewModel representation of the Post.</returns>
        public static PostViewModel ToViewModel(this Post post, string? authorName = null)
        {
            return new PostViewModel
            {
                PostId = post.Id,
                AuthorId = post.AuthorId,
                AuthorName = authorName ?? $"{post.Author?.FirstName} {post.Author?.LastName}".Trim(),
                AuthorImageUrl = post.Author?.ProfilePictureUrl,
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                LikeCount = post.LikeCount,
                CommentCount = post.CommentCount,
                ShareCount = post.ShareCount,
                AudienceType = post.AudienceType,
                Images = post.Images?.ToList() ?? new List<PostImage>()
            };
        }

        /// <summary>
        /// Converts a collection of Post entities to PostViewModel list.
        /// </summary>
        /// <param name="posts">The collection of Post entities.</param>
        /// <returns>A list of PostViewModel objects.</returns>
        public static List<PostViewModel> ToViewModels(this IEnumerable<Post> posts)
        {
            return posts.Select(post => post.ToViewModel()).ToList();
        }

        /// <summary>
        /// Converts a GroupPostListDto to a PostViewModel compatible with existing components.
        /// </summary>
        /// <param name="dto">The GroupPostListDto from a projection query.</param>
        /// <returns>A PostViewModel that can be used with FeedArticle and other components.</returns>
        public static PostViewModel ToViewModel(this GroupPostListDto dto)
        {
            return new PostViewModel
            {
                PostId = dto.Id,
                AuthorId = dto.AuthorId,
                AuthorName = dto.AuthorName,
                AuthorImageUrl = dto.AuthorImageUrl,
                Content = dto.Content,
                CreatedAt = dto.CreatedAt,
                LikeCount = dto.LikeCount,
                CommentCount = dto.CommentCount,
                ShareCount = dto.ShareCount,
                AudienceType = AudienceType.FriendsOnly, // Group posts are group-member only
                Images = dto.Images.Select(i => i.ToPostImage()).ToList()
            };
        }

        /// <summary>
        /// Converts a collection of GroupPostListDto to PostViewModel list.
        /// </summary>
        /// <param name="dtos">The collection of GroupPostListDto objects.</param>
        /// <returns>A list of PostViewModel objects.</returns>
        public static List<PostViewModel> ToViewModels(this IEnumerable<GroupPostListDto> dtos)
        {
            return dtos.Select(dto => dto.ToViewModel()).ToList();
        }
    }
}
