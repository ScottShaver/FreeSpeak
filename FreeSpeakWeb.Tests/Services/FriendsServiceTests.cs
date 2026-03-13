using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    public class FriendsServiceTests : TestBase
    {
        private static NotificationService CreateMockNotificationService()
        {
            var notificationRepo = MockRepositories.CreateMockNotificationRepository();
            var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            var logger = new Mock<ILogger<NotificationService>>();
            var scopeFactory = new Mock<IServiceScopeFactory>();
            return new NotificationService(notificationRepo.Object, dbFactory.Object, logger.Object, scopeFactory.Object);
        }

        private static UserPreferenceService CreateMockUserPreferenceService()
        {
            var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            var logger = new Mock<ILogger<UserPreferenceService>>();
            return new UserPreferenceService(dbFactory.Object, logger.Object);
        }

        [Fact]
        public async Task SendFriendRequestAsync_WithValidUsers_ShouldCreatePendingFriendship()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest1");
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();
            var userRepo = MockRepositories.CreateMockUserRepository();
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1", userName: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2", userName: "user2");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.SendFriendRequestAsync("user1", "user2");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var friendship = context.Friendships.FirstOrDefault();
                friendship.Should().NotBeNull();
                friendship!.RequesterId.Should().Be("user1");
                friendship.AddresseeId.Should().Be("user2");
                friendship.Status.Should().Be(FriendshipStatus.Pending);
            }
        }

        [Fact]
        public async Task SendFriendRequestAsync_ToSelf_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest2");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            // Act
            var (success, errorMessage) = await service.SendFriendRequestAsync("user1", "user1");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Be("You cannot send a friend request to yourself.");
        }

        [Fact]
        public async Task SendFriendRequestAsync_WhenAlreadyPending_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest3");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var friendship = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Pending);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Friendships.Add(friendship);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.SendFriendRequestAsync("user1", "user2");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("pending");
        }

        [Fact]
        public async Task AcceptFriendRequestAsync_WithValidRequest_ShouldUpdateStatusToAccepted()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest4");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var friendship = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Pending);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Friendships.Add(friendship);
                await context.SaveChangesAsync();
            }

            int friendshipId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                friendshipId = context.Friendships.First().Id;
            }

            // Act
            var (success, errorMessage) = await service.AcceptFriendRequestAsync(friendshipId, "user2");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var updatedFriendship = context.Friendships.Find(friendshipId);
                updatedFriendship.Should().NotBeNull();
                updatedFriendship!.Status.Should().Be(FriendshipStatus.Accepted);
                updatedFriendship.RespondedAt.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task AcceptFriendRequestAsync_ByNonAddressee_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest5");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var user3 = TestDataFactory.CreateTestUser(id: "user3");
            var friendship = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Pending);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2, user3);
                context.Friendships.Add(friendship);
                await context.SaveChangesAsync();
            }

            int friendshipId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                friendshipId = context.Friendships.First().Id;
            }

            // Act
            var (success, errorMessage) = await service.AcceptFriendRequestAsync(friendshipId, "user3");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not authorized");
        }

        [Fact]
        public async Task GetFriendsAsync_WithAcceptedFriendships_ShouldReturnFriendsList()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest6");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1", userName: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2", userName: "user2");
            var user3 = TestDataFactory.CreateTestUser(id: "user3", userName: "user3");

            var friendship1 = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Accepted);
            var friendship2 = TestDataFactory.CreateTestFriendship("user3", "user1", FriendshipStatus.Accepted);
            var friendship3 = TestDataFactory.CreateTestFriendship("user1", "user3", FriendshipStatus.Pending);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2, user3);
                context.Friendships.AddRange(friendship1, friendship2);
                await context.SaveChangesAsync();
            }

            // Act
            var friends = await service.GetFriendsAsync("user1");

            // Assert
            friends.Should().HaveCount(2);
            friends.Should().Contain(u => u.Id == "user2");
            friends.Should().Contain(u => u.Id == "user3");
        }

        [Fact]
        public async Task AreFriendsAsync_WithAcceptedFriendship_ShouldReturnTrue()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest7");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var friendship = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Accepted);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Friendships.Add(friendship);
                await context.SaveChangesAsync();
            }

            // Act
            var areFriends = await service.AreFriendsAsync("user1", "user2");

            // Assert
            areFriends.Should().BeTrue();
        }

        [Fact]
        public async Task AreFriendsAsync_WithPendingFriendship_ShouldReturnFalse()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest8");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var friendship = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Pending);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Friendships.Add(friendship);
                await context.SaveChangesAsync();
            }

            // Act
            var areFriends = await service.AreFriendsAsync("user1", "user2");

            // Assert
            areFriends.Should().BeFalse();
        }

        [Fact(Skip = "InMemory database doesn't support complex string operations in LINQ. Use integration tests with real database.")]
        public async Task SearchUsersAsync_WithMatchingUsers_ShouldReturnResults()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest9");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var currentUser = TestDataFactory.CreateTestUser(id: "current", userName: "current");
            var user1 = TestDataFactory.CreateTestUser(id: "user1", userName: "john", firstName: "John", lastName: "Doe");
            var user2 = TestDataFactory.CreateTestUser(id: "user2", userName: "jane", firstName: "Jane", lastName: "Smith");
            var user3 = TestDataFactory.CreateTestUser(id: "user3", userName: "johnny", firstName: "Johnny", lastName: "Test");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(currentUser, user1, user2, user3);
                await context.SaveChangesAsync();
            }

            // Act
            var results = await service.SearchUsersAsync("john", "current");

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(u => u.Id == "user1");
            results.Should().Contain(u => u.Id == "user3");
        }

        [Fact]
        public async Task GetPendingRequestsAsync_ShouldReturnOnlyPendingRequestsForUser()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest10");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var user3 = TestDataFactory.CreateTestUser(id: "user3");

            var pendingRequest = TestDataFactory.CreateTestFriendship("user2", "user1", FriendshipStatus.Pending);
            var acceptedRequest = TestDataFactory.CreateTestFriendship("user3", "user1", FriendshipStatus.Accepted);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2, user3);
                context.Friendships.AddRange(pendingRequest, acceptedRequest);
                await context.SaveChangesAsync();
            }

            // Act
            var requests = await service.GetPendingRequestsAsync("user1");

            // Assert
            requests.Should().HaveCount(1);
            requests[0].Requester.Id.Should().Be("user2");
        }

        [Fact]
        public async Task BlockUserAsync_ShouldCreateBlockedFriendship()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest11");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.BlockUserAsync("user1", "user2");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var friendship = context.Friendships.FirstOrDefault();
                friendship.Should().NotBeNull();
                friendship!.Status.Should().Be(FriendshipStatus.Blocked);
            }
        }

        [Fact]
        public async Task GetMutualFriendsAsync_ShouldReturnCommonFriends()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsTest12");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = MockRepositories.CreateMockFriendshipRepository();            var userRepo = MockRepositories.CreateMockUserRepository();            var service = new FriendsService(friendshipRepo.Object, userRepo.Object, dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var mutual1 = TestDataFactory.CreateTestUser(id: "mutual1");
            var mutual2 = TestDataFactory.CreateTestUser(id: "mutual2");
            var notMutual = TestDataFactory.CreateTestUser(id: "notMutual");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2, mutual1, mutual2, notMutual);
                
                // User1 friends
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user1", "mutual1", FriendshipStatus.Accepted));
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user1", "mutual2", FriendshipStatus.Accepted));
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user1", "notMutual", FriendshipStatus.Accepted));
                
                // User2 friends
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user2", "mutual1", FriendshipStatus.Accepted));
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user2", "mutual2", FriendshipStatus.Accepted));
                
                await context.SaveChangesAsync();
            }

            // Act
            var mutualFriends = await service.GetMutualFriendsAsync("user1", "user2");

            // Assert
            mutualFriends.Should().HaveCount(2);
            mutualFriends.Should().Contain(u => u.Id == "mutual1");
            mutualFriends.Should().Contain(u => u.Id == "mutual2");
        }
    }
}

