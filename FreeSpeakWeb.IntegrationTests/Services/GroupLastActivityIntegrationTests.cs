using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.IntegrationTests.Infrastructure;
using FreeSpeakWeb.Repositories;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeSpeakWeb.IntegrationTests.Services
{
    /// <summary>
    /// Integration tests for the group "last activity" lookup in <see cref="GroupPostRepository"/>.
    /// </summary>
    public class GroupLastActivityIntegrationTests : IntegrationTestBase
    {
        private GroupPostRepository CreateRepository()
        {
            var factory = CreateDbContextFactory();
            var validator = new GroupAccessValidator(factory, NullLogger<GroupAccessValidator>.Instance, Mock.Of<IRoleService>());
            var profiler = new ProfilerHelper(Options.Create(new ProfilingSettings { Enabled = false }));
            return new GroupPostRepository(factory, CreateLogger<GroupPostRepository>(), validator, profiler);
        }

        private async Task<int> SeedGroupAsync(params (PostStatus Status, DateTime CreatedAt)[] posts)
        {
            var userId = "user-" + Guid.NewGuid().ToString("N");
            await using var context = CreateDbContext();
            context.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                NormalizedUserName = userId.ToUpper(),
                Email = $"{userId}@example.com",
                NormalizedEmail = $"{userId}@example.com".ToUpper(),
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "User",
                SecurityStamp = Guid.NewGuid().ToString()
            });
            var group = new Group
            {
                CreatorId = userId,
                Name = "Activity group " + Guid.NewGuid(),
                Description = "d",
                IsPublic = true,
                CreatedAt = DateTime.UtcNow.AddDays(-100),
                LastActiveAt = DateTime.UtcNow
            };
            context.Groups.Add(group);
            await context.SaveChangesAsync();

            foreach (var (status, createdAt) in posts)
            {
                context.GroupPosts.Add(new GroupPost { GroupId = group.Id, AuthorId = userId, Content = "c", Status = status, CreatedAt = createdAt });
            }
            await context.SaveChangesAsync();
            return group.Id;
        }

        /// <summary>Pending and declined posts must not move the last activity time.</summary>
        [Fact]
        public async Task GetLastPostCreatedAtByGroupAsync_IgnoresPendingAndDeclinedPosts()
        {
            var posted = DateTime.UtcNow.AddDays(-3);
            var groupId = await SeedGroupAsync(
                (PostStatus.Posted, DateTime.UtcNow.AddDays(-10)),
                (PostStatus.Posted, posted),
                (PostStatus.Pending, DateTime.UtcNow.AddHours(-1)),
                (PostStatus.Declined, DateTime.UtcNow.AddMinutes(-5)));

            var result = await CreateRepository().GetLastPostCreatedAtByGroupAsync(groupId);

            result.Should().NotBeNull();
            result!.Value.Should().BeCloseTo(posted, TimeSpan.FromSeconds(1));
        }

        /// <summary>A group without published posts has no last post time.</summary>
        [Fact]
        public async Task GetLastPostCreatedAtByGroupAsync_NoPublishedPosts_ReturnsNull()
        {
            var groupId = await SeedGroupAsync((PostStatus.Pending, DateTime.UtcNow));

            var result = await CreateRepository().GetLastPostCreatedAtByGroupAsync(groupId);

            result.Should().BeNull();
        }
    }
}
