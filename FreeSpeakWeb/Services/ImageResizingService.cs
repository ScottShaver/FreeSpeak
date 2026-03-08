using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Defines the size variants available for images
/// </summary>
public enum ImageSize
{
    /// <summary>
    /// Thumbnail size - 300px max dimension (for feed, lists)
    /// </summary>
    Thumbnail,
    
    /// <summary>
    /// Medium size - 400px max dimension (for profile pages, detail views)
    /// </summary>
    Medium,
    
    /// <summary>
    /// Full size - original image (for image viewer, downloads)
    /// </summary>
    Full
}

/// <summary>
/// Service for resizing images with caching to improve performance
/// </summary>
public class ImageResizingService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImageResizingService> _logger;
    private readonly string _cacheBasePath;

    // Size configurations
    private const int ThumbnailMaxSize = 300;  // Increased from 150 to prevent pixelation when displayed
    private const int MediumMaxSize = 400;
    private const int JpegQuality = 85;

    public ImageResizingService(
        IWebHostEnvironment environment,
        ILogger<ImageResizingService> logger)
    {
        _environment = environment;
        _logger = logger;
        _cacheBasePath = Path.Combine(_environment.ContentRootPath, "AppData", "cache", "resized-images");
        
        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheBasePath);
    }

    /// <summary>
    /// Gets a resized version of an image, using cache if available
    /// </summary>
    /// <param name="originalImagePath">Full path to the original image</param>
    /// <param name="size">Desired size variant</param>
    /// <returns>Byte array of the resized image</returns>
    public async Task<byte[]?> GetResizedImageAsync(string originalImagePath, ImageSize size)
    {
        _logger.LogInformation("🖼️ GetResizedImageAsync called - Path: {Path}, Size: {Size}", originalImagePath, size);

        try
        {
            // If requesting full size, just return the original
            if (size == ImageSize.Full)
            {
                _logger.LogInformation("⚡ Returning full-size image (no resize needed)");
                if (File.Exists(originalImagePath))
                {
                    return await File.ReadAllBytesAsync(originalImagePath);
                }
                _logger.LogWarning("❌ Original file not found: {Path}", originalImagePath);
                return null;
            }

            // Check if we have a cached version
            var cacheKey = GenerateCacheKey(originalImagePath, size);
            var cachedPath = Path.Combine(_cacheBasePath, cacheKey);

            _logger.LogInformation("🔍 Cache lookup - Key: {CacheKey}, Path: {CachePath}", cacheKey, cachedPath);

            if (File.Exists(cachedPath))
            {
                // Check if cached version is newer than original
                var originalModified = File.GetLastWriteTimeUtc(originalImagePath);
                var cachedModified = File.GetLastWriteTimeUtc(cachedPath);

                if (cachedModified >= originalModified)
                {
                    _logger.LogInformation("✅ Cache HIT - Returning cached resized image: {CacheKey}", cacheKey);
                    return await File.ReadAllBytesAsync(cachedPath);
                }
                else
                {
                    _logger.LogInformation("🔄 Cache STALE - Original modified, deleting old cache: {CacheKey}", cacheKey);
                    // Original was updated, delete old cache
                    File.Delete(cachedPath);
                }
            }
            else
            {
                _logger.LogInformation("❌ Cache MISS - No cached version exists for: {CacheKey}", cacheKey);
            }

            // No valid cache, resize the image
            _logger.LogInformation("🎨 RESIZING IMAGE - Creating new thumbnail...");
            return await ResizeAndCacheImageAsync(originalImagePath, size, cachedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ERROR in GetResizedImageAsync: {Message}", ex.Message);

            // Fallback to original image on error
            try
            {
                if (File.Exists(originalImagePath))
                {
                    _logger.LogWarning("⚠️ Falling back to original image due to error");
                    return await File.ReadAllBytesAsync(originalImagePath);
                }
            }
            catch
            {
                // Ignore fallback errors
            }

            return null;
        }
    }

    /// <summary>
    /// Resizes an image and saves it to cache
    /// </summary>
    private async Task<byte[]?> ResizeAndCacheImageAsync(string originalImagePath, ImageSize size, string cachePath)
    {
        _logger.LogInformation("🔧 ResizeAndCacheImageAsync - Original: {Original}, Cache: {Cache}", 
            Path.GetFileName(originalImagePath), Path.GetFileName(cachePath));

        if (!File.Exists(originalImagePath))
        {
            _logger.LogError("❌ Original image file not found: {Path}", originalImagePath);
            return null;
        }

        _logger.LogInformation("📂 Loading image from: {Path}", originalImagePath);
        using var image = await Image.LoadAsync(originalImagePath);

        _logger.LogInformation("📐 Original dimensions: {Width}x{Height}", image.Width, image.Height);

        // Determine target size based on size variant
        var maxDimension = size switch
        {
            ImageSize.Thumbnail => ThumbnailMaxSize,
            ImageSize.Medium => MediumMaxSize,
            _ => 0 // Full size, shouldn't get here
        };

        // Calculate new dimensions maintaining aspect ratio
        var (newWidth, newHeight) = CalculateResizeDimensions(
            image.Width, 
            image.Height, 
            maxDimension);

        _logger.LogInformation("🎯 Target dimensions: {Width}x{Height} (max: {Max}px)", newWidth, newHeight, maxDimension);

        // Resize the image
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(newWidth, newHeight),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3 // High quality resampling
        }));

        _logger.LogInformation("💾 Saving resized image to cache: {Path}", cachePath);

        // Save to cache
        await image.SaveAsJpegAsync(cachePath, new JpegEncoder
        {
            Quality = JpegQuality
        });

        var fileInfo = new FileInfo(cachePath);
        _logger.LogInformation("✅ THUMBNAIL CREATED! Size: {Size}, Saved: {Bytes} bytes at {Path}", 
            size, fileInfo.Length, cachePath);

        // Return the cached version
        return await File.ReadAllBytesAsync(cachePath);
    }

    /// <summary>
    /// Calculates new dimensions maintaining aspect ratio
    /// </summary>
    private static (int width, int height) CalculateResizeDimensions(
        int originalWidth, 
        int originalHeight, 
        int maxDimension)
    {
        if (originalWidth <= maxDimension && originalHeight <= maxDimension)
        {
            // Image is already smaller than target
            return (originalWidth, originalHeight);
        }

        double ratio = originalWidth > originalHeight
            ? (double)maxDimension / originalWidth
            : (double)maxDimension / originalHeight;

        return (
            (int)(originalWidth * ratio),
            (int)(originalHeight * ratio)
        );
    }

    /// <summary>
    /// Generates a cache key for an image and size combination
    /// </summary>
    private static string GenerateCacheKey(string originalPath, ImageSize size)
    {
        // Create a unique key based on file path and size
        var fileHash = Path.GetFileNameWithoutExtension(originalPath);
        return $"{fileHash}_{size.ToString().ToLower()}.jpg";
    }

    /// <summary>
    /// Clears the entire image cache
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheBasePath))
            {
                var files = Directory.GetFiles(_cacheBasePath);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
                _logger.LogInformation("Cleared {Count} cached images", files.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing image cache");
        }
    }

    /// <summary>
    /// Clears cache entries older than specified days
    /// </summary>
    public void ClearOldCache(int olderThanDays = 30)
    {
        try
        {
            if (Directory.Exists(_cacheBasePath))
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
                var files = Directory.GetFiles(_cacheBasePath);
                var deletedCount = 0;

                foreach (var file in files)
                {
                    var lastAccess = File.GetLastAccessTimeUtc(file);
                    if (lastAccess < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }

                _logger.LogInformation("Cleared {Count} old cached images (older than {Days} days)", 
                    deletedCount, olderThanDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing old cache entries");
        }
    }
}
