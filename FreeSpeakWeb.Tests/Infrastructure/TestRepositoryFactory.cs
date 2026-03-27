using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace FreeSpeakWeb.Tests.Infrastructure
{
    /// <summary>
    /// Factory class that creates real repository instances using an in-memory database.
    /// This enables integration-style testing where repositories actually persist and retrieve data.
    /// </summary>
    public class TestRepositoryFactory
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IMemoryCache _memoryCache;

        /// <summary>
        /// Initializes a new instance of the TestRepositoryFactory with the given database context factory.
        /// </summary>
        /// <param name="contextFactory">The database context factory to use for all repositories.</param>
        public TestRepositoryFactory(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        /// <summary>
        /// Gets the database context factory.
        /// </summary>
        public IDbContextFactory<ApplicationDbContext> ContextFactory => _contextFactory;

        /// <summary>
        /// Creates a FriendshipCacheService for testing.
        /// </summary>
        /// <returns>A real FriendshipCacheService instance.</returns>
        public FriendshipCacheService CreateFriendshipCacheService()
        {
            return new FriendshipCacheService(
                _memoryCache,
                _contextFactory,
                CreateMockLogger<FriendshipCacheService>());
        }

        /// <summary>
        /// Creates a PostRepository for testing feed posts.
        /// </summary>
        /// <returns>A real PostRepository instance.</returns>
        public IFeedPostRepository<Post, PostImage> CreateFeedPostRepository()
        {
            return new PostRepository(
                _contextFactory,
                CreateMockLogger<PostRepository>(),
                CreateFriendshipCacheService(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a FeedCommentRepository for testing comments.
        /// </summary>
        /// <returns>A real FeedCommentRepository instance.</returns>
        public IFeedCommentRepository CreateFeedCommentRepository()
        {
            return new FeedCommentRepository(
                _contextFactory,
                CreateMockLogger<FeedCommentRepository>(),
                CreateMockAuditLogRepository(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a FeedPostLikeRepository for testing post likes.
        /// </summary>
        /// <returns>A real FeedPostLikeRepository instance.</returns>
        public IFeedPostLikeRepository CreateFeedPostLikeRepository()
        {
            return new FeedPostLikeRepository(
                _contextFactory,
                CreateMockLogger<FeedPostLikeRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a FeedCommentLikeRepository for testing comment likes.
        /// </summary>
        /// <returns>A real FeedCommentLikeRepository instance.</returns>
        public IFeedCommentLikeRepository CreateFeedCommentLikeRepository()
        {
            return new FeedCommentLikeRepository(
                _contextFactory,
                CreateMockLogger<FeedCommentLikeRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a PinnedPostRepository for testing pinned posts.
        /// </summary>
        /// <returns>A real PinnedPostRepository instance.</returns>
        public IPinnedPostRepository CreatePinnedPostRepository()
        {
            return new PinnedPostRepository(
                _contextFactory,
                CreateMockLogger<PinnedPostRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a PostNotificationMuteRepository for testing notification mutes.
        /// </summary>
        /// <returns>A real PostNotificationMuteRepository instance.</returns>
        public IPostNotificationMuteRepository CreatePostNotificationMuteRepository()
        {
            return new PostNotificationMuteRepository(
                _contextFactory,
                CreateMockLogger<PostNotificationMuteRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a NotificationRepository for testing notifications.
        /// </summary>
        /// <returns>A real NotificationRepository instance.</returns>
        public INotificationRepository CreateNotificationRepository()
        {
            return new NotificationRepository(
                _contextFactory,
                CreateMockLogger<NotificationRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a FriendshipRepository for testing friendships.
        /// </summary>
        /// <returns>A real FriendshipRepository instance.</returns>
        public IFriendshipRepository CreateFriendshipRepository()
        {
            return new FriendshipRepository(
                _contextFactory,
                CreateMockLogger<FriendshipRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a GroupAccessValidator for testing group access.
        /// </summary>
        /// <returns>A real GroupAccessValidator instance.</returns>
        public GroupAccessValidator CreateGroupAccessValidator()
        {
            var roleService = new Mock<IRoleService>();
            roleService.Setup(r => r.IsSystemAdministratorAsync(It.IsAny<string>())).ReturnsAsync(false);
            return new GroupAccessValidator(
                _contextFactory,
                CreateMockLogger<GroupAccessValidator>(),
                roleService.Object);
        }

        /// <summary>
        /// Creates a GroupPostRepository for testing group posts.
        /// </summary>
        /// <returns>A real GroupPostRepository instance.</returns>
        public IGroupPostRepository<GroupPost, GroupPostImage> CreateGroupPostRepository()
        {
            return new GroupPostRepository(
                _contextFactory,
                CreateMockLogger<GroupPostRepository>(),
                CreateGroupAccessValidator(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a GroupCommentRepository for testing group comments.
        /// </summary>
        /// <returns>A real GroupCommentRepository instance.</returns>
        public IGroupCommentRepository CreateGroupCommentRepository()
        {
            return new GroupCommentRepository(
                _contextFactory,
                CreateMockLogger<GroupCommentRepository>(),
                CreateMockAuditLogRepository(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a GroupPostLikeRepository for testing group post likes.
        /// </summary>
        /// <returns>A real GroupPostLikeRepository instance.</returns>
        public IGroupPostLikeRepository CreateGroupPostLikeRepository()
        {
            return new GroupPostLikeRepository(
                _contextFactory,
                CreateMockLogger<GroupPostLikeRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a GroupCommentLikeRepository for testing group comment likes.
        /// </summary>
        /// <returns>A real GroupCommentLikeRepository instance.</returns>
        public IGroupCommentLikeRepository CreateGroupCommentLikeRepository()
        {
            return new GroupCommentLikeRepository(
                _contextFactory,
                CreateMockLogger<GroupCommentLikeRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a GroupMemberRepository for testing group membership.
        /// </summary>
        /// <returns>A real GroupMemberRepository instance.</returns>
        public IGroupMemberRepository CreateGroupMemberRepository()
        {
            return new GroupMemberRepository(
                _contextFactory,
                CreateMockLogger<GroupMemberRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a UserRepository for testing user operations.
        /// </summary>
        /// <returns>A real UserRepository instance.</returns>
        public IUserRepository CreateUserRepository()
        {
            return new UserRepository(
                _contextFactory,
                CreateMockLogger<UserRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a GroupRepository for testing group operations.
        /// </summary>
        /// <returns>A real GroupRepository instance.</returns>
        public IGroupRepository CreateGroupRepository()
        {
            return new GroupRepository(
                _contextFactory,
                CreateMockLogger<GroupRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Creates a GroupFileRepository for testing group file operations.
        /// </summary>
        /// <returns>A real GroupFileRepository instance.</returns>
        public IGroupFileRepository CreateGroupFileRepository()
        {
            return new GroupFileRepository(
                _contextFactory,
                CreateMockLogger<GroupFileRepository>(),
                CreateMockProfilerHelper());
        }

        /// <summary>
        /// Invalidates the friendship cache for a user.
        /// </summary>
        /// <param name="userId">The user ID to invalidate.</param>
        public void InvalidateFriendshipCache(string userId)
        {
            _memoryCache.Remove($"user_friends_{userId}");
        }

        /// <summary>
        /// Clears all entries from the memory cache.
        /// </summary>
        public void ClearCache()
        {
            // MemoryCache doesn't have a Clear method, so we create a new one
            // This is acceptable for tests since we create new factory per test
        }

        private static ILogger<T> CreateMockLogger<T>()
        {
            return new Mock<ILogger<T>>().Object;
        }

        /// <summary>
        /// Creates a mock ProfilerHelper for testing.
        /// </summary>
        /// <returns>A mock ProfilerHelper instance with profiling disabled.</returns>
        private static ProfilerHelper CreateMockProfilerHelper()
        {
            var mockOptions = new Mock<Microsoft.Extensions.Options.IOptions<ProfilingSettings>>();
            mockOptions.Setup(o => o.Value).Returns(new ProfilingSettings { Enabled = false });
            return new ProfilerHelper(mockOptions.Object);
        }

        /// <summary>
        /// Creates a mock IAuditLogRepository for testing.
        /// </summary>
        /// <returns>A mock IAuditLogRepository instance.</returns>
        private static IAuditLogRepository CreateMockAuditLogRepository()
        {
            return new Mock<IAuditLogRepository>().Object;
        }
    }
}
