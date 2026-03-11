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
    public class FriendsServiceEdgeCaseTests : TestBase
    {
        private static NotificationService CreateMockNotificationService()
        {
            var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            var logger = new Mock<ILogger<NotificationService>>();
            var scopeFactory = new Mock<IServiceScopeFactory>();
            return new NotificationService(dbFactory.Object, logger.Object, scopeFactory.Object);
        }

        private static UserPreferenceService CreateMockUserPreferenceService()
        {
            var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            var logger = new Mock<ILogger<UserPreferenceService>>();
            return new UserPreferenceService(dbFactory.Object, logger.Object);
        }

        [Fact]
        public async Task RejectFriendRequestAsync_WithValidRequest_ShouldUpdateStatus()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsEdge1");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(dbFactory, notificationService, userPreferenceService);

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
            var (success, errorMessage) = await service.RejectFriendRequestAsync(friendshipId, "user2");

            // Assert
            success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var rejectedFriendship = context.Friendships.Find(friendshipId);
                rejectedFriendship!.Status.Should().Be(FriendshipStatus.Rejected);
                rejectedFriendship.RespondedAt.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task RemoveFriendAsync_WithAcceptedFriendship_ShouldDeleteFriendship()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsEdge2");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var friendship = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Accepted);

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
            var (success, errorMessage) = await service.RemoveFriendAsync(friendshipId, "user1");

            // Assert
            success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var deletedFriendship = context.Friendships.Find(friendshipId);
                deletedFriendship.Should().BeNull();
            }
        }

        [Fact]
        public async Task GetFriendsCountAsync_WithMultipleFriendships_ShouldReturnCorrectCount()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsEdge3");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var user3 = TestDataFactory.CreateTestUser(id: "user3");
            var user4 = TestDataFactory.CreateTestUser(id: "user4");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2, user3, user4);
                
                // User1 has 2 accepted friends and 1 pending
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Accepted));
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user3", "user1", FriendshipStatus.Accepted));
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user1", "user4", FriendshipStatus.Pending));
                
                await context.SaveChangesAsync();
            }

            // Act
            var count = await service.GetFriendsCountAsync("user1");

            // Assert
            count.Should().Be(2); // Only accepted friendships
        }

        [Fact]
        public async Task GetPendingRequestsCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsEdge4");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var user3 = TestDataFactory.CreateTestUser(id: "user3");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2, user3);
                
                // User1 has 2 pending received requests
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user2", "user1", FriendshipStatus.Pending));
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user3", "user1", FriendshipStatus.Pending));
                
                await context.SaveChangesAsync();
            }

            // Act
            var count = await service.GetPendingRequestsCountAsync("user1");

            // Assert
            count.Should().Be(2);
        }

        [Fact]
        public async Task GetSentRequestsAsync_ShouldReturnOnlySentRequests()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsEdge5");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var user3 = TestDataFactory.CreateTestUser(id: "user3");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2, user3);
                
                // User1 sent request to user2
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Pending));
                // User3 sent request to user1 (received, not sent)
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user3", "user1", FriendshipStatus.Pending));
                
                await context.SaveChangesAsync();
            }

            // Act
            var sentRequests = await service.GetSentRequestsAsync("user1");

            // Assert
            sentRequests.Should().HaveCount(1);
            sentRequests[0].Addressee.Id.Should().Be("user2");
        }

        [Fact]
        public async Task BlockUserAsync_WithExistingFriendship_ShouldUpdateToBlocked()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsEdge6");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(dbFactory, notificationService, userPreferenceService);

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
            var (success, errorMessage) = await service.BlockUserAsync("user1", "user2");

            // Assert
            success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var blockedFriendship = context.Friendships.FirstOrDefault();
                blockedFriendship.Should().NotBeNull();
                blockedFriendship!.Status.Should().Be(FriendshipStatus.Blocked);
            }
        }

        [Fact]
        public async Task GetPeopleYouMayKnowAsync_WithNoFriends_ShouldReturnEmpty()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsEdge7");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user1);
                await context.SaveChangesAsync();
            }

            // Act
            var suggestions = await service.GetPeopleYouMayKnowAsync("user1");

            // Assert
            suggestions.Should().BeEmpty();
        }

        [Fact]
        public async Task GetPeopleYouMayKnowAsync_WithMutualFriends_ShouldReturnSuggestions()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsEdge8");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var friend1 = TestDataFactory.CreateTestUser(id: "friend1");
            var friend2 = TestDataFactory.CreateTestUser(id: "friend2");
            var suggested = TestDataFactory.CreateTestUser(id: "suggested");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, friend1, friend2, suggested);
                
                // User1 is friends with friend1 and friend2
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user1", "friend1", FriendshipStatus.Accepted));
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("user1", "friend2", FriendshipStatus.Accepted));
                
                // Both friend1 and friend2 are friends with suggested user
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("friend1", "suggested", FriendshipStatus.Accepted));
                context.Friendships.Add(TestDataFactory.CreateTestFriendship("friend2", "suggested", FriendshipStatus.Accepted));
                
                await context.SaveChangesAsync();
            }

            // Act
            var suggestions = await service.GetPeopleYouMayKnowAsync("user1");

            // Assert
            suggestions.Should().HaveCount(1);
            suggestions[0].User.Id.Should().Be("suggested");
            suggestions[0].MutualFriendsCount.Should().Be(2);
        }

        [Fact]
        public async Task GetFriendshipStatusAsync_WithNoRelationship_ShouldReturnNull()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsEdge9");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                await context.SaveChangesAsync();
            }

            // Act
            var status = await service.GetFriendshipStatusAsync("user1", "user2");

            // Assert
            status.Should().BeNull();
        }

        [Fact]
        public async Task GetFriendshipAsync_ShouldReturnBidirectional()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("FriendsEdge10");
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var service = new FriendsService(dbFactory, notificationService, userPreferenceService);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var friendship = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Accepted);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Friendships.Add(friendship);
                await context.SaveChangesAsync();
            }

            // Act - Check both directions
            var result1 = await service.GetFriendshipAsync("user1", "user2");
            var result2 = await service.GetFriendshipAsync("user2", "user1");

            // Assert
            result1.Should().NotBeNull();
            result2.Should().NotBeNull();
            result1!.Id.Should().Be(result2!.Id); // Same friendship regardless of order
        }
    }
}
