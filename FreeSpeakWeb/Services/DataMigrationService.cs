using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Service for handling data migrations that don't require schema changes
/// </summary>
public class DataMigrationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<DataMigrationService> _logger;
    private readonly IWebHostEnvironment _environment;

    public DataMigrationService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<DataMigrationService> logger,
        IWebHostEnvironment environment)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Updates profile picture URLs from old format to secure API format
    /// </summary>
    public async Task MigrateProfilePictureUrlsAsync()
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // First, check what URLs we have
            var allUrls = await context.Users
                .Where(u => u.ProfilePictureUrl != null)
                .Select(u => new { u.UserName, u.ProfilePictureUrl })
                .Take(5)
                .ToListAsync();

            _logger.LogWarning("📊 Sample profile picture URLs before migration:");
            foreach (var u in allUrls)
            {
                _logger.LogWarning("   {UserName}: {Url}", u.UserName, u.ProfilePictureUrl);
            }

            // Update all profile picture URLs from /api/profile-picture/ to /api/secure-files/profile-picture/
            var updateCount = await context.Database.ExecuteSqlRawAsync(
                @"UPDATE ""AspNetUsers"" 
                  SET ""ProfilePictureUrl"" = REPLACE(""ProfilePictureUrl"", '/api/profile-picture/', '/api/secure-files/profile-picture/')
                  WHERE ""ProfilePictureUrl"" LIKE '/api/profile-picture/%'");

            _logger.LogInformation("✅ Migrated {Count} profile picture URLs to secure format", updateCount);

            if (updateCount == 0)
            {
                _logger.LogWarning("⚠️ No URLs needed migration - they may already be in the correct format");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error migrating profile picture URLs");
            throw;
        }
    }

    /// <summary>
    /// Updates post image URLs from old format to secure API format
    /// This is for any existing images that might have the old format
    /// </summary>
    public async Task MigratePostImageUrlsAsync()
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Update post images to use secure API URLs
            // Old format: /uploads/posts/{userId}/images/{filename}
            // New format: /api/secure-files/post-image/{userId}/{imageId}/{filename}
            // Note: This migration assumes imageId is the same as the GUID in the filename
            
            var images = await context.PostImages
                .Where(pi => pi.ImageUrl.StartsWith("/uploads/posts/"))
                .ToListAsync();

            foreach (var image in images)
            {
                // Extract userId and filename from old URL
                // Format: /uploads/posts/{userId}/images/{filename}
                var parts = image.ImageUrl.Split('/');
                if (parts.Length >= 6)
                {
                    var userId = parts[3];
                    var filename = parts[5];
                    
                    // Extract the GUID from the filename (before the extension)
                    var imageId = Path.GetFileNameWithoutExtension(filename);
                    
                    // Build new secure URL
                    image.ImageUrl = $"/api/secure-files/post-image/{userId}/{imageId}/{filename}";
                }
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Migrated {Count} post image URLs to secure format", images.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating post image URLs");
            throw;
        }
    }

    /// <summary>
    /// Moves uploaded files from wwwroot to AppData (outside public access)
    /// IMPORTANT: Run this once during deployment to secure existing files
    /// </summary>
    public async Task MoveFilesOutOfWwwrootAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var movedCount = 0;

                // Move profile pictures
                var oldProfilePath = Path.Combine(_environment.WebRootPath, "images", "profiles");
                var newProfilePath = Path.Combine(_environment.ContentRootPath, "AppData", "images", "profiles");

                if (Directory.Exists(oldProfilePath))
                {
                    Directory.CreateDirectory(newProfilePath);
                    foreach (var file in Directory.GetFiles(oldProfilePath))
                    {
                        var fileName = Path.GetFileName(file);
                        var newFile = Path.Combine(newProfilePath, fileName);

                        if (!File.Exists(newFile))
                        {
                            File.Copy(file, newFile);
                            movedCount++;
                        }
                    }
                    _logger.LogInformation("Moved {Count} profile pictures from wwwroot to AppData", movedCount);
                }

                // Move post uploads
                var oldUploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "posts");
                var newUploadsPath = Path.Combine(_environment.ContentRootPath, "AppData", "uploads", "posts");

                if (Directory.Exists(oldUploadsPath))
                {
                    movedCount = 0;
                    CopyDirectory(oldUploadsPath, newUploadsPath, ref movedCount);
                    _logger.LogInformation("Moved {Count} post files from wwwroot to AppData", movedCount);
                }

                _logger.LogInformation("File migration completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving files from wwwroot to AppData");
                throw;
            }
        });
    }

    private void CopyDirectory(string sourceDir, string destDir, ref int count)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);

            if (!File.Exists(destFile))
            {
                File.Copy(file, destFile);
                count++;
            }
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var destSubDir = Path.Combine(destDir, dirName);
            CopyDirectory(dir, destSubDir, ref count);
        }
    }
}
