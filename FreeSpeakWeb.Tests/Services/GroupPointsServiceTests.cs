using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Unit tests for GroupPointsService covering point awarding, retrieval, and level calculations.
    /// </summary>
    public class GroupPointsServiceTests : TestBase
    {
        /// <summary>
        /// Creates a GroupPointsService instance with an in-memory database.
        /// </summary>
        private GroupPointsService CreateGroupPointsService(TestRepositoryFactory repoFactory)
        {
            var logger = CreateMockLogger<GroupPointsService>();
            return new GroupPointsService(repoFactory.ContextFactory, logger);
        }

        #region Award Points Tests

        [Fact]
        public async Task AwardPointsAsync_WithValidUser_ShouldIncreasePoints()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest1");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1", enablePointsSystem: true);
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardPointsAsync("user1", group.Id, 20);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(20);

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var updatedGroupUser = await context.GroupUsers
                    .FirstOrDefaultAsync(gu => gu.UserId == "user1" && gu.GroupId == group.Id);
                updatedGroupUser.Should().NotBeNull();
                updatedGroupUser!.GroupPoints.Should().Be(20);
            }
        }

        [Fact]
        public async Task AwardPointsAsync_WithMultipleAwards_ShouldAccumulatePoints()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest2");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1", enablePointsSystem: true);
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            await service.AwardPointsAsync("user1", group.Id, 20); // Post
            await service.AwardPointsAsync("user1", group.Id, 5);  // Comment
            var (success, finalTotal) = await service.AwardPointsAsync("user1", group.Id, 1);  // Like

            // Assert
            success.Should().BeTrue();
            finalTotal.Should().Be(26);
        }

        [Fact]
        public async Task AwardPointsAsync_WithNegativePoints_ShouldDecreasePoints()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest3");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1", enablePointsSystem: true);
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            groupUser.GroupPoints = 100;

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardPointsAsync("user1", group.Id, -30);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(70);
        }

        [Fact]
        public async Task AwardPointsAsync_WithPointsBelowZero_ShouldClampToZero()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest4");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1", enablePointsSystem: true);
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            groupUser.GroupPoints = 10;

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardPointsAsync("user1", group.Id, -50);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(0);
        }

        [Fact]
        public async Task AwardPointsAsync_WithNullUserId_ShouldReturnFailure()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest5");
            var service = CreateGroupPointsService(repoFactory);

            // Act
            var (success, newTotal) = await service.AwardPointsAsync(null!, 1, 20);

            // Assert
            success.Should().BeFalse();
            newTotal.Should().Be(0);
        }

        [Fact]
        public async Task AwardPointsAsync_WithNonExistentGroupUser_ShouldReturnFailure()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest6");
            var service = CreateGroupPointsService(repoFactory);

            // Act
            var (success, newTotal) = await service.AwardPointsAsync("nonexistent", 999, 20);

            // Assert
            success.Should().BeFalse();
            newTotal.Should().Be(0);
        }

        [Fact]
        public async Task AwardPointsAsync_WithZeroPoints_ShouldReturnSuccessWithNoChange()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest7");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            groupUser.GroupPoints = 50;

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardPointsAsync("user1", group.Id, 0);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(0); // Returns 0 for zero-point operations
        }

        #endregion

        #region Specialized Award Methods Tests

        [Fact]
        public async Task AwardPostCreationPointsAsync_ShouldAward20Points()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest8");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1", enablePointsSystem: true);
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardPostCreationPointsAsync("user1", group.Id);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(GroupPointsService.PointsForPostCreation);
        }

        [Fact]
        public async Task AwardCommentPointsAsync_OnOtherUserPost_ShouldAward5Points()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest9");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1", enablePointsSystem: true);
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardCommentPointsAsync("user1", "user2", group.Id);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(GroupPointsService.PointsForCommentOnOtherPost);
        }

        [Fact]
        public async Task AwardCommentPointsAsync_OnOwnPost_ShouldNotAwardPoints()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest10");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardCommentPointsAsync("user1", "user1", group.Id);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(0); // No points for commenting on own post
        }

        [Fact]
        public async Task AwardLikePointsAsync_OnOtherUserComment_ShouldAward1Point()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest11");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1", enablePointsSystem: true);
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardLikePointsAsync("user1", "user2", group.Id);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(GroupPointsService.PointsForLikeOnOtherComment);
        }

        [Fact]
        public async Task AwardLikePointsAsync_OnOwnComment_ShouldNotAwardPoints()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest12");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardLikePointsAsync("user1", "user1", group.Id);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(0); // No points for liking own comment
        }

        [Fact]
        public async Task AwardPost50CommentsMilestoneAsync_ShouldAward50Points()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest13");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1", enablePointsSystem: true);
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardPost50CommentsMilestoneAsync("user1", group.Id);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(GroupPointsService.PointsForPost50Comments);
        }

        [Fact]
        public async Task AwardPost20LikesMilestoneAsync_ShouldAward30Points()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest14");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1", enablePointsSystem: true);
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, newTotal) = await service.AwardPost20LikesMilestoneAsync("user1", group.Id);

            // Assert
            success.Should().BeTrue();
            newTotal.Should().Be(GroupPointsService.PointsForPost20Likes);
        }

        #endregion

        #region Get User Points Tests

        [Fact]
        public async Task GetUserPointsAsync_WithExistingUser_ShouldReturnPoints()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest15");
            var service = CreateGroupPointsService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            groupUser.GroupPoints = 150;

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var points = await service.GetUserPointsAsync("user1", group.Id);

            // Assert
            points.Should().Be(150);
        }

        [Fact]
        public async Task GetUserPointsAsync_WithNonExistentUser_ShouldReturnZero()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest16");
            var service = CreateGroupPointsService(repoFactory);

            // Act
            var points = await service.GetUserPointsAsync("nonexistent", 999);

            // Assert
            points.Should().Be(0);
        }

        [Fact]
        public async Task GetUserPointsAsync_WithNullUserId_ShouldReturnZero()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupPointsTest17");
            var service = CreateGroupPointsService(repoFactory);

            // Act
            var points = await service.GetUserPointsAsync(null!, 1);

            // Assert
            points.Should().Be(0);
        }

        #endregion

        #region Member Level Tests

        [Theory]
        [InlineData(0, MemberLevel.Member)]
        [InlineData(699, MemberLevel.Member)]
        [InlineData(700, MemberLevel.RisingContributor)]
        [InlineData(1499, MemberLevel.RisingContributor)]
        [InlineData(1500, MemberLevel.TopContributor)]
        [InlineData(4999, MemberLevel.TopContributor)]
        [InlineData(5000, MemberLevel.AllStarContributor)]
        [InlineData(10000, MemberLevel.AllStarContributor)]
        public void GetMemberLevel_WithVariousPoints_ShouldReturnCorrectLevel(int points, MemberLevel expectedLevel)
        {
            // Act
            var level = GroupPointsService.GetMemberLevel(points);

            // Assert
            level.Should().Be(expectedLevel);
        }

        [Theory]
        [InlineData(MemberLevel.Member, "Member")]
        [InlineData(MemberLevel.RisingContributor, "Rising Contributor")]
        [InlineData(MemberLevel.TopContributor, "Top Contributor")]
        [InlineData(MemberLevel.AllStarContributor, "All-star Contributor")]
        public void GetMemberLevelName_WithVariousLevels_ShouldReturnCorrectName(MemberLevel level, string expectedName)
        {
            // Act
            var name = GroupPointsService.GetMemberLevelName(level);

            // Assert
            name.Should().Be(expectedName);
        }

        [Theory]
        [InlineData(MemberLevel.Member, "bi-person-fill")]
        [InlineData(MemberLevel.RisingContributor, "bi-arrow-up-circle-fill")]
        [InlineData(MemberLevel.TopContributor, "bi-award-fill")]
        [InlineData(MemberLevel.AllStarContributor, "bi-star-fill")]
        public void GetMemberLevelIcon_WithVariousLevels_ShouldReturnCorrectIcon(MemberLevel level, string expectedIcon)
        {
            // Act
            var icon = GroupPointsService.GetMemberLevelIcon(level);

            // Assert
            icon.Should().Be(expectedIcon);
        }

        [Theory]
        [InlineData(MemberLevel.Member, "text-info")]
        [InlineData(MemberLevel.RisingContributor, "text-success")]
        [InlineData(MemberLevel.TopContributor, "text-primary")]
        [InlineData(MemberLevel.AllStarContributor, "text-warning")]
        public void GetMemberLevelColor_WithVariousLevels_ShouldReturnCorrectColor(MemberLevel level, string expectedColor)
        {
            // Act
            var color = GroupPointsService.GetMemberLevelColor(level);

            // Assert
            color.Should().Be(expectedColor);
        }

        #endregion
    }
}
