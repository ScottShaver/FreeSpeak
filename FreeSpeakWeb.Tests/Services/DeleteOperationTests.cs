using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// P2 Delete Operation Tests - Verifies file cleanup and notification cleanup
    /// for both Post and GroupPost delete operations.
    /// </summary>
    public class DeleteOperationTests : TestBase
    {
        #region Test Infrastructure

        private static IOptions<SiteSettings> CreateTestSiteSettings()
        {
            return Options.Create(new SiteSettings
            {
                SiteName = "TestSite",
                MaxFeedPostCommentDepth = 4,
                MaxFeedPostDirectCommentCount = 1000
            });
        }

        private static IWebHostEnvironment CreateMockWebHostEnvironment()
        {
            var mock = new Mock<IWebHostEnvironment>();
            mock.Setup(m => m.ContentRootPath).Returns(Path.GetTempPath());
            return mock.Object;
        }

        private static NotificationService CreateMockNotificationService()
        {
            var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            var logger = new Mock<ILogger<NotificationService>>();
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var notificationRepo = MockRepositories.CreateMockNotificationRepository();            return new NotificationService(notificationRepo.Object, dbFactory.Object, logger.Object, scopeFactory.Object);
        }

        private static UserPreferenceService CreateMockUserPreferenceService()
        {
            var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            var logger = new Mock<ILogger<UserPreferenceService>>();
            return new UserPreferenceService(dbFactory.Object, logger.Object);
        }

        private static PostNotificationHelper CreateMockPostNotificationHelper()
        {
            var dbFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            var logger = new Mock<ILogger<PostNotificationHelper>>();
            return new PostNotificationHelper(dbFactory.Object, CreateMockNotificationService(), CreateMockUserPreferenceService(), logger.Object);
        }

        private static GroupAccessValidator CreateMockGroupAccessValidator(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            var logger = new Mock<ILogger<GroupAccessValidator>>();
            return new GroupAccessValidator(dbFactory, logger.Object);
        }

        private PostService CreatePostService(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            return new PostService(
                dbFactory,
                MockRepositories.CreateMockFeedPostRepository().Object,
                MockRepositories.CreateMockFeedCommentRepository().Object,
                MockRepositories.CreateMockFeedPostLikeRepository().Object,
                MockRepositories.CreateMockFeedCommentLikeRepository().Object,
                MockRepositories.CreateMockPinnedPostRepository().Object,
                MockRepositories.CreateMockPostNotificationMuteRepository().Object,
                MockRepositories.CreateMockNotificationRepository().Object,
                CreateMockLogger<PostService>(),
                CreateTestSiteSettings(),
                CreateMockWebHostEnvironment(),
                CreateMockNotificationService(),
                CreateMockUserPreferenceService(),
                CreateMockPostNotificationHelper());
        }

        private GroupPostService CreateGroupPostService(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            return new GroupPostService(
                dbFactory,
                MockRepositories.CreateMockGroupPostRepository().Object,
                MockRepositories.CreateMockGroupCommentRepository().Object,
                MockRepositories.CreateMockGroupPostLikeRepository().Object,
                MockRepositories.CreateMockGroupCommentLikeRepository().Object,
                MockRepositories.CreateMockGroupRepository().Object,
                MockRepositories.CreateMockNotificationRepository().Object,
                CreateMockLogger<GroupPostService>(),
                CreateMockNotificationService(),
                CreateMockUserPreferenceService(),
                CreateMockWebHostEnvironment(),
                CreateMockPostNotificationHelper(),
                CreateMockGroupAccessValidator(dbFactory));
        }

        private static UserNotification CreateTestNotification(
            string userId,
            NotificationType type,
            string message,
            string? data = null)
        {
            return new UserNotification
            {
                UserId = userId,
                Type = type,
                Message = message,
                Data = data,
                CreatedAt = DateTime.UtcNow
            };
        }

        private static PostNotificationMute CreateTestPostNotificationMute(int postId, string userId)
        {
            return new PostNotificationMute
            {
                PostId = postId,
                UserId = userId,
                MutedAt = DateTime.UtcNow
            };
        }

        private static PinnedPost CreateTestPinnedPost(int postId, string userId)
        {
            return new PinnedPost
            {
                PostId = postId,
                UserId = userId,
                PinnedAt = DateTime.UtcNow
            };
        }

        private static CommentLike CreateTestCommentLike(int commentId, string userId, LikeType type = LikeType.Like)
        {
            return new CommentLike
            {
                CommentId = commentId,
                UserId = userId,
                Type = type,
                CreatedAt = DateTime.UtcNow
            };
        }

        #endregion

        #region Post Notification Cleanup Tests

        [Fact]
        public async Task DeletePostAsync_ShouldDeleteRelatedNotifications()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeletePost_Notifications");
            var service = CreatePostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                // Create notifications related to this post
                var notification1 = CreateTestNotification(
                    "user2",
                    NotificationType.PostComment,
                    "User1 commented on your post",
                    $"{{\"PostId\":{post.Id},\"CommentId\":1}}");
                var notification2 = CreateTestNotification(
                    "user2",
                    NotificationType.PostLiked,
                    "User1 liked your post",
                    $"{{\"PostId\":{post.Id}}}");
                // Notification for a different post (should NOT be deleted)
                var notification3 = CreateTestNotification(
                    "user2",
                    NotificationType.PostComment,
                    "Someone commented on another post",
                    "{\"PostId\":999}");

                context.UserNotifications.AddRange(notification1, notification2, notification3);
                await context.SaveChangesAsync();
            }

            // Verify notifications exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var notificationCount = await context.UserNotifications.CountAsync();
                notificationCount.Should().Be(3);
            }

            // Act
            var result = await service.DeletePostAsync(post.Id, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                // Only the unrelated notification should remain
                var remainingNotifications = await context.UserNotifications.ToListAsync();
                remainingNotifications.Should().HaveCount(1);
                remainingNotifications[0].Data.Should().Contain("\"PostId\":999");
            }
        }

        [Fact]
        public async Task DeletePostAsync_ShouldDeleteNotificationMutes()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeletePost_Mutes");
            var service = CreatePostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                // Create notification mutes for this post
                var mute1 = CreateTestPostNotificationMute(post.Id, "user1");
                var mute2 = CreateTestPostNotificationMute(post.Id, "user2");

                context.PostNotificationMutes.AddRange(mute1, mute2);
                await context.SaveChangesAsync();
            }

            // Verify mutes exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var muteCount = await context.PostNotificationMutes.CountAsync();
                muteCount.Should().Be(2);
            }

            // Act
            var result = await service.DeletePostAsync(post.Id, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingMutes = await context.PostNotificationMutes.ToListAsync();
                remainingMutes.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task DeletePostAsync_ShouldDeletePinnedPostRecords()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeletePost_Pinned");
            var service = CreatePostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                // Create pinned post records (multiple users can pin the same post)
                var pinned1 = CreateTestPinnedPost(post.Id, "user1");
                var pinned2 = CreateTestPinnedPost(post.Id, "user2");

                context.PinnedPosts.AddRange(pinned1, pinned2);
                await context.SaveChangesAsync();
            }

            // Verify pinned records exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var pinnedCount = await context.PinnedPosts.CountAsync();
                pinnedCount.Should().Be(2);
            }

            // Act
            var result = await service.DeletePostAsync(post.Id, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingPinned = await context.PinnedPosts.ToListAsync();
                remainingPinned.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task DeletePostAsync_ShouldDeleteCommentLikes()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeletePost_CommentLikes");
            var service = CreatePostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 2);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                // Add comments
                var comment1 = TestDataFactory.CreateTestComment(post.Id, "user1", "Comment 1");
                var comment2 = TestDataFactory.CreateTestComment(post.Id, "user2", "Comment 2");
                context.Comments.AddRange(comment1, comment2);
                await context.SaveChangesAsync();

                // Add likes to the comments
                var like1 = CreateTestCommentLike(comment1.Id, "user2");
                var like2 = CreateTestCommentLike(comment2.Id, "user1");
                context.CommentLikes.AddRange(like1, like2);
                await context.SaveChangesAsync();
            }

            // Verify comment likes exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var likeCount = await context.CommentLikes.CountAsync();
                likeCount.Should().Be(2);
            }

            // Act
            var result = await service.DeletePostAsync(post.Id, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingLikes = await context.CommentLikes.ToListAsync();
                remainingLikes.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task DeletePostAsync_ShouldDeletePostLikes()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeletePost_PostLikes");
            var service = CreatePostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var user3 = TestDataFactory.CreateTestUser(id: "user3");
            var post = TestDataFactory.CreateTestPost("user1", likeCount: 2);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2, user3);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                // Add likes to the post
                var like1 = new Like { PostId = post.Id, UserId = "user2", Type = LikeType.Like, CreatedAt = DateTime.UtcNow };
                var like2 = new Like { PostId = post.Id, UserId = "user3", Type = LikeType.Love, CreatedAt = DateTime.UtcNow };
                context.Likes.AddRange(like1, like2);
                await context.SaveChangesAsync();
            }

            // Verify likes exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var likeCount = await context.Likes.CountAsync();
                likeCount.Should().Be(2);
            }

            // Act
            var result = await service.DeletePostAsync(post.Id, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingLikes = await context.Likes.ToListAsync();
                remainingLikes.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task DeletePostAsync_ShouldDeletePostImages()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeletePost_Images");
            var service = CreatePostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                // Add images to the post
                var image1 = new PostImage { PostId = post.Id, ImageUrl = "/api/secure-files/post-image/user1/1/image1.jpg", DisplayOrder = 0, UploadedAt = DateTime.UtcNow };
                var image2 = new PostImage { PostId = post.Id, ImageUrl = "/api/secure-files/post-image/user1/2/image2.jpg", DisplayOrder = 1, UploadedAt = DateTime.UtcNow };
                context.PostImages.AddRange(image1, image2);
                await context.SaveChangesAsync();
            }

            // Verify images exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var imageCount = await context.PostImages.CountAsync();
                imageCount.Should().Be(2);
            }

            // Act
            var result = await service.DeletePostAsync(post.Id, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingImages = await context.PostImages.ToListAsync();
                remainingImages.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task DeletePostAsync_ShouldDeleteCommentsAndReplies()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeletePost_CommentsReplies");
            var service = CreatePostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 3);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                // Add parent comment and replies
                var parentComment = TestDataFactory.CreateTestComment(post.Id, "user2", "Parent comment");
                context.Comments.Add(parentComment);
                await context.SaveChangesAsync();

                var reply1 = TestDataFactory.CreateTestComment(post.Id, "user1", "Reply 1", parentComment.Id);
                var reply2 = TestDataFactory.CreateTestComment(post.Id, "user2", "Reply 2", parentComment.Id);
                context.Comments.AddRange(reply1, reply2);
                await context.SaveChangesAsync();
            }

            // Verify comments exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var commentCount = await context.Comments.CountAsync();
                commentCount.Should().Be(3);
            }

            // Act
            var result = await service.DeletePostAsync(post.Id, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingComments = await context.Comments.ToListAsync();
                remainingComments.Should().BeEmpty();
            }
        }

        #endregion

        #region GroupPost Notification Cleanup Tests

        [Fact]
        public async Task DeleteGroupPostAsync_ShouldDeleteRelatedNotifications()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_Notifications");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var groupUser = TestDataFactory.CreateTestGroupUser(group.Id, "user1");
                context.GroupUsers.Add(groupUser);

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Create notifications related to this group post
                var notification1 = CreateTestNotification(
                    "user2",
                    NotificationType.GroupPostComment,
                    "User1 commented on your group post",
                    $"{{\"GroupPostId\":{post.Id},\"CommentId\":1,\"GroupId\":{group.Id}}}");
                var notification2 = CreateTestNotification(
                    "user2",
                    NotificationType.GroupPostLiked,
                    "User1 liked your group post",
                    $"{{\"GroupPostId\":{post.Id},\"GroupId\":{group.Id}}}");
                // Notification for a different post (should NOT be deleted)
                var notification3 = CreateTestNotification(
                    "user2",
                    NotificationType.GroupPostComment,
                    "Someone commented on another post",
                    "{\"GroupPostId\":999}");

                context.UserNotifications.AddRange(notification1, notification2, notification3);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Verify notifications exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var notificationCount = await context.UserNotifications.CountAsync();
                notificationCount.Should().Be(3);
            }

            // Act
            var result = await service.DeleteGroupPostAsync(postId, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                // Only the unrelated notification should remain
                var remainingNotifications = await context.UserNotifications.ToListAsync();
                remainingNotifications.Should().HaveCount(1);
                remainingNotifications[0].Data.Should().Contain("\"GroupPostId\":999");
            }
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ShouldDeleteNotificationMutes()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_Mutes");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var groupUser = TestDataFactory.CreateTestGroupUser(group.Id, "user1");
                context.GroupUsers.Add(groupUser);

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Create notification mutes for this post
                var mute1 = TestDataFactory.CreateTestGroupPostNotificationMute(post.Id, "user1");
                var mute2 = TestDataFactory.CreateTestGroupPostNotificationMute(post.Id, "user2");

                context.GroupPostNotificationMutes.AddRange(mute1, mute2);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Verify mutes exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var muteCount = await context.GroupPostNotificationMutes.CountAsync();
                muteCount.Should().Be(2);
            }

            // Act
            var result = await service.DeleteGroupPostAsync(postId, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingMutes = await context.GroupPostNotificationMutes.ToListAsync();
                remainingMutes.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ShouldDeletePinnedPostRecords()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_Pinned");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var groupUser = TestDataFactory.CreateTestGroupUser(group.Id, "user1");
                context.GroupUsers.Add(groupUser);

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Create pinned post records
                var pinned1 = TestDataFactory.CreateTestPinnedGroupPost("user1", post.Id);
                var pinned2 = TestDataFactory.CreateTestPinnedGroupPost("user2", post.Id);

                context.PinnedGroupPosts.AddRange(pinned1, pinned2);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Verify pinned records exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var pinnedCount = await context.PinnedGroupPosts.CountAsync();
                pinnedCount.Should().Be(2);
            }

            // Act
            var result = await service.DeleteGroupPostAsync(postId, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingPinned = await context.PinnedGroupPosts.ToListAsync();
                remainingPinned.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ShouldDeleteCommentLikes()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_CommentLikes");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var groupUser1 = TestDataFactory.CreateTestGroupUser(group.Id, "user1");
                var groupUser2 = TestDataFactory.CreateTestGroupUser(group.Id, "user2");
                context.GroupUsers.AddRange(groupUser1, groupUser2);

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "user1", commentCount: 2);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Add comments
                var comment1 = TestDataFactory.CreateTestGroupPostComment(post.Id, "user1", "Comment 1");
                var comment2 = TestDataFactory.CreateTestGroupPostComment(post.Id, "user2", "Comment 2");
                context.GroupPostComments.AddRange(comment1, comment2);
                await context.SaveChangesAsync();

                // Add likes to the comments
                var like1 = TestDataFactory.CreateTestGroupPostCommentLike(comment1.Id, "user2");
                var like2 = TestDataFactory.CreateTestGroupPostCommentLike(comment2.Id, "user1");
                context.GroupPostCommentLikes.AddRange(like1, like2);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Verify comment likes exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var likeCount = await context.GroupPostCommentLikes.CountAsync();
                likeCount.Should().Be(2);
            }

            // Act
            var result = await service.DeleteGroupPostAsync(postId, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingLikes = await context.GroupPostCommentLikes.ToListAsync();
                remainingLikes.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ShouldDeletePostLikes()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_PostLikes");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var groupUser = TestDataFactory.CreateTestGroupUser(group.Id, "user1");
                context.GroupUsers.Add(groupUser);

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "user1", likeCount: 2);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Add likes to the post
                var like1 = TestDataFactory.CreateTestGroupPostLike(post.Id, "user1");
                var like2 = TestDataFactory.CreateTestGroupPostLike(post.Id, "user2", LikeType.Love);
                context.GroupPostLikes.AddRange(like1, like2);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Verify likes exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var likeCount = await context.GroupPostLikes.CountAsync();
                likeCount.Should().Be(2);
            }

            // Act
            var result = await service.DeleteGroupPostAsync(postId, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingLikes = await context.GroupPostLikes.ToListAsync();
                remainingLikes.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ShouldDeletePostImages()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_Images");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var groupUser = TestDataFactory.CreateTestGroupUser(group.Id, "user1");
                context.GroupUsers.Add(groupUser);

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "user1");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Add images to the post
                var image1 = new GroupPostImage { PostId = post.Id, ImageUrl = $"/api/secure-files/group-post-image/{group.Id}/{post.Id}/1/image1.jpg", DisplayOrder = 0, UploadedAt = DateTime.UtcNow };
                var image2 = new GroupPostImage { PostId = post.Id, ImageUrl = $"/api/secure-files/group-post-image/{group.Id}/{post.Id}/2/image2.jpg", DisplayOrder = 1, UploadedAt = DateTime.UtcNow };
                context.GroupPostImages.AddRange(image1, image2);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Verify images exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var imageCount = await context.GroupPostImages.CountAsync();
                imageCount.Should().Be(2);
            }

            // Act
            var result = await service.DeleteGroupPostAsync(postId, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingImages = await context.GroupPostImages.ToListAsync();
                remainingImages.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ShouldDeleteCommentsAndReplies()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_CommentsReplies");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                var groupUser1 = TestDataFactory.CreateTestGroupUser(group.Id, "user1");
                var groupUser2 = TestDataFactory.CreateTestGroupUser(group.Id, "user2");
                context.GroupUsers.AddRange(groupUser1, groupUser2);

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "user1", commentCount: 3);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Add parent comment and replies
                var parentComment = TestDataFactory.CreateTestGroupPostComment(post.Id, "user2", "Parent comment");
                context.GroupPostComments.Add(parentComment);
                await context.SaveChangesAsync();

                var reply1 = TestDataFactory.CreateTestGroupPostComment(post.Id, "user1", "Reply 1", parentComment.Id);
                var reply2 = TestDataFactory.CreateTestGroupPostComment(post.Id, "user2", "Reply 2", parentComment.Id);
                context.GroupPostComments.AddRange(reply1, reply2);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Verify comments exist before delete
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var commentCount = await context.GroupPostComments.CountAsync();
                commentCount.Should().Be(3);
            }

            // Act
            var result = await service.DeleteGroupPostAsync(postId, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var remainingComments = await context.GroupPostComments.ToListAsync();
                remainingComments.Should().BeEmpty();
            }
        }

        #endregion

        #region Comprehensive Cleanup Tests

        [Fact]
        public async Task DeletePostAsync_WithAllRelatedData_ShouldCleanupEverything()
        {
            // Arrange - Create a post with ALL possible related data
            var dbFactory = CreateDbContextFactory("DeletePost_Comprehensive");
            var service = CreatePostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1", likeCount: 1, commentCount: 2);

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                // Add pinned post
                context.PinnedPosts.Add(CreateTestPinnedPost(post.Id, "user1"));
                context.PinnedPosts.Add(CreateTestPinnedPost(post.Id, "user2"));

                // Add notification mute
                context.PostNotificationMutes.Add(CreateTestPostNotificationMute(post.Id, "user1"));

                // Add post like
                context.Likes.Add(new Like { PostId = post.Id, UserId = "user2", Type = LikeType.Like, CreatedAt = DateTime.UtcNow });

                // Add post image
                context.PostImages.Add(new PostImage { PostId = post.Id, ImageUrl = "/api/secure-files/post-image/user1/1/test.jpg", DisplayOrder = 0, UploadedAt = DateTime.UtcNow });

                // Add comments with likes
                var comment = TestDataFactory.CreateTestComment(post.Id, "user2", "Test comment");
                context.Comments.Add(comment);
                await context.SaveChangesAsync();

                context.CommentLikes.Add(CreateTestCommentLike(comment.Id, "user1"));

                // Add notification
                context.UserNotifications.Add(CreateTestNotification(
                    "user2",
                    NotificationType.PostComment,
                    "Someone commented",
                    $"{{\"PostId\":{post.Id}}}"));

                await context.SaveChangesAsync();
            }

            // Verify all data exists
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                (await context.PinnedPosts.CountAsync()).Should().Be(2);
                (await context.PostNotificationMutes.CountAsync()).Should().Be(1);
                (await context.Likes.CountAsync()).Should().Be(1);
                (await context.PostImages.CountAsync()).Should().Be(1);
                (await context.Comments.CountAsync()).Should().Be(1);
                (await context.CommentLikes.CountAsync()).Should().Be(1);
                (await context.UserNotifications.CountAsync()).Should().Be(1);
            }

            // Act
            var result = await service.DeletePostAsync(post.Id, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                (await context.Posts.CountAsync()).Should().Be(0);
                (await context.PinnedPosts.CountAsync()).Should().Be(0);
                (await context.PostNotificationMutes.CountAsync()).Should().Be(0);
                (await context.Likes.CountAsync()).Should().Be(0);
                (await context.PostImages.CountAsync()).Should().Be(0);
                (await context.Comments.CountAsync()).Should().Be(0);
                (await context.CommentLikes.CountAsync()).Should().Be(0);
                (await context.UserNotifications.CountAsync()).Should().Be(0);
            }
        }

        [Fact]
        public async Task DeleteGroupPostAsync_WithAllRelatedData_ShouldCleanupEverything()
        {
            // Arrange - Create a group post with ALL possible related data
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_Comprehensive");
            var service = CreateGroupPostService(dbFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "user1"));
                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "user2"));

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "user1", likeCount: 1, commentCount: 2);
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();

                // Add pinned post
                context.PinnedGroupPosts.Add(TestDataFactory.CreateTestPinnedGroupPost("user1", post.Id));
                context.PinnedGroupPosts.Add(TestDataFactory.CreateTestPinnedGroupPost("user2", post.Id));

                // Add notification mute
                context.GroupPostNotificationMutes.Add(TestDataFactory.CreateTestGroupPostNotificationMute(post.Id, "user1"));

                // Add post like
                context.GroupPostLikes.Add(TestDataFactory.CreateTestGroupPostLike(post.Id, "user2"));

                // Add post image
                context.GroupPostImages.Add(new GroupPostImage { PostId = post.Id, ImageUrl = $"/api/secure-files/group-post-image/{group.Id}/{post.Id}/1/test.jpg", DisplayOrder = 0, UploadedAt = DateTime.UtcNow });

                // Add comments with likes
                var comment = TestDataFactory.CreateTestGroupPostComment(post.Id, "user2", "Test comment");
                context.GroupPostComments.Add(comment);
                await context.SaveChangesAsync();

                context.GroupPostCommentLikes.Add(TestDataFactory.CreateTestGroupPostCommentLike(comment.Id, "user1"));

                // Add notification
                context.UserNotifications.Add(CreateTestNotification(
                    "user2",
                    NotificationType.GroupPostComment,
                    "Someone commented",
                    $"{{\"GroupPostId\":{post.Id}}}"));

                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Verify all data exists
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                (await context.PinnedGroupPosts.CountAsync()).Should().Be(2);
                (await context.GroupPostNotificationMutes.CountAsync()).Should().Be(1);
                (await context.GroupPostLikes.CountAsync()).Should().Be(1);
                (await context.GroupPostImages.CountAsync()).Should().Be(1);
                (await context.GroupPostComments.CountAsync()).Should().Be(1);
                (await context.GroupPostCommentLikes.CountAsync()).Should().Be(1);
                (await context.UserNotifications.CountAsync()).Should().Be(1);
            }

            // Act
            var result = await service.DeleteGroupPostAsync(postId, "user1");

            // Assert
            result.Success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                (await context.GroupPosts.CountAsync()).Should().Be(0);
                (await context.PinnedGroupPosts.CountAsync()).Should().Be(0);
                (await context.GroupPostNotificationMutes.CountAsync()).Should().Be(0);
                (await context.GroupPostLikes.CountAsync()).Should().Be(0);
                (await context.GroupPostImages.CountAsync()).Should().Be(0);
                (await context.GroupPostComments.CountAsync()).Should().Be(0);
                (await context.GroupPostCommentLikes.CountAsync()).Should().Be(0);
                (await context.UserNotifications.CountAsync()).Should().Be(0);
            }
        }

        #endregion

        #region Delete Authorization Tests

        [Fact]
        public async Task DeleteGroupPostAsync_ByModerator_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_Moderator");
            var service = CreateGroupPostService(dbFactory);

            var author = TestDataFactory.CreateTestUser(id: "author");
            var moderator = TestDataFactory.CreateTestUser(id: "moderator");
            var group = TestDataFactory.CreateTestGroup("admin");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(author, moderator);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                // Author is a regular member
                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "author"));
                // Moderator has moderator privileges
                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "moderator", isModerator: true));

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "author");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Act - Moderator deletes author's post
            var result = await service.DeleteGroupPostAsync(postId, "moderator");

            // Assert
            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ByAdmin_ShouldSucceed()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_Admin");
            var service = CreateGroupPostService(dbFactory);

            var author = TestDataFactory.CreateTestUser(id: "author");
            var admin = TestDataFactory.CreateTestUser(id: "admin");
            var group = TestDataFactory.CreateTestGroup("creator");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(author, admin);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                // Author is a regular member
                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "author"));
                // Admin has admin privileges
                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "admin", isAdmin: true));

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "author");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Act - Admin deletes author's post
            var result = await service.DeleteGroupPostAsync(postId, "admin");

            // Assert
            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteGroupPostAsync_ByRegularMember_ShouldFail()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("DeleteGroupPost_RegularMember");
            var service = CreateGroupPostService(dbFactory);

            var author = TestDataFactory.CreateTestUser(id: "author");
            var member = TestDataFactory.CreateTestUser(id: "member");
            var group = TestDataFactory.CreateTestGroup("admin");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(author, member);
                context.Groups.Add(group);
                await context.SaveChangesAsync();

                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "author"));
                context.GroupUsers.Add(TestDataFactory.CreateTestGroupUser(group.Id, "member"));

                var post = TestDataFactory.CreateTestGroupPost(group.Id, "author");
                context.GroupPosts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = (await context.GroupPosts.FirstAsync()).Id;
            }

            // Act - Regular member tries to delete author's post
            var result = await service.DeleteGroupPostAsync(postId, "member");

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("not authorized");
        }

        #endregion
    }
}

