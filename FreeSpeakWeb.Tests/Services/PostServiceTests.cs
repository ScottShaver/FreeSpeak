using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    public class PostServiceTests : TestBase
    {
        #region Post Operations Tests

        [Fact]
        public async Task CreatePostAsync_WithValidContent_ShouldCreatePost()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PostTest1");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            using (var context = await dbFactory.CreateDbContextAsync())
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

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("PostTest2");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            // Act
            var (success, errorMessage, post) = await service.CreatePostAsync("user1", "");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("cannot be empty");
            post.Should().BeNull();
        }

        [Fact]
        public async Task CreatePostAsync_WithImages_ShouldCreatePostAndImages()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PostTest3");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }

            var imageUrls = new List<string> { "image1.jpg", "image2.jpg" };

            // Act
            var (success, errorMessage, post) = await service.CreatePostAsync("user1", "Post with images", imageUrls);

            // Assert
            success.Should().BeTrue();
            post.Should().NotBeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("PostTest4");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", "Original content");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage) = await service.UpdatePostAsync(postId, "user1", "Updated content");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("PostTest5");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage) = await service.UpdatePostAsync(postId, "user2", "Hacked content");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("not authorized");
        }

        [Fact]
        public async Task DeletePostAsync_ByAuthor_ShouldRemovePost()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PostTest6");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage) = await service.DeletePostAsync(postId, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var deletedPost = context.Posts.Find(postId);
                deletedPost.Should().BeNull();
            }
        }

        [Fact]
        public async Task GetFeedPostsAsync_ShouldReturnUserAndFriendsPosts()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PostTest7");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var user3 = TestDataFactory.CreateTestUser(id: "user3");

            var friendship = TestDataFactory.CreateTestFriendship("user1", "user2", FriendshipStatus.Accepted);
            
            var post1 = TestDataFactory.CreateTestPost("user1", "User1 post");
            var post2 = TestDataFactory.CreateTestPost("user2", "User2 post");
            var post3 = TestDataFactory.CreateTestPost("user3", "User3 post");

            using (var context = await dbFactory.CreateDbContextAsync())
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

        #endregion

        #region Comment Operations Tests

        [Fact]
        public async Task AddCommentAsync_WithValidData_ShouldCreateComment()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PostTest8");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
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

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                var updatedPost = context.Posts.Find(postId);
                updatedPost!.CommentCount.Should().Be(1);
            }
        }

        [Fact]
        public async Task AddCommentAsync_WithReply_ShouldCreateNestedComment()
        {
            // Arrange
            var dbFactory = CreateDbContextFactory("PostTest9");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            var parentComment = TestDataFactory.CreateTestComment(1, "user1", "Parent comment");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
                
                parentComment.PostId = context.Posts.First().Id;
                context.Comments.Add(parentComment);
                await context.SaveChangesAsync();
            }

            int postId, parentCommentId;
            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("PostTest10");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", commentCount: 1);
            var comment = TestDataFactory.CreateTestComment(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                comment.PostId = context.Posts.First().Id;
                context.Comments.Add(comment);
                await context.SaveChangesAsync();
            }

            int commentId, postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                commentId = context.Comments.First().Id;
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage) = await service.DeleteCommentAsync(commentId, "user1");

            // Assert
            success.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("PostTest11");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage, isLiked) = await service.ToggleLikeAsync(postId, "user1");

            // Assert
            success.Should().BeTrue();
            isLiked.Should().BeTrue();

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("PostTest12");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1", likeCount: 1);
            var like = TestDataFactory.CreateTestLike(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                like.PostId = context.Posts.First().Id;
                context.Likes.Add(like);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
            {
                postId = context.Posts.First().Id;
            }

            // Act
            var (success, errorMessage, isLiked) = await service.ToggleLikeAsync(postId, "user1");

            // Assert
            success.Should().BeTrue();
            isLiked.Should().BeFalse();

            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("PostTest13");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");
            var like = TestDataFactory.CreateTestLike(1, "user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();

                like.PostId = context.Posts.First().Id;
                context.Likes.Add(like);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("PostTest14");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
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
            var dbFactory = CreateDbContextFactory("PostTest15");
            var logger = CreateMockLogger<PostService>();
            var service = new PostService(dbFactory, logger);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var post = TestDataFactory.CreateTestPost("user1");

            using (var context = await dbFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Posts.Add(post);
                await context.SaveChangesAsync();
            }

            int postId;
            using (var context = await dbFactory.CreateDbContextAsync())
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
    }
}
