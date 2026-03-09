using FreeSpeakWeb.Data;
using Microsoft.AspNetCore.Identity;

namespace FreeSpeakWeb.Tests.Infrastructure
{
    /// <summary>
    /// Test data factory for creating test entities
    /// </summary>
    public static class TestDataFactory
    {
        public static ApplicationUser CreateTestUser(
            string id = "",
            string userName = "testuser",
            string email = "test@example.com",
            string firstName = "Test",
            string lastName = "User")
        {
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
            }

            return new ApplicationUser
            {
                Id = id,
                UserName = userName,
                NormalizedUserName = userName.ToUpper(),
                Email = email,
                NormalizedEmail = email.ToUpper(),
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                SecurityStamp = Guid.NewGuid().ToString()
            };
        }

        public static Post CreateTestPost(
            string authorId,
            string content = "Test post content",
            int likeCount = 0,
            int commentCount = 0,
            int shareCount = 0,
            AudienceType audienceType = AudienceType.FriendsOnly)
        {
            return new Post
            {
                AuthorId = authorId,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                LikeCount = likeCount,
                CommentCount = commentCount,
                ShareCount = shareCount,
                AudienceType = audienceType
            };
        }

        public static Comment CreateTestComment(
            int postId,
            string authorId,
            string content = "Test comment",
            int? parentCommentId = null)
        {
            return new Comment
            {
                PostId = postId,
                AuthorId = authorId,
                Content = content,
                ParentCommentId = parentCommentId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Like CreateTestLike(int postId, string userId)
        {
            return new Like
            {
                PostId = postId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Friendship CreateTestFriendship(
            string requesterId,
            string addresseeId,
            FriendshipStatus status = FriendshipStatus.Pending)
        {
            return new Friendship
            {
                RequesterId = requesterId,
                AddresseeId = addresseeId,
                Status = status,
                RequestedAt = DateTime.UtcNow,
                RespondedAt = status != FriendshipStatus.Pending ? DateTime.UtcNow : null
            };
        }

        public static PostImage CreateTestPostImage(
            int postId,
            string imageUrl = "https://example.com/image.jpg",
            int displayOrder = 0)
        {
            return new PostImage
            {
                PostId = postId,
                ImageUrl = imageUrl,
                DisplayOrder = displayOrder,
                UploadedAt = DateTime.UtcNow
            };
        }
    }
}
