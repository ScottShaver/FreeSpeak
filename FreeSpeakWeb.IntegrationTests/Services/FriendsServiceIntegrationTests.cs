using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.IntegrationTests.Infrastructure;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace FreeSpeakWeb.IntegrationTests.Services
{
    public class FriendsServiceIntegrationTests : IntegrationTestBase
    {
        private static NotificationService CreateMockNotificationService()
        {
            // Create a no-op notification service for testing
            // We're testing FriendsService, not notifications
            return new NullNotificationService();
        }

        private static UserPreferenceService CreateMockUserPreferenceService()
        {
            // Create a no-op user preference service for testing
            return new NullUserPreferenceService();
        }

        // Null implementation of NotificationService for testing
        private class NullNotificationService : NotificationService
        {
            public NullNotificationService()
                : base(null!, new NullDbContextFactory(), new NullLogger(), new NullServiceScopeFactory())
            {
            }

            private class NullDbContextFactory : IDbContextFactory<ApplicationDbContext>
            {
                public ApplicationDbContext CreateDbContext() => null!;
                public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult<ApplicationDbContext>(null!);
            }

            private class NullLogger : ILogger<NotificationService>
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => false;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
            }

            private class NullServiceScopeFactory : IServiceScopeFactory
            {
                public IServiceScope CreateScope() => new NullServiceScope();

                private class NullServiceScope : IServiceScope
                {
                    public IServiceProvider ServiceProvider => new NullServiceProvider();
                    public void Dispose() { }

                    private class NullServiceProvider : IServiceProvider
                    {
                        public object? GetService(Type serviceType) => null;
                    }
                }
            }
        }

        // Null implementation of UserPreferenceService for testing
        private class NullUserPreferenceService : UserPreferenceService
        {
            public NullUserPreferenceService()
                : base(new NullDbContextFactory(), new NullLogger())
            {
            }

            private class NullDbContextFactory : IDbContextFactory<ApplicationDbContext>
            {
                public ApplicationDbContext CreateDbContext() => null!;
            }

            private class NullLogger : ILogger<UserPreferenceService>
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => false;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
            }
        }

        [Fact]
        public async Task SearchUsersAsync_WithMatchingFirstName_ShouldReturnResults()
        {
            // Arrange
            var factory = CreateDbContextFactory();
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = (FreeSpeakWeb.Repositories.Abstractions.IFriendshipRepository)null!;            var userRepo = (FreeSpeakWeb.Repositories.Abstractions.IUserRepository)null!;            var friendshipCache = (FreeSpeakWeb.Services.FriendshipCacheService)null!;
            var service = new FriendsService(friendshipRepo, userRepo, factory, notificationService, userPreferenceService, friendshipCache, MockRepositories.CreateMockAuditLogRepository().Object);

            // Create test users
            var currentUser = CreateTestUser("current", "CurrentUser", "Current", "User");
            var john = CreateTestUser("john1", "john_doe", "John", "Doe");
            var johnny = CreateTestUser("johnny1", "johnny_test", "Johnny", "Test");
            var jane = CreateTestUser("jane1", "jane_smith", "Jane", "Smith");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(currentUser, john, johnny, jane);
                await context.SaveChangesAsync();
            }

            // Act
            var results = await service.SearchUsersAsync("john", "current");

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(u => u.Id == "john1");
            results.Should().Contain(u => u.Id == "johnny1");
            results.Should().NotContain(u => u.Id == "jane1");
        }

        [Fact]
        public async Task SearchUsersAsync_WithMatchingLastName_ShouldReturnResults()
        {
            // Arrange
            var factory = CreateDbContextFactory();
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = (FreeSpeakWeb.Repositories.Abstractions.IFriendshipRepository)null!;            var userRepo = (FreeSpeakWeb.Repositories.Abstractions.IUserRepository)null!;            var friendshipCache = (FreeSpeakWeb.Services.FriendshipCacheService)null!;
            var service = new FriendsService(friendshipRepo, userRepo, factory, notificationService, userPreferenceService, friendshipCache, MockRepositories.CreateMockAuditLogRepository().Object);

            var currentUser = CreateTestUser("current", "current", "Current", "User");
            var user1 = CreateTestUser("user1", "alice", "Alice", "Smith");
            var user2 = CreateTestUser("user2", "bob", "Bob", "Smith");
            var user3 = CreateTestUser("user3", "charlie", "Charlie", "Jones");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(currentUser, user1, user2, user3);
                await context.SaveChangesAsync();
            }

            // Act
            var results = await service.SearchUsersAsync("smith", "current");

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(u => u.Id == "user1");
            results.Should().Contain(u => u.Id == "user2");
        }

        [Fact]
        public async Task SearchUsersAsync_WithMatchingUsername_ShouldReturnResults()
        {
            // Arrange
            var factory = CreateDbContextFactory();
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = (FreeSpeakWeb.Repositories.Abstractions.IFriendshipRepository)null!;            var userRepo = (FreeSpeakWeb.Repositories.Abstractions.IUserRepository)null!;            var friendshipCache = (FreeSpeakWeb.Services.FriendshipCacheService)null!;
            var service = new FriendsService(friendshipRepo, userRepo, factory, notificationService, userPreferenceService, friendshipCache, MockRepositories.CreateMockAuditLogRepository().Object);

            var currentUser = CreateTestUser("current", "current", "Current", "User");
            var user1 = CreateTestUser("user1", "developer123", "Alice", "Smith");
            var user2 = CreateTestUser("user2", "dev_master", "Bob", "Jones");
            var user3 = CreateTestUser("user3", "designer456", "Charlie", "Brown");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(currentUser, user1, user2, user3);
                await context.SaveChangesAsync();
            }

            // Act
            var results = await service.SearchUsersAsync("dev", "current");

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(u => u.Id == "user1");
            results.Should().Contain(u => u.Id == "user2");
        }

        [Fact]
        public async Task SearchUsersAsync_WithMultipleWords_ShouldMatchAnyWord()
        {
            // Arrange
            var factory = CreateDbContextFactory();
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = (FreeSpeakWeb.Repositories.Abstractions.IFriendshipRepository)null!;            var userRepo = (FreeSpeakWeb.Repositories.Abstractions.IUserRepository)null!;            var friendshipCache = (FreeSpeakWeb.Services.FriendshipCacheService)null!;
            var service = new FriendsService(friendshipRepo, userRepo, factory, notificationService, userPreferenceService, friendshipCache, MockRepositories.CreateMockAuditLogRepository().Object);

            var currentUser = CreateTestUser("current", "current", "Current", "User");
            var user1 = CreateTestUser("user1", "alice", "Alice", "Johnson");
            var user2 = CreateTestUser("user2", "bob", "Bob", "Smith");
            var user3 = CreateTestUser("user3", "charlie", "Charlie", "Brown");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(currentUser, user1, user2, user3);
                await context.SaveChangesAsync();
            }

            // Act - Search for "alice smith" should match both Alice and Bob Smith
            var results = await service.SearchUsersAsync("alice smith", "current");

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(u => u.Id == "user1"); // Matches Alice
            results.Should().Contain(u => u.Id == "user2"); // Matches Smith
        }

        [Fact]
        public async Task SearchUsersAsync_ShouldExcludeExistingConnections()
        {
            // Arrange
            var factory = CreateDbContextFactory();
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = (FreeSpeakWeb.Repositories.Abstractions.IFriendshipRepository)null!;            var userRepo = (FreeSpeakWeb.Repositories.Abstractions.IUserRepository)null!;            var friendshipCache = (FreeSpeakWeb.Services.FriendshipCacheService)null!;
            var service = new FriendsService(friendshipRepo, userRepo, factory, notificationService, userPreferenceService, friendshipCache, MockRepositories.CreateMockAuditLogRepository().Object);

            var currentUser = CreateTestUser("current", "current", "Current", "User");
            var friend = CreateTestUser("friend", "friend", "Friend", "User");
            var stranger = CreateTestUser("stranger", "stranger", "Stranger", "User");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(currentUser, friend, stranger);
                
                // Create friendship
                context.Friendships.Add(new Friendship
                {
                    RequesterId = "current",
                    AddresseeId = "friend",
                    Status = FriendshipStatus.Accepted,
                    RequestedAt = DateTime.UtcNow
                });
                
                await context.SaveChangesAsync();
            }

            // Act
            var results = await service.SearchUsersAsync("user", "current");

            // Assert
            results.Should().HaveCount(1);
            results.Should().Contain(u => u.Id == "stranger");
            results.Should().NotContain(u => u.Id == "friend"); // Already connected
            results.Should().NotContain(u => u.Id == "current"); // Exclude self
        }

        [Fact]
        public async Task SearchUsersAsync_WithCaseInsensitive_ShouldReturnResults()
        {
            // Arrange
            var factory = CreateDbContextFactory();
            var notificationService = CreateMockNotificationService();
            var userPreferenceService = CreateMockUserPreferenceService();
            var friendshipRepo = (FreeSpeakWeb.Repositories.Abstractions.IFriendshipRepository)null!;            var userRepo = (FreeSpeakWeb.Repositories.Abstractions.IUserRepository)null!;            var friendshipCache = (FreeSpeakWeb.Services.FriendshipCacheService)null!;
            var service = new FriendsService(friendshipRepo, userRepo, factory, notificationService, userPreferenceService, friendshipCache, MockRepositories.CreateMockAuditLogRepository().Object);

            var currentUser = CreateTestUser("current", "current", "Current", "User");
            var user1 = CreateTestUser("user1", "TechGuru", "Michael", "Johnson");

            await using (var context = CreateDbContext())
            {
                context.Users.AddRange(currentUser, user1);
                await context.SaveChangesAsync();
            }

            // Act - Search with different case
            var results = await service.SearchUsersAsync("TECHGURU", "current");

            // Assert
            results.Should().HaveCount(1);
            results.Should().Contain(u => u.Id == "user1");
        }

        private ApplicationUser CreateTestUser(string id, string userName, string firstName, string lastName)
        {
            return new ApplicationUser
            {
                Id = id,
                UserName = userName,
                NormalizedUserName = userName.ToUpper(),
                Email = $"{userName}@example.com",
                NormalizedEmail = $"{userName.ToUpper()}@EXAMPLE.COM",
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                SecurityStamp = Guid.NewGuid().ToString()
            };
        }
    }
}



