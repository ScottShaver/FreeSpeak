using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.Abstractions;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Abstract base class providing common repository functionality for posts.
    /// Concrete implementations can inherit from this to reduce code duplication.
    /// </summary>
    /// <typeparam name="TPost">The post entity type</typeparam>
    /// <typeparam name="TImage">The post image entity type</typeparam>
    /// <typeparam name="TContext">The DbContext type</typeparam>
    public abstract class BasePostRepository<TPost, TImage, TContext>
        where TPost : class, IPostEntity
        where TImage : class, IPostImage
        where TContext : DbContext
    {
        protected readonly IDbContextFactory<TContext> ContextFactory;
        protected readonly ILogger Logger;

        protected BasePostRepository(
            IDbContextFactory<TContext> contextFactory,
            ILogger logger)
        {
            ContextFactory = contextFactory;
            Logger = logger;
        }

        #region Abstract Members

        /// <summary>
        /// Gets the DbSet for posts
        /// </summary>
        protected abstract DbSet<TPost> GetPostSet(TContext context);

        /// <summary>
        /// Gets the DbSet for images
        /// </summary>
        protected abstract DbSet<TImage> GetImageSet(TContext context);

        /// <summary>
        /// Creates a query with standard includes (author, images)
        /// </summary>
        protected abstract IQueryable<TPost> CreateBaseQuery(TContext context, bool includeAuthor, bool includeImages);

        /// <summary>
        /// Gets the images navigation property for a post
        /// </summary>
        protected abstract Expression<Func<TPost, IEnumerable<TImage>>> GetImagesExpression();

        #endregion

        #region Common Operations

        protected async Task<TPost?> GetByIdInternalAsync(int postId, bool includeAuthor, bool includeImages)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                return await CreateBaseQuery(context, includeAuthor, includeImages)
                    .FirstOrDefaultAsync(p => p.Id == postId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving post {PostId}", postId);
                return null;
            }
        }

        protected async Task<bool> ExistsInternalAsync(int postId)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                return await GetPostSet(context).AnyAsync(p => p.Id == postId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking existence of post {PostId}", postId);
                return false;
            }
        }

        protected async Task<List<TPost>> GetByAuthorInternalAsync(string authorId, int skip, int take)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                return await CreateBaseQuery(context, true, true)
                    .Where(p => p.AuthorId == authorId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving posts for author {AuthorId}", authorId);
                return new List<TPost>();
            }
        }

        protected async Task<int> GetCountByAuthorInternalAsync(string authorId)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                return await GetPostSet(context).CountAsync(p => p.AuthorId == authorId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error counting posts for author {AuthorId}", authorId);
                return 0;
            }
        }

        protected async Task<List<TImage>> GetImagesInternalAsync(int postId)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                return await GetImageSet(context)
                    .Where(img => img.PostId == postId)
                    .OrderBy(img => img.DisplayOrder)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving images for post {PostId}", postId);
                return new List<TImage>();
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Executes an action with a new context and handles exceptions
        /// </summary>
        protected async Task<(bool Success, string? ErrorMessage)> ExecuteWithContextAsync(
            Func<TContext, Task> action,
            string errorMessage,
            string operationName,
            params object[] logArgs)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                await action(context);
                return (true, null);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, operationName, logArgs);
                return (false, errorMessage);
            }
        }

        /// <summary>
        /// Executes a function with a new context and handles exceptions
        /// </summary>
        protected async Task<TResult?> ExecuteWithContextAsync<TResult>(
            Func<TContext, Task<TResult>> func,
            string operationName,
            params object[] logArgs)
            where TResult : class
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                return await func(context);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, operationName, logArgs);
                return null;
            }
        }

        /// <summary>
        /// Validates that a user can perform an operation on a post
        /// </summary>
        protected async Task<(bool IsValid, TPost? Post, string? ErrorMessage)> ValidatePostOwnershipAsync(
            int postId, string userId, string actionDescription)
        {
            using var context = await ContextFactory.CreateDbContextAsync();

            var post = await GetPostSet(context).FindAsync(postId);

            if (post == null)
                return (false, null, "Post not found.");

            if (post.AuthorId != userId)
                return (false, null, $"You are not authorized to {actionDescription} this post.");

            return (true, post, null);
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for repository operations
    /// </summary>
    public static class RepositoryExtensions
    {
        /// <summary>
        /// Applies pagination to a query
        /// </summary>
        public static IQueryable<T> Paginate<T>(this IQueryable<T> query, int skip, int take)
        {
            return query.Skip(skip).Take(take);
        }

        /// <summary>
        /// Checks if pagination parameters indicate "has more" items
        /// </summary>
        public static async Task<(List<T> Items, bool HasMore)> ToListWithHasMoreAsync<T>(
            this IQueryable<T> query, int pageSize)
        {
            var items = await query.Take(pageSize + 1).ToListAsync();
            var hasMore = items.Count > pageSize;
            if (hasMore)
                items = items.Take(pageSize).ToList();
            return (items, hasMore);
        }
    }
}
