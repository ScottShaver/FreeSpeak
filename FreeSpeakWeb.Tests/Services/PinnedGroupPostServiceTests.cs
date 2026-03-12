using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    public class PinnedGroupPostServiceTests : TestBase
    {
        [Fact]
        public async Task PinGroupPostAsync_ValidPost_ShouldPinPost()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PinnedGroupPostTest1");
            var logger = CreateMockLogger<PinnedGroupPostService>();
            var service = new PinnedGroupPostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                groupUser.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.PinGroupPostAsync("user1", post.Id);

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var pinned = await context.PinnedGroupPosts.FirstOrDefaultAsync(p => p.UserId == "user1" && p.PostId == post.Id);
                pinned.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task PinGroupPostAsync_NonMember_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PinnedGroupPostTest2");
            var logger = CreateMockLogger<PinnedGroupPostService>();
            var service = new PinnedGroupPostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user2");
            var post = TestDataFactory.CreateTestGroupPost(1, "user2");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.PinGroupPostAsync("user1", post.Id);

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("must be a member");
        }

        [Fact]
        public async Task PinGroupPostAsync_AlreadyPinned_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PinnedGroupPostTest3");
            var logger = CreateMockLogger<PinnedGroupPostService>();
            var service = new PinnedGroupPostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var pinnedPost = TestDataFactory.CreateTestPinnedGroupPost("user1", 1);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                groupUser.GroupId = group.Id;
                context.GroupPosts.Add(post);
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
                pinnedPost.PostId = post.Id;
                context.PinnedGroupPosts.Add(pinnedPost);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.PinGroupPostAsync("user1", post.Id);

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("already pinned");
        }

        [Fact]
        public async Task UnpinGroupPostAsync_PinnedPost_ShouldUnpin()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PinnedGroupPostTest4");
            var logger = CreateMockLogger<PinnedGroupPostService>();
            var service = new PinnedGroupPostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var pinnedPost = TestDataFactory.CreateTestPinnedGroupPost("user1", 1);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                pinnedPost.PostId = post.Id;
                context.PinnedGroupPosts.Add(pinnedPost);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.UnpinGroupPostAsync("user1", post.Id);

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var unpinned = await context.PinnedGroupPosts.FirstOrDefaultAsync(p => p.UserId == "user1" && p.PostId == post.Id);
                unpinned.Should().BeNull();
            }
        }

        [Fact]
        public async Task UnpinGroupPostAsync_NotPinned_ShouldReturnError()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PinnedGroupPostTest5");
            var logger = CreateMockLogger<PinnedGroupPostService>();
            var service = new PinnedGroupPostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage) = await service.UnpinGroupPostAsync("user1", post.Id);

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not pinned");
        }

        [Fact]
        public async Task IsGroupPostPinnedAsync_PinnedPost_ShouldReturnTrue()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PinnedGroupPostTest6");
            var logger = CreateMockLogger<PinnedGroupPostService>();
            var service = new PinnedGroupPostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");
            var pinnedPost = TestDataFactory.CreateTestPinnedGroupPost("user1", 1);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
                pinnedPost.PostId = post.Id;
                context.PinnedGroupPosts.Add(pinnedPost);
                await context.SaveChangesAsync();
            }

            // Act
            var isPinned = await service.IsGroupPostPinnedAsync("user1", post.Id);

            // Assert
            isPinned.Should().BeTrue();
        }

        [Fact]
        public async Task IsGroupPostPinnedAsync_NotPinned_ShouldReturnFalse()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PinnedGroupPostTest7");
            var logger = CreateMockLogger<PinnedGroupPostService>();
            var service = new PinnedGroupPostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            var post = TestDataFactory.CreateTestGroupPost(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                post.GroupId = group.Id;
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            // Act
            var isPinned = await service.IsGroupPostPinnedAsync("user1", post.Id);

            // Assert
            isPinned.Should().BeFalse();
        }

        [Fact]
        public async Task GetPinnedGroupPostsAsync_ShouldReturnPinnedPosts()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PinnedGroupPostTest8");
            var logger = CreateMockLogger<PinnedGroupPostService>();
            var service = new PinnedGroupPostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var post1 = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Post 1");
                var post2 = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Post 2");
                context.GroupPosts.AddRange(post1, post2);
                await context.SaveChangesAsync();

                var pinned1 = TestDataFactory.CreateTestPinnedGroupPost("user1", post1.Id);
                var pinned2 = TestDataFactory.CreateTestPinnedGroupPost("user1", post2.Id);
                context.PinnedGroupPosts.AddRange(pinned1, pinned2);
                await context.SaveChangesAsync();
            }

            // Act
            var posts = await service.GetPinnedGroupPostsAsync("user1");

            // Assert
            posts.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetPinnedGroupPostsByGroupAsync_ShouldReturnPostsForSpecificGroup()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PinnedGroupPostTest9");
            var logger = CreateMockLogger<PinnedGroupPostService>();
            var service = new PinnedGroupPostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group1 = TestDataFactory.CreateTestGroup("user1", "Group 1");
            var group2 = TestDataFactory.CreateTestGroup("user1", "Group 2");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.AddRange(group1, group2);
                await context.SaveChangesAsync();

                var post1 = TestDataFactory.CreateTestGroupPost(group1.Id, "user1", "Post 1");
                var post2 = TestDataFactory.CreateTestGroupPost(group2.Id, "user1", "Post 2");
                context.GroupPosts.AddRange(post1, post2);
                await context.SaveChangesAsync();

                var pinned1 = TestDataFactory.CreateTestPinnedGroupPost("user1", post1.Id);
                var pinned2 = TestDataFactory.CreateTestPinnedGroupPost("user1", post2.Id);
                context.PinnedGroupPosts.AddRange(pinned1, pinned2);
                await context.SaveChangesAsync();
            }

            // Act
            var posts = await service.GetPinnedGroupPostsByGroupAsync("user1", group1.Id);

            // Assert
            posts.Should().HaveCount(1);
            posts[0].GroupId.Should().Be(group1.Id);
        }

        [Fact]
        public async Task GetPinnedGroupPostCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PinnedGroupPostTest10");
            var logger = CreateMockLogger<PinnedGroupPostService>();
            var service = new PinnedGroupPostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var post1 = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Post 1");
                var post2 = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Post 2");
                var post3 = TestDataFactory.CreateTestGroupPost(group.Id, "user1", "Post 3");
                context.GroupPosts.AddRange(post1, post2, post3);
                await context.SaveChangesAsync();

                var pinned1 = TestDataFactory.CreateTestPinnedGroupPost("user1", post1.Id);
                var pinned2 = TestDataFactory.CreateTestPinnedGroupPost("user1", post2.Id);
                context.PinnedGroupPosts.AddRange(pinned1, pinned2);
                await context.SaveChangesAsync();
            }

            // Act
            var count = await service.GetPinnedGroupPostCountAsync("user1");

            // Assert
            count.Should().Be(2);
        }
    }
}
