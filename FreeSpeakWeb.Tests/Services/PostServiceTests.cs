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
    public class PostServiceTests : TestBase
    {
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

        private NotificationService CreateNotificationService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            var logger = new Mock<ILogger<NotificationService>>();
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var notificationRepo = new TestRepositoryFactory(contextFactory).CreateNotificationRepository();
            return new NotificationService(notificationRepo, contextFactory, logger.Object, scopeFactory.Object);
        }

        private UserPreferenceService CreateUserPreferenceService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            var logger = new Mock<ILogger<UserPreferenceService>>();
            return new UserPreferenceService(contextFactory, logger.Object);
        }

        private PostNotificationHelper CreatePostNotificationHelper(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            var logger = new Mock<ILogger<PostNotificationHelper>>();
            return new PostNotificationHelper(contextFactory, CreateNotificationService(contextFactory), CreateUserPreferenceService(contextFactory), logger.Object);
        }

        /// <summary>
        /// Creates a PostService with real repositories using an in-memory database.
        /// </summary>
        private PostService CreatePostService(TestRepositoryFactory repoFactory)
        {
            var logger = CreateMockLogger<PostService>();
            return new PostService(
                repoFactory.ContextFactory,
                repoFactory.CreateFeedPostRepository(),
                repoFactory.CreateFeedCommentRepository(),
                repoFactory.CreateFeedPostLikeRepository(),
                repoFactory.CreateFeedCommentLikeRepository(),
                repoFactory.CreatePinnedPostRepository(),
                repoFactory.CreatePostNotificationMuteRepository(),
                repoFactory.CreateNotificationRepository(),
                logger,
                CreateTestSiteSettings(),
                CreateMockWebHostEnvironment(),
                CreateNotificationService(repoFactory.ContextFactory),
                CreateUserPreferenceService(repoFactory.ContextFactory),
                CreatePostNotificationHelper(repoFactory.ContextFactory),
                MockRepositories.CreateMockAuditLogRepository().Object);
        }

        /// <summary>
        /// Creates a PostService with custom site settings for testing limits.
        /// </summary>
        private PostService CreatePostServiceWithCustomSettings(TestRepositoryFactory repoFactory, int maxDirectComments = 1000, int maxCommentDepth = 4)
        {
            var logger = CreateMockLogger<PostService>();
            var siteSettings = Options.Create(new SiteSettings
            {
                SiteName = "TestSite",
                MaxFeedPostCommentDepth = maxCommentDepth,
                MaxFeedPostDirectCommentCount = maxDirectComments
            });
            return new PostService(
                repoFactory.ContextFactory,
                repoFactory.CreateFeedPostRepository(),
                repoFactory.CreateFeedCommentRepository(),
                repoFactory.CreateFeedPostLikeRepository(),
                repoFactory.CreateFeedCommentLikeRepository(),
                repoFactory.CreatePinnedPostRepository(),
                repoFactory.CreatePostNotificationMuteRepository(),
                repoFactory.CreateNotificationRepository(),
                logger,
                siteSettings,
                CreateMockWebHostEnvironment(),
                CreateNotificationService(repoFactory.ContextFactory),
                CreateUserPreferenceService(repoFactory.ContextFactory),
                CreatePostNotificationHelper(repoFactory.ContextFactory),
                MockRepositories.CreateMockAuditLogRepository().Object);
        }

        #region Post Operations Tests

        [Fact]
        public async Task CreatePostAsync_WithValidContent_ShouldCreatePost()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest1");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }

            // Act
            var (success, errorMessage, post) = await service.CreatePostAsync("user1", "Test post content");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            post.Should().NotBeNull();
            post!.Content.Should().Be("Test post content");
            post.AuthorId.Should().Be("user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var savedPost = context.Posts.FirstOrDefault();
                savedPost.Should().NotBeNull();
                savedPost!.Content.Should().Be("Test post content");
            }
        }

        [Fact]
        public async Task CreatePostAsync_WithEmptyContent_ShouldReturnError()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest2");
            var service = CreatePostService(repoFactory);

            // Act
            var (success, errorMessage, post) = await service.CreatePostAsync("user1", "");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("Post must contain either text or images");
            post.Should().BeNull();
        }

        [Fact]
        public async Task CreatePostAsync_WithImages_ShouldCreatePostAndImages()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest3");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }

            var imageUrls = new List<string> { "image1.jpg", "image2.jpg" };

            // Act
            var (success, errorMessage, post) = await service.CreatePostAsync("user1", "Post with images", AudienceType.Public, imageUrls);

            // Assert
            success.Should().BeTrue();
            post.Should().NotBeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var images = context.PostImages.Where(pi => pi.PostId == post!.Id).ToList();
                images.Should().HaveCount(2);
                images[0].DisplayOrder.Should().Be(0);
                images[1].DisplayOrder.Should().Be(1);
            }
        }

        [Fact]
        public async Task UpdatePostAsync_ByAuthor_ShouldUpdateContent()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest4");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", "Original content");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage, updatedImages) = await service.UpdatePostAsync(postId, "user1", "Updated content");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var updatedPost = context.Posts.Find(postId);
                updatedPost!.Content.Should().Be("Updated content");
                updatedPost.UpdatedAt.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task UpdatePostAsync_ByNonAuthor_ShouldReturnError()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest5");
            var service = CreatePostService(repoFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage, _) = await service.UpdatePostAsync(postId, "user2", "Hacked content");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not authorized");
        }

        [Fact]
        public async Task DeletePostAsync_ByAuthor_ShouldRemovePost()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest6");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage) = await service.DeletePostAsync(postId, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var deletedPost = context.Posts.Find(postId);
                deletedPost.Should().BeNull();
            }
        }

        [Fact]
        public async Task GetFeedPostsAsync_ShouldReturnUserAndFriendsPosts()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest7");
            var service = CreatePostService(repoFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var user3 = TestDataFactory.CreateTestUser(id: "user3");

            var friendship = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Accepted);

            var post1 = TestDataFactory.CreateTestPost("user1", "User1 post");
            var post2 = TestDataFactory.CreateTestPost("user2", "User2 post");
            var post3 = TestDataFactory.CreateTestPost("user3", "User3 post");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2, user3);
                context.Friendships.Add(friendship);
                context.Posts.AddRange(post1, post2, post3);
                await context.SaveChangesAsync();
            }

            // Act
            var feedPosts = await service.GetFeedPostsAsync("user1");

            // Assert
            feedPosts.Should().HaveCount(2);
            feedPosts.Should().Contain(p => p.AuthorId == "user1");
            feedPosts.Should().Contain(p => p.AuthorId == "user2");
            feedPosts.Should().NotContain(p => p.AuthorId == "user3");
        }

        [Fact]
        public async Task GetFeedPostsAsync_ShouldRespectAudienceSettings()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest_AudienceFilter");
            var service = CreatePostService(repoFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");

            var friendship = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Accepted);

            // User1's own posts with different audience types
            var user1PublicPost = TestDataFactory.CreateTestPost("user1", "User1 public", audienceType: AudienceType.Public);
            var user1FriendsPost = TestDataFactory.CreateTestPost("user1", "User1 friends", audienceType: AudienceType.FriendsOnly);
            var user1PrivatePost = TestDataFactory.CreateTestPost("user1", "User1 private", audienceType: AudienceType.MeOnly);

            // User2's posts with different audience types (user1 should only see public and friends-only)
            var user2PublicPost = TestDataFactory.CreateTestPost("user2", "User2 public", audienceType: AudienceType.Public);
            var user2FriendsPost = TestDataFactory.CreateTestPost("user2", "User2 friends", audienceType: AudienceType.FriendsOnly);
            var user2PrivatePost = TestDataFactory.CreateTestPost("user2", "User2 private", audienceType: AudienceType.MeOnly);

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Friendships.Add(friendship);
                context.Posts.AddRange(user1PublicPost, user1FriendsPost, user1PrivatePost, 
                                      user2PublicPost, user2FriendsPost, user2PrivatePost);
                await context.SaveChangesAsync();
            }

            // Act
            var feedPosts = await service.GetFeedPostsAsync("user1");

            // Assert
            feedPosts.Should().HaveCount(5); // All 3 of user1's posts + 2 visible posts from user2

            // User1 should see ALL their own posts
            feedPosts.Should().Contain(p => p.Id == user1PublicPost.Id);
            feedPosts.Should().Contain(p => p.Id == user1FriendsPost.Id);
            feedPosts.Should().Contain(p => p.Id == user1PrivatePost.Id);

            // User1 should see user2's public and friends-only posts
            feedPosts.Should().Contain(p => p.Id == user2PublicPost.Id);
            feedPosts.Should().Contain(p => p.Id == user2FriendsPost.Id);

            // User1 should NOT see user2's private posts
            feedPosts.Should().NotContain(p => p.Id == user2PrivatePost.Id);
        }

        #endregion

        #region Comment Operations Tests

        [Fact]
        public async Task AddCommentAsync_WithValidData_ShouldCreateComment()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest8");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage, comment) = await service.AddCommentAsync(postId, "user1", "Great post!");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            comment.Should().NotBeNull();
            comment!.Content.Should().Be("Great post!");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var updatedPost = context.Posts.Find(postId);
                updatedPost!.CommentCount.Should().Be(1);
            }
        }

        [Fact]
        public async Task AddCommentAsync_WithReply_ShouldCreateNestedComment()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest9");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            var parentComment = TestDataFactory.CreateTestComment(1, "user1", "Parent comment");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                parentComment.PostId = context.Posts.First().Id;
                context.Comments.Add(parentComment);
                await context.SaveChangesAsync();
            }

            int postId, parentCommentId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
                parentCommentId = context.Comments.First().Id;
            }

            // Act
            var (success, errorMessage, reply) = await service.AddCommentAsync(
                postId, "user1", "Reply to comment", parentCommentId: parentCommentId);

            // Assert
            success.Should().BeTrue();
            reply.Should().NotBeNull();
            reply!.ParentCommentId.Should().Be(parentCommentId);
        }

        [Fact]
        public async Task DeleteCommentAsync_ByAuthor_ShouldRemoveCommentAndUpdateCount()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest10");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 1);
            var comment = TestDataFactory.CreateTestComment(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                comment.PostId = context.Posts.First().Id;
                context.Comments.Add(comment);
                await context.SaveChangesAsync();
            }

            int commentId, postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                commentId = context.Comments.First().Id;
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage) = await service.DeleteCommentAsync(commentId, "user1");

            // Assert
            success.Should().BeTrue();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var deletedComment = context.Comments.Find(commentId);
                deletedComment.Should().BeNull();

                var updatedPost = context.Posts.Find(postId);
                updatedPost!.CommentCount.Should().Be(0);
            }
        }

        #endregion

        #region Like Operations Tests

        [Fact]
        public async Task ToggleLikeAsync_FirstTime_ShouldAddLike()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest11");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage, isLiked) = await service.ToggleLikeAsync(postId, "user1");

            // Assert
            success.Should().BeTrue();
            isLiked.Should().BeTrue();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var like = context.Likes.FirstOrDefault();
                like.Should().NotBeNull();
                like!.UserId.Should().Be("user1");

                var updatedPost = context.Posts.Find(postId);
                updatedPost!.LikeCount.Should().Be(1);
            }
        }

        [Fact]
        public async Task ToggleLikeAsync_SecondTime_ShouldRemoveLike()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest12");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", likeCount: 1);
            var like = TestDataFactory.CreateTestLike(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                like.PostId = context.Posts.First().Id;
                context.Likes.Add(like);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage, isLiked) = await service.ToggleLikeAsync(postId, "user1");

            // Assert
            success.Should().BeTrue();
            isLiked.Should().BeFalse();

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var likes = context.Likes.ToList();
                likes.Should().BeEmpty();

                var updatedPost = context.Posts.Find(postId);
                updatedPost!.LikeCount.Should().Be(0);
            }
        }

        [Fact]
        public async Task HasUserLikedPostAsync_WhenLiked_ShouldReturnTrue()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest13");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            var like = TestDataFactory.CreateTestLike(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                like.PostId = context.Posts.First().Id;
                context.Likes.Add(like);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var hasLiked = await service.HasUserLikedPostAsync(postId, "user1");

            // Assert
            hasLiked.Should().BeTrue();
        }

        #endregion

        #region Post Image Operations Tests

        [Fact]
        public async Task AddImageToPostAsync_ByAuthor_ShouldAddImage()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest14");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage, postImage) = await service.AddImageToPostAsync(
                postId, "https://example.com/image.jpg", "user1");

            // Assert
            success.Should().BeTrue();
            postImage.Should().NotBeNull();
            postImage!.ImageUrl.Should().Be("https://example.com/image.jpg");
            postImage.DisplayOrder.Should().Be(0);
        }

        [Fact]
        public async Task AddImageToPostAsync_ByNonAuthor_ShouldReturnError()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("PostTest15");
            var service = CreatePostService(repoFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage, postImage) = await service.AddImageToPostAsync(
                postId, "https://example.com/image.jpg", "user2");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not authorized");
        }

        #endregion

        #region Comment Limiting Tests

        [Fact]
        public async Task AddCommentAsync_WhenDirectCommentLimitReached_ShouldReturnError()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("CommentLimitTest1");
            var service = CreatePostServiceWithCustomSettings(repoFactory, maxDirectComments: 3);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost(authorId: "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Add 3 direct comments (reaching the limit)
            for (int i = 0; i < 3; i++)
            {
                await service.AddCommentAsync(postId, "user1", $"Comment {i}");
            }

            // Act - Try to add 4th direct comment
            var (success, errorMessage, comment) = await service.AddCommentAsync(postId, "user1", "Comment 4");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("maximum of 3 direct comments");
            comment.Should().BeNull();
        }

        [Fact]
        public async Task AddCommentAsync_ReplyWhenDirectCommentLimitReached_ShouldSucceed()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("CommentLimitTest2");
            var service = CreatePostServiceWithCustomSettings(repoFactory, maxDirectComments: 2);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost(authorId: "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Add 2 direct comments (reaching the limit)
            var (success1, _, comment1) = await service.AddCommentAsync(postId, "user1", "Comment 1");
            var (success2, _, comment2) = await service.AddCommentAsync(postId, "user1", "Comment 2");

            // Act - Try to add a reply (should succeed even though direct comment limit is reached)
            var (success, errorMessage, replyComment) = await service.AddCommentAsync(
                postId, "user1", "Reply to comment 1", null, comment1!.Id);

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            replyComment.Should().NotBeNull();
            replyComment!.ParentCommentId.Should().Be(comment1.Id);
        }

        [Fact]
        public async Task GetDirectCommentCountAsync_ShouldReturnOnlyDirectComments()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("DirectCommentCountTest1");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost(authorId: "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Add 3 direct comments
            var (_, _, comment1) = await service.AddCommentAsync(postId, "user1", "Direct 1");
            await service.AddCommentAsync(postId, "user1", "Direct 2");
            await service.AddCommentAsync(postId, "user1", "Direct 3");

            // Add 2 replies to the first comment
            await service.AddCommentAsync(postId, "user1", "Reply 1", null, comment1!.Id);
            await service.AddCommentAsync(postId, "user1", "Reply 2", null, comment1.Id);

            // Act
            var directCount = await service.GetDirectCommentCountAsync(postId);

            // Assert
            directCount.Should().Be(3); // Should only count direct comments, not replies
        }

        [Fact]
        public async Task AddCommentAsync_WithParentCommentId_ShouldCreateReply()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("ReplyTest1");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost(authorId: "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Add a parent comment
            var (_, _, parentComment) = await service.AddCommentAsync(postId, "user1", "Parent comment");

            // Act - Add a reply
            var (success, errorMessage, reply) = await service.AddCommentAsync(
                postId, "user1", "Reply to parent", null, parentComment!.Id);

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            reply.Should().NotBeNull();
            reply!.ParentCommentId.Should().Be(parentComment.Id);
            reply.Content.Should().Be("Reply to parent");

            // Verify in database
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                var savedReply = context.Comments.FirstOrDefault(c => c.ParentCommentId == parentComment.Id);
                savedReply.Should().NotBeNull();
                savedReply!.Content.Should().Be("Reply to parent");
            }
        }

        [Fact]
        public async Task GetRepliesAsync_ShouldReturnRepliesForComment()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("RepliesTest1");
            var service = CreatePostService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost(authorId: "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Add parent comment and 2 replies
            var (_, _, parentComment) = await service.AddCommentAsync(postId, "user1", "Parent");
            await service.AddCommentAsync(postId, "user1", "Reply 1", null, parentComment!.Id);
            await service.AddCommentAsync(postId, "user1", "Reply 2", null, parentComment.Id);

            // Act
            var replies = await service.GetRepliesAsync(parentComment.Id);

            // Assert
            replies.Should().HaveCount(2);
            replies.Should().AllSatisfy(r => r.ParentCommentId.Should().Be(parentComment.Id));
        }

        #endregion
    }
}


