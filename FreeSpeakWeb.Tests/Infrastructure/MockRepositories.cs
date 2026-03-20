using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace FreeSpeakWeb.Tests.Infrastructure
{
    /// <summary>
    /// Helper class for creating mock repositories in tests
    /// </summary>
    public static class MockRepositories
    {
        /// <summary>
        /// Creates a mock INotificationRepository
        /// </summary>
        public static Mock<INotificationRepository> CreateMockNotificationRepository()
        {
            var mock = new Mock<INotificationRepository>();

            // Setup common default behaviors
            mock.Setup(r => r.AddAsync(It.IsAny<UserNotification>()))
                .ReturnsAsync((UserNotification notification) => notification);

            mock.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((UserNotification?)null);

            mock.Setup(r => r.GetUserNotificationsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<UserNotification>());

            mock.Setup(r => r.GetUnreadCountAsync(It.IsAny<string>()))
                .ReturnsAsync(0);

            return mock;
        }

        /// <summary>
        /// Creates a mock IFriendshipRepository
        /// </summary>
        public static Mock<IFriendshipRepository> CreateMockFriendshipRepository()
        {
            var mock = new Mock<IFriendshipRepository>();

            // Setup common default behaviors
            mock.Setup(r => r.AddAsync(It.IsAny<Friendship>()))
                .ReturnsAsync((Friendship friendship) => friendship);

            mock.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Friendship?)null);

            mock.Setup(r => r.GetFriendshipAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((Friendship?)null);

            mock.Setup(r => r.GetAcceptedFriendshipsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<Friendship>());

            mock.Setup(r => r.AreFriendsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            return mock;
        }

        /// <summary>
        /// Creates a mock IUserRepository
        /// </summary>
        public static Mock<IUserRepository> CreateMockUserRepository()
        {
            var mock = new Mock<IUserRepository>();

            // Setup common default behaviors
            mock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            mock.Setup(r => r.GetByUsernameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            mock.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            mock.Setup(r => r.ExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            mock.Setup(r => r.SearchUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ApplicationUser>());

            return mock;
        }

        /// <summary>
        /// Creates a mock IGroupRepository
        /// </summary>
        public static Mock<IGroupRepository> CreateMockGroupRepository()
        {
            var mock = new Mock<IGroupRepository>();

            mock.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Group?)null);

            mock.Setup(r => r.AddAsync(It.IsAny<Group>()))
                .ReturnsAsync((Group group) => group);

            mock.Setup(r => r.ExistsAsync(It.IsAny<int>()))
                .ReturnsAsync(false);

            return mock;
        }

        /// <summary>
        /// Creates a mock IFeedPostRepository
        /// </summary>
        public static Mock<IFeedPostRepository<Post, PostImage>> CreateMockFeedPostRepository()
        {
            var mock = new Mock<IFeedPostRepository<Post, PostImage>>();

            mock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((Post?)null);

            mock.Setup(r => r.CreateAsync(It.IsAny<Post>()))
                .ReturnsAsync((Post post) => (true, null, post));

            mock.Setup(r => r.GetFeedPostsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<PostListDto>());

            return mock;
        }

        /// <summary>
        /// Creates a mock IFeedCommentRepository
        /// </summary>
        public static Mock<IFeedCommentRepository> CreateMockFeedCommentRepository()
        {
            var mock = new Mock<IFeedCommentRepository>();

            mock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((Comment?)null);

            mock.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync((int postId, string authorId, string content, string? imageUrl, int? parentCommentId) => 
                    (true, null, new Comment { Id = 1, PostId = postId, AuthorId = authorId, Content = content }));

            mock.Setup(r => r.GetTopLevelCommentsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<Comment>());

            return mock;
        }

        /// <summary>
        /// Creates a mock IFeedPostLikeRepository
        /// </summary>
        public static Mock<IFeedPostLikeRepository> CreateMockFeedPostLikeRepository()
        {
            var mock = new Mock<IFeedPostLikeRepository>();

            mock.Setup(r => r.GetUserLikeAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync((Like?)null);

            mock.Setup(r => r.AddOrUpdateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<LikeType>()))
                .ReturnsAsync((int postId, string userId, LikeType likeType) => 
                    (true, null, new Like { Id = 1, PostId = postId, UserId = userId, Type = likeType }));

            mock.Setup(r => r.HasUserLikedAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            return mock;
        }

        /// <summary>
        /// Creates a mock IFeedCommentLikeRepository
        /// </summary>
        public static Mock<IFeedCommentLikeRepository> CreateMockFeedCommentLikeRepository()
        {
            var mock = new Mock<IFeedCommentLikeRepository>();

            mock.Setup(r => r.GetUserLikeAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync((CommentLike?)null);

            mock.Setup(r => r.AddOrUpdateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<LikeType>()))
                .ReturnsAsync((int commentId, string userId, LikeType likeType) => 
                    (true, null, new CommentLike { Id = 1, CommentId = commentId, UserId = userId, Type = likeType }));

            mock.Setup(r => r.HasUserLikedAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            return mock;
        }

        /// <summary>
        /// Creates a mock IPinnedPostRepository
        /// </summary>
        public static Mock<IPinnedPostRepository> CreateMockPinnedPostRepository()
        {
            var mock = new Mock<IPinnedPostRepository>();

            mock.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((PinnedPost?)null);

            mock.Setup(r => r.IsPostPinnedAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            mock.Setup(r => r.GetPinnedPostsByPostIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<PinnedPost>());

            return mock;
        }

        /// <summary>
        /// Creates a mock IPostNotificationMuteRepository
        /// </summary>
        public static Mock<IPostNotificationMuteRepository> CreateMockPostNotificationMuteRepository()
        {
            var mock = new Mock<IPostNotificationMuteRepository>();

            mock.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((PostNotificationMute?)null);

            mock.Setup(r => r.IsPostMutedAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            mock.Setup(r => r.GetMuteRecordsByPostIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<PostNotificationMute>());

            return mock;
        }

        /// <summary>
        /// Creates a mock IGroupPostRepository
        /// </summary>
        public static Mock<IGroupPostRepository<GroupPost, GroupPostImage>> CreateMockGroupPostRepository()
        {
            var mock = new Mock<IGroupPostRepository<GroupPost, GroupPostImage>>();

            mock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((GroupPost?)null);

            mock.Setup(r => r.CreateAsync(It.IsAny<GroupPost>()))
                .ReturnsAsync((GroupPost post) => (true, null, post));

            mock.Setup(r => r.GetByGroupAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<GroupPost>());

            return mock;
        }

        /// <summary>
        /// Creates a mock IGroupCommentRepository
        /// </summary>
        public static Mock<IGroupCommentRepository> CreateMockGroupCommentRepository()
        {
            var mock = new Mock<IGroupCommentRepository>();

            mock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((GroupPostComment?)null);

            mock.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync((int postId, string authorId, string content, string? imageUrl, int? parentCommentId) => 
                    (true, null, new GroupPostComment { Id = 1, PostId = postId, AuthorId = authorId, Content = content }));

            mock.Setup(r => r.GetTopLevelCommentsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<GroupPostComment>());

            return mock;
        }

        /// <summary>
        /// Creates a mock IGroupPostLikeRepository
        /// </summary>
        public static Mock<IGroupPostLikeRepository> CreateMockGroupPostLikeRepository()
        {
            var mock = new Mock<IGroupPostLikeRepository>();

            mock.Setup(r => r.GetUserLikeAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync((GroupPostLike?)null);

            mock.Setup(r => r.AddOrUpdateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<LikeType>()))
                .ReturnsAsync((int postId, string userId, LikeType likeType) => 
                    (true, null, new GroupPostLike { Id = 1, PostId = postId, UserId = userId, Type = likeType }));

            mock.Setup(r => r.HasUserLikedAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            return mock;
        }

        /// <summary>
        /// Creates a mock IGroupCommentLikeRepository
        /// </summary>
        public static Mock<IGroupCommentLikeRepository> CreateMockGroupCommentLikeRepository()
        {
            var mock = new Mock<IGroupCommentLikeRepository>();

            mock.Setup(r => r.GetUserLikeAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync((GroupPostCommentLike?)null);

            mock.Setup(r => r.AddOrUpdateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<LikeType>()))
                .ReturnsAsync((int commentId, string userId, LikeType likeType) => 
                    (true, null, new GroupPostCommentLike { Id = 1, CommentId = commentId, UserId = userId, Type = likeType }));

            mock.Setup(r => r.HasUserLikedAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            return mock;
        }

        /// <summary>
        /// Creates a mock IGroupMemberRepository
        /// </summary>
        public static Mock<IGroupMemberRepository> CreateMockGroupMemberRepository()
        {
            var mock = new Mock<IGroupMemberRepository>();

            mock.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((GroupUser?)null);

            mock.Setup(r => r.GetMembershipAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync((GroupUser?)null);

            mock.Setup(r => r.IsMemberAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            mock.Setup(r => r.IsAdminAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            mock.Setup(r => r.GetGroupMembersAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<GroupUser>());

            mock.Setup(r => r.GetMemberCountAsync(It.IsAny<int>()))
                .ReturnsAsync(0);

            return mock;
        }

        /// <summary>
        /// Creates a mock FriendshipCacheService for testing.
        /// Returns empty friend lists by default. Configure specific behaviors in individual tests.
        /// </summary>
        public static Mock<FriendshipCacheService> CreateMockFriendshipCacheService()
        {
            // Create mocks for FriendshipCacheService dependencies
            var mockCache = new Mock<IMemoryCache>();
            var mockContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            var mockLogger = new Mock<ILogger<FriendshipCacheService>>();

            // Setup IMemoryCache to always return false (cache miss) by default
            object? cacheValue = null;
            mockCache.Setup(c => c.TryGetValue(It.IsAny<object>(), out cacheValue))
                .Returns(false);

            mockCache.Setup(c => c.CreateEntry(It.IsAny<object>()))
                .Returns(Mock.Of<ICacheEntry>());

            // Create the mock FriendshipCacheService
            var mock = new Mock<FriendshipCacheService>(
                mockCache.Object,
                mockContextFactory.Object,
                mockLogger.Object);

            // Setup default behaviors - return empty friend lists
            mock.Setup(s => s.GetUserFriendIdsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<string>());

            mock.Setup(s => s.GetUserFeedAuthorIdsAsync(It.IsAny<string>()))
                .ReturnsAsync((string userId) => (new List<string>(), new List<string> { userId }));

            mock.Setup(s => s.InvalidateUserFriendCache(It.IsAny<string>()))
                .Verifiable();

            mock.Setup(s => s.InvalidateFriendshipCache(It.IsAny<string>(), It.IsAny<string>()))
                .Verifiable();

            return mock;
        }

        /// <summary>
        /// Creates a mock IAuditLogRepository for testing.
        /// The mock accepts any audit log action without actually persisting it.
        /// </summary>
        public static Mock<IAuditLogRepository> CreateMockAuditLogRepository()
        {
            var mock = new Mock<IAuditLogRepository>();

            // Setup LogActionAsync to complete successfully
            mock.Setup(r => r.LogActionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Setup generic LogActionAsync to complete successfully
            mock.Setup(r => r.LogActionAsync(It.IsAny<string>(), It.IsAny<ActionCategory>(), It.IsAny<object>()))
                .Returns(Task.CompletedTask);

            // Setup query methods to return empty results
            mock.Setup(r => r.GetUserAuditLogsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<AuditLog>());

            mock.Setup(r => r.GetUserAuditLogCountAsync(It.IsAny<string>()))
                .ReturnsAsync(0);

            mock.Setup(r => r.SearchAuditLogsAsync(It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new List<AuditLog>());

            return mock;
        }
    }
}
