using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories;

/// <summary>
/// Implementation of IPostNotificationMuteRepository for managing notification mute preferences
/// </summary>
public class PostNotificationMuteRepository : IPostNotificationMuteRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<PostNotificationMuteRepository> _logger;
    private readonly ProfilerHelper _profiler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostNotificationMuteRepository"/> class.
    /// </summary>
    /// <param name="contextFactory">Factory for creating database contexts.</param>
    /// <param name="logger">Logger for recording repository operations.</param>
    /// <param name="profiler">Helper for profiling repository operations.</param>
    public PostNotificationMuteRepository(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<PostNotificationMuteRepository> logger,
        ProfilerHelper profiler)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _profiler = profiler;
    }

    public async Task<PostNotificationMute?> GetByIdAsync(int id)
    {
        using var step = _profiler.Step($"PostNotificationMuteRepository.GetByIdAsync({id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PostNotificationMutes.FindAsync(id);
    }

    public async Task<List<PostNotificationMute>> GetAllAsync()
    {
        using var step = _profiler.Step("PostNotificationMuteRepository.GetAllAsync");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PostNotificationMutes.ToListAsync();
    }

    public async Task<PostNotificationMute> AddAsync(PostNotificationMute entity)
    {
        using var step = _profiler.Step("PostNotificationMuteRepository.AddAsync");
        using var context = await _contextFactory.CreateDbContextAsync();
        context.PostNotificationMutes.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(PostNotificationMute entity)
    {
        using var step = _profiler.Step($"PostNotificationMuteRepository.UpdateAsync({entity.Id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        context.PostNotificationMutes.Update(entity);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var step = _profiler.Step($"PostNotificationMuteRepository.DeleteAsync({id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.PostNotificationMutes.FindAsync(id);
        if (entity != null)
        {
            context.PostNotificationMutes.Remove(entity);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(PostNotificationMute entity)
    {
        using var step = _profiler.Step($"PostNotificationMuteRepository.DeleteAsync(entity:{entity.Id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        context.PostNotificationMutes.Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int id)
    {
        using var step = _profiler.Step($"PostNotificationMuteRepository.ExistsAsync({id})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PostNotificationMutes.AnyAsync(m => m.Id == id);
    }

    public async Task<bool> IsPostMutedAsync(int postId, string userId)
    {
        using var step = _profiler.Step($"PostNotificationMuteRepository.IsPostMutedAsync({postId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PostNotificationMutes
            .AnyAsync(m => m.PostId == postId && m.UserId == userId);
    }

    public async Task<PostNotificationMute?> GetMuteRecordAsync(int postId, string userId)
    {
        using var step = _profiler.Step($"PostNotificationMuteRepository.GetMuteRecordAsync({postId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PostNotificationMutes
            .FirstOrDefaultAsync(m => m.PostId == postId && m.UserId == userId);
    }

    public async Task<List<PostNotificationMute>> GetMuteRecordsByPostIdAsync(int postId)
    {
        using var step = _profiler.Step($"PostNotificationMuteRepository.GetMuteRecordsByPostIdAsync({postId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PostNotificationMutes
            .Where(m => m.PostId == postId)
            .ToListAsync();
    }

    public async Task<List<PostNotificationMute>> GetUserMutedPostsAsync(string userId)
    {
        using var step = _profiler.Step($"PostNotificationMuteRepository.GetUserMutedPostsAsync({userId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PostNotificationMutes
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.MutedAt)
            .ToListAsync();
    }

    public async Task RemoveMuteRecordsByPostIdAsync(int postId)
    {
        using var step = _profiler.Step($"PostNotificationMuteRepository.RemoveMuteRecordsByPostIdAsync({postId})");
        using var context = await _contextFactory.CreateDbContextAsync();
        var muteRecords = await context.PostNotificationMutes
            .Where(m => m.PostId == postId)
            .ToListAsync();

        if (muteRecords.Any())
        {
            context.PostNotificationMutes.RemoveRange(muteRecords);
            await context.SaveChangesAsync();
        }
    }
}
