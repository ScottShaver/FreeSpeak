using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
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
    }
}
