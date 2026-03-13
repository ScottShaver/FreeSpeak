using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Service for handling data migrations that don't require schema changes.
/// Includes migrations for securing file URLs and moving files outside wwwroot.
/// These migrations are typically run once during application startup in development.
/// </summary>
public class DataMigrationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<DataMigrationService> _logger;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataMigrationService"/> class.
    /// </summary>
    /// <param name="contextFactory">Factory for creating database contexts.</param>
    /// <param name="logger">Logger for recording migration progress and errors.</param>
    /// <param name="environment">Web host environment for accessing file paths.</param>
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
    /// Updates profile picture URLs from old format (/api/profile-picture/) to 
    /// secure API format (/api/secure-files/profile-picture/).
    /// </summary>
    /// <returns>A task representing the asynchronous migration operation.</returns>
    /// <exception cref="Exception">Rethrows any exception after logging.</exception>
    public async Task MigrateProfilePictureUrlsAsync()
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // SECURITY: Use LINQ instead of raw SQL to prevent potential SQL injection
            // Find all users with old format profile picture URLs
            var usersToUpdate = await context.Users
                .Where(u => u.ProfilePictureUrl != null && 
                           u.ProfilePictureUrl.StartsWith("/api/profile-picture/"))
                .ToListAsync();

            if (usersToUpdate.Any())
            {
                // Update each user's profile picture URL
                foreach (var user in usersToUpdate)
                {
                    user.ProfilePictureUrl = user.ProfilePictureUrl!.Replace(
                        "/api/profile-picture/", 
                        "/api/secure-files/profile-picture/");
                }

                await context.SaveChangesAsync();
                _logger.LogInformation("Migrated {Count} profile picture URLs to secure format", usersToUpdate.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating profile picture URLs");
            throw;
        }
    }

    /// <summary>
    /// Updates post image URLs from old static format (/uploads/posts/{userId}/images/{filename})
    /// to secure API format (/api/secure-files/post-image/{userId}/{imageId}/{filename}).
    /// </summary>
    /// <returns>A task representing the asynchronous migration operation.</returns>
    /// <exception cref="Exception">Rethrows any exception after logging.</exception>
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
    /// Moves uploaded files from wwwroot (publicly accessible) to AppData (protected).
    /// This is a security measure to prevent direct file access without authentication.
    /// Should be run once during deployment to secure existing files.
    /// </summary>
    /// <returns>A task representing the asynchronous file move operation.</returns>
    /// <exception cref="Exception">Rethrows any exception after logging.</exception>
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

    /// <summary>
    /// Recursively copies a directory and its contents to a new location.
    /// Only copies files that don't already exist at the destination.
    /// </summary>
    /// <param name="sourceDir">The source directory path to copy from.</param>
    /// <param name="destDir">The destination directory path to copy to.</param>
    /// <param name="count">A reference counter incremented for each file copied.</param>
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
