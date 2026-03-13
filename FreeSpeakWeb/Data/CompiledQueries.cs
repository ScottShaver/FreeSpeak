using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Contains pre-compiled EF Core queries for frequently executed database operations.
    /// Compiled queries eliminate the overhead of query compilation on each execution,
    /// providing 10-20% performance improvement for hot code paths.
    /// </summary>
    /// <remarks>
    /// Usage: Call the static methods directly with an ApplicationDbContext instance.
    /// These queries are compiled once at application startup and reused for all subsequent calls.
    /// </remarks>
    public static class CompiledQueries
    {
        #region Post Queries

        /// <summary>
        /// Compiled query to retrieve a post by ID with author and images.
        /// </summary>
        private static readonly Func<ApplicationDbContext, int, Task<Post?>> GetPostByIdCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, int postId) =>
                context.Posts
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .FirstOrDefault(p => p.Id == postId));

        /// <summary>
        /// Retrieves a post by its unique identifier using a compiled query.
        /// Includes author information and images ordered by display order.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>The post if found; otherwise, null.</returns>
        public static Task<Post?> GetPostByIdAsync(ApplicationDbContext context, int postId)
        {
            return GetPostByIdCompiledQuery(context, postId);
        }

        /// <summary>
        /// Compiled query to check if a post exists.
        /// </summary>
        private static readonly Func<ApplicationDbContext, int, Task<bool>> PostExistsCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, int postId) =>
                context.Posts.Any(p => p.Id == postId));

        /// <summary>
        /// Checks whether a post with the specified ID exists using a compiled query.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="postId">The unique identifier of the post.</param>
        /// <returns>True if the post exists; otherwise, false.</returns>
        public static Task<bool> PostExistsAsync(ApplicationDbContext context, int postId)
        {
            return PostExistsCompiledQuery(context, postId);
        }

        /// <summary>
        /// Compiled query to get posts by author ID.
        /// </summary>
        private static readonly Func<ApplicationDbContext, string, int, int, IAsyncEnumerable<Post>> GetPostsByAuthorCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, string authorId, int skip, int take) =>
                context.Posts
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Where(p => p.AuthorId == authorId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take));

        /// <summary>
        /// Retrieves posts by author ID using a compiled query with pagination.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="authorId">The ID of the post author.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to retrieve.</param>
        /// <returns>A list of posts by the specified author.</returns>
        public static async Task<List<Post>> GetPostsByAuthorAsync(
            ApplicationDbContext context, string authorId, int skip, int take)
        {
            var posts = new List<Post>();
            await foreach (var post in GetPostsByAuthorCompiledQuery(context, authorId, skip, take))
            {
                posts.Add(post);
            }
            return posts;
        }

        /// <summary>
        /// Compiled query to count posts by author ID.
        /// </summary>
        private static readonly Func<ApplicationDbContext, string, Task<int>> GetPostCountByAuthorCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, string authorId) =>
                context.Posts.Count(p => p.AuthorId == authorId));

        /// <summary>
        /// Gets the total count of posts by a specific author using a compiled query.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="authorId">The ID of the post author.</param>
        /// <returns>The total number of posts by the author.</returns>
        public static Task<int> GetPostCountByAuthorAsync(ApplicationDbContext context, string authorId)
        {
            return GetPostCountByAuthorCompiledQuery(context, authorId);
        }

        /// <summary>
        /// Compiled query to get public posts with pagination.
        /// </summary>
        private static readonly Func<ApplicationDbContext, int, int, IAsyncEnumerable<Post>> GetPublicPostsCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, int skip, int take) =>
                context.Posts
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Where(p => p.AudienceType == AudienceType.Public)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take));

        /// <summary>
        /// Retrieves public posts using a compiled query with pagination.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to retrieve.</param>
        /// <returns>A list of public posts.</returns>
        public static async Task<List<Post>> GetPublicPostsAsync(
            ApplicationDbContext context, int skip, int take)
        {
            var posts = new List<Post>();
            await foreach (var post in GetPublicPostsCompiledQuery(context, skip, take))
            {
                posts.Add(post);
            }
            return posts;
        }

        #endregion

        #region Group Post Queries

        /// <summary>
        /// Compiled query to retrieve a group post by ID with author and images.
        /// </summary>
        private static readonly Func<ApplicationDbContext, int, Task<GroupPost?>> GetGroupPostByIdCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, int postId) =>
                context.GroupPosts
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(p => p.Author)
                    .Include(p => p.Group)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .FirstOrDefault(p => p.Id == postId));

        /// <summary>
        /// Retrieves a group post by its unique identifier using a compiled query.
        /// Includes author, group, and images information.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="postId">The unique identifier of the group post.</param>
        /// <returns>The group post if found; otherwise, null.</returns>
        public static Task<GroupPost?> GetGroupPostByIdAsync(ApplicationDbContext context, int postId)
        {
            return GetGroupPostByIdCompiledQuery(context, postId);
        }

        /// <summary>
        /// Compiled query to check if a group post exists.
        /// </summary>
        private static readonly Func<ApplicationDbContext, int, Task<bool>> GroupPostExistsCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, int postId) =>
                context.GroupPosts.Any(p => p.Id == postId));

        /// <summary>
        /// Checks whether a group post with the specified ID exists using a compiled query.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="postId">The unique identifier of the group post.</param>
        /// <returns>True if the group post exists; otherwise, false.</returns>
        public static Task<bool> GroupPostExistsAsync(ApplicationDbContext context, int postId)
        {
            return GroupPostExistsCompiledQuery(context, postId);
        }

        /// <summary>
        /// Compiled query to get group posts by group ID.
        /// </summary>
        private static readonly Func<ApplicationDbContext, int, int, int, IAsyncEnumerable<GroupPost>> GetPostsByGroupCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, int groupId, int skip, int take) =>
                context.GroupPosts
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(p => p.Author)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Where(p => p.GroupId == groupId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take));

        /// <summary>
        /// Retrieves posts for a specific group using a compiled query with pagination.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="skip">Number of posts to skip for pagination.</param>
        /// <param name="take">Number of posts to retrieve.</param>
        /// <returns>A list of group posts.</returns>
        public static async Task<List<GroupPost>> GetPostsByGroupAsync(
            ApplicationDbContext context, int groupId, int skip, int take)
        {
            var posts = new List<GroupPost>();
            await foreach (var post in GetPostsByGroupCompiledQuery(context, groupId, skip, take))
            {
                posts.Add(post);
            }
            return posts;
        }

        /// <summary>
        /// Compiled query to count posts in a group.
        /// </summary>
        private static readonly Func<ApplicationDbContext, int, Task<int>> GetPostCountByGroupCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, int groupId) =>
                context.GroupPosts.Count(p => p.GroupId == groupId));

        /// <summary>
        /// Gets the total count of posts in a specific group using a compiled query.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>The total number of posts in the group.</returns>
        public static Task<int> GetPostCountByGroupAsync(ApplicationDbContext context, int groupId)
        {
            return GetPostCountByGroupCompiledQuery(context, groupId);
        }

        #endregion

        #region Friendship Queries

        /// <summary>
        /// Compiled query to get friend IDs for a user.
        /// </summary>
        private static readonly Func<ApplicationDbContext, string, IAsyncEnumerable<string>> GetFriendIdsCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, string userId) =>
                context.Friendships
                    .AsNoTracking()
                    .Where(f => f.Status == FriendshipStatus.Accepted &&
                               (f.RequesterId == userId || f.AddresseeId == userId))
                    .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId));

        /// <summary>
        /// Retrieves the list of friend IDs for a user using a compiled query.
        /// Returns IDs of users who have an accepted friendship with the specified user.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A list of friend user IDs.</returns>
        public static async Task<List<string>> GetFriendIdsAsync(ApplicationDbContext context, string userId)
        {
            var friendIds = new List<string>();
            await foreach (var friendId in GetFriendIdsCompiledQuery(context, userId))
            {
                friendIds.Add(friendId);
            }
            return friendIds;
        }

        #endregion

        #region Group Membership Queries

        /// <summary>
        /// Compiled query to get group IDs for a user.
        /// </summary>
        private static readonly Func<ApplicationDbContext, string, IAsyncEnumerable<int>> GetUserGroupIdsCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, string userId) =>
                context.GroupUsers
                    .AsNoTracking()
                    .Where(gu => gu.UserId == userId)
                    .Select(gu => gu.GroupId));

        /// <summary>
        /// Retrieves the list of group IDs that a user is a member of using a compiled query.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A list of group IDs the user is a member of.</returns>
        public static async Task<List<int>> GetUserGroupIdsAsync(ApplicationDbContext context, string userId)
        {
            var groupIds = new List<int>();
            await foreach (var groupId in GetUserGroupIdsCompiledQuery(context, userId))
            {
                groupIds.Add(groupId);
            }
            return groupIds;
        }

        /// <summary>
        /// Compiled query to check if a user is a member of a group.
        /// </summary>
        private static readonly Func<ApplicationDbContext, int, string, Task<bool>> IsUserGroupMemberCompiledQuery =
            EF.CompileAsyncQuery((ApplicationDbContext context, int groupId, string userId) =>
                context.GroupUsers.Any(gu => gu.GroupId == groupId && gu.UserId == userId));

        /// <summary>
        /// Checks whether a user is a member of a specific group using a compiled query.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user is a member of the group; otherwise, false.</returns>
        public static Task<bool> IsUserGroupMemberAsync(ApplicationDbContext context, int groupId, string userId)
        {
            return IsUserGroupMemberCompiledQuery(context, groupId, userId);
        }

        #endregion
    }
}
