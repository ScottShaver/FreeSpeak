using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories;

/// <summary>
/// Implementation of IPinnedPostRepository for managing pinned posts
/// </summary>
public class PinnedPostRepository : IPinnedPostRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<PinnedPostRepository> _logger;
    private readonly ProfilerHelper _profiler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PinnedPostRepository"/> class.
    /// </summary>
    /// <param name="contextFactory">Factory for creating database contexts.</param>
    /// <param name="logger">Logger for recording repository operations.</param>
    /// <param name="profiler">Helper for profiling repository operations.</param>
    public PinnedPostRepository(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<PinnedPostRepository> logger,
        ProfilerHelper profiler)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _profiler = profiler;
    }

    public async Task<PinnedPost?> GetByIdAsync(int id)
    {
        using var step = _profiler.Step($"PinnedPostRepository.GetByIdAsync({id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts.FindAsync(id);
    }

    public async Task<List<PinnedPost>> GetAllAsync()
    {
        using var step = _profiler.Step("PinnedPostRepository.GetAllAsync");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts.ToListAsync();
    }

    public async Task<PinnedPost> AddAsync(PinnedPost entity)
    {
        using var step = _profiler.Step("PinnedPostRepository.AddAsync");
        using var context = await _contextFactory.CreateDbContextAsync();
        context.PinnedPosts.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(PinnedPost entity)
    {
        using var step = _profiler.Step($"PinnedPostRepository.UpdateAsync({entity.Id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        context.PinnedPosts.Update(entity);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var step = _profiler.Step($"PinnedPostRepository.DeleteAsync({id})");
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
        using var step = _profiler.Step($"PinnedPostRepository.DeleteAsync(entity:{entity.Id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        context.PinnedPosts.Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int id)
    {
        using var step = _profiler.Step($"PinnedPostRepository.ExistsAsync({id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts.AnyAsync(pp => pp.Id == id);
    }

    public async Task<bool> IsPostPinnedAsync(int postId, string userId)
    {
        using var step = _profiler.Step($"PinnedPostRepository.IsPostPinnedAsync({postId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts
            .AnyAsync(pp => pp.PostId == postId && pp.UserId == userId);
    }

    public async Task<PinnedPost?> GetPinnedPostAsync(int postId, string userId)
    {
        using var step = _profiler.Step($"PinnedPostRepository.GetPinnedPostAsync({postId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts
            .FirstOrDefaultAsync(pp => pp.PostId == postId && pp.UserId == userId);
    }

    public async Task<List<PinnedPost>> GetPinnedPostsByPostIdAsync(int postId)
    {
        using var step = _profiler.Step($"PinnedPostRepository.GetPinnedPostsByPostIdAsync({postId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts
            .Where(pp => pp.PostId == postId)
            .ToListAsync();
    }

    public async Task<List<PinnedPost>> GetUserPinnedPostsAsync(string userId)
    {
        using var step = _profiler.Step($"PinnedPostRepository.GetUserPinnedPostsAsync({userId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PinnedPosts
            .Where(pp => pp.UserId == userId)
            .OrderByDescending(pp => pp.PinnedAt)
            .ToListAsync();
    }

    public async Task RemovePinnedPostsByPostIdAsync(int postId)
    {
        using var step = _profiler.Step($"PinnedPostRepository.RemovePinnedPostsByPostIdAsync({postId})");
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
