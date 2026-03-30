using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Mapping
{
    /// <summary>
    /// Extension methods for mapping between DTOs and entity models.
    /// These mappings create lightweight entity instances for UI display from database projections.
    /// </summary>
    public static class PostDtoMappingExtensions
    {
        /// <summary>
        /// Converts a PostListDto projection to a Post entity for UI display.
        /// Creates a minimal Post instance with only the properties needed for rendering.
        /// Note: This is not a full entity - it's for display purposes only and should not be persisted.
        /// </summary>
        /// <param name="dto">The PostListDto projection from the database.</param>
        /// <returns>A Post instance populated with data from the DTO.</returns>
        public static Post ToDisplayEntity(this PostListDto dto)
        {
            return new Post
            {
                Id = dto.Id,
                AuthorId = dto.AuthorId,
                Author = new ApplicationUser
                {
                    Id = dto.AuthorId,
                    FirstName = dto.AuthorName.Split(' ').FirstOrDefault() ?? dto.AuthorName,
                    LastName = dto.AuthorName.Split(' ').Skip(1).FirstOrDefault() ?? "",
                    UserName = dto.AuthorUserName,
                    ProfilePictureUrl = dto.AuthorImageUrl
                },
                Content = dto.Content,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                LikeCount = dto.LikeCount,
                CommentCount = dto.CommentCount,
                ShareCount = dto.ShareCount,
                AudienceType = dto.AudienceType,
                Images = dto.Images?.Select(i => new PostImage
                {
                    Id = i.Id,
                    PostId = dto.Id,
                    ImageUrl = i.ImageUrl,
                    DisplayOrder = i.DisplayOrder
                }).ToList() ?? new List<PostImage>()
            };
        }

        /// <summary>
        /// Converts a collection of PostListDto projections to Post entities for UI display.
        /// </summary>
        /// <param name="dtos">The collection of PostListDto projections.</param>
        /// <returns>A list of Post instances for UI rendering.</returns>
        public static List<Post> ToDisplayEntities(this IEnumerable<PostListDto> dtos)
        {
            return dtos.Select(dto => dto.ToDisplayEntity()).ToList();
        }
    }
}
