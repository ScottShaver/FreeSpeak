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

        public static Group CreateTestGroup(
            string creatorId,
            string name = "Test Group",
            string description = "Test group description",
            bool isPublic = true,
            bool isHidden = false)
        {
            return new Group
            {
                CreatorId = creatorId,
                Name = name,
                Description = description,
                IsPublic = isPublic,
                IsHidden = isHidden,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };
        }

        public static GroupUser CreateTestGroupUser(
            int groupId,
            string userId,
            bool isAdmin = false,
            bool isModerator = false)
        {
            return new GroupUser
            {
                GroupId = groupId,
                UserId = userId,
                IsAdmin = isAdmin,
                IsModerator = isModerator,
                JoinedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };
        }

        public static GroupPost CreateTestGroupPost(
            int groupId,
            string authorId,
            string content = "Test group post content",
            int likeCount = 0,
            int commentCount = 0,
            int shareCount = 0)
        {
            return new GroupPost
            {
                GroupId = groupId,
                AuthorId = authorId,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                LikeCount = likeCount,
                CommentCount = commentCount,
                ShareCount = shareCount
            };
        }

        public static GroupPostComment CreateTestGroupPostComment(
            int postId,
            string authorId,
            string content = "Test group comment",
            int? parentCommentId = null)
        {
            return new GroupPostComment
            {
                PostId = postId,
                AuthorId = authorId,
                Content = content,
                ParentCommentId = parentCommentId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static GroupPostLike CreateTestGroupPostLike(
            int postId,
            string userId,
            LikeType type = LikeType.Like)
        {
            return new GroupPostLike
            {
                PostId = postId,
                UserId = userId,
                Type = type,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static GroupPostCommentLike CreateTestGroupPostCommentLike(
            int commentId,
            string userId,
            LikeType type = LikeType.Like)
        {
            return new GroupPostCommentLike
            {
                CommentId = commentId,
                UserId = userId,
                Type = type,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static PinnedGroupPost CreateTestPinnedGroupPost(
            string userId,
            int postId)
        {
            return new PinnedGroupPost
            {
                UserId = userId,
                PostId = postId,
                PinnedAt = DateTime.UtcNow
            };
        }

        public static GroupBannedMember CreateTestGroupBannedMember(
            int groupId,
            string userId)
        {
            return new GroupBannedMember
            {
                GroupId = groupId,
                UserId = userId,
                BannedAt = DateTime.UtcNow
            };
        }

        public static GroupPostNotificationMute CreateTestGroupPostNotificationMute(
            int postId,
            string userId)
        {
            return new GroupPostNotificationMute
            {
                PostId = postId,
                UserId = userId,
                MutedAt = DateTime.UtcNow
            };
        }
    }
}
