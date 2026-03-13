using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories;

/// <summary>
/// Implementation of IPinnedPostRepository for managing pinned posts
/// </summary>
public class PinnedPostRepository : IPinnedPostRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public PinnedPostRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PinnedPost?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts.FindAsync(id);
    }

    public async Task<List<PinnedPost>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts.ToListAsync();
    }

    public async Task<PinnedPost> AddAsync(PinnedPost entity)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.PinnedPosts.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(PinnedPost entity)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.PinnedPosts.Update(entity);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.PinnedPosts.FindAsync(id);
        if (entity != null)
        {
            context.PinnedPosts.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(PinnedPost entity)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.PinnedPosts.Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts.AnyAsync(pp => pp.Id == id);
    }

    public async Task<bool> IsPostPinnedAsync(int postId, string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts
            .AnyAsync(pp => pp.PostId == postId && pp.UserId == userId);
    }

    public async Task<PinnedPost?> GetPinnedPostAsync(int postId, string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts
            .FirstOrDefaultAsync(pp => pp.PostId == postId && pp.UserId == userId);
    }

    public async Task<List<PinnedPost>> GetPinnedPostsByPostIdAsync(int postId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts
            .Where(pp => pp.PostId == postId)
            .ToListAsync();
    }

    public async Task<List<PinnedPost>> GetUserPinnedPostsAsync(string userId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts
            .Where(pp => pp.UserId == userId)
            .OrderByDescending(pp => pp.PinnedAt)
            .ToListAsync();
    }

    public async Task RemovePinnedPostsByPostIdAsync(int postId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var pinnedPosts = await context.PinnedPosts
            .Where(pp => pp.PostId == postId)
            .ToListAsync();

        if (pinnedPosts.Any())
        {
            context.PinnedPosts.RemoveRange(pinnedPosts);
            await context.SaveChangesAsync();
        }
    }
}
