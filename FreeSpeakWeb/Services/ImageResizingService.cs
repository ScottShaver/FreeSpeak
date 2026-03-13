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

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageResizingService"/> class.
    /// </summary>
    /// <param name="environment">Web hosting environment for determining file paths.</param>
    /// <param name="logger">Logger for recording service operations.</param>
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
        try
        {
            // If requesting full size, just return the original
            if (size == ImageSize.Full)
            {
                if (File.Exists(originalImagePath))
                {
                    return await File.ReadAllBytesAsync(originalImagePath);
                }
                return null;
            }

            // Check if we have a cached version
            var cacheKey = GenerateCacheKey(originalImagePath, size);
            var cachedPath = Path.Combine(_cacheBasePath, cacheKey);

            if (File.Exists(cachedPath))
            {
                // Check if cached version is newer than original
                var originalModified = File.GetLastWriteTimeUtc(originalImagePath);
                var cachedModified = File.GetLastWriteTimeUtc(cachedPath);

                if (cachedModified >= originalModified)
                {
                    return await File.ReadAllBytesAsync(cachedPath);
                }
                else
                {
                    // Original was updated, delete old cache
                    File.Delete(cachedPath);
                }
            }

            // No valid cache, resize the image
            return await ResizeAndCacheImageAsync(originalImagePath, size, cachedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetResizedImageAsync: {Message}", ex.Message);

            // Fallback to original image on error
            try
            {
                if (File.Exists(originalImagePath))
                {
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
    /// Resizes an image and saves it to the cache directory.
    /// </summary>
    /// <param name="originalImagePath">Full path to the original image file.</param>
    /// <param name="size">The desired size variant.</param>
    /// <param name="cachePath">Full path where the cached image should be saved.</param>
    /// <returns>Byte array of the resized image, or null if the operation fails.</returns>
    private async Task<byte[]?> ResizeAndCacheImageAsync(string originalImagePath, ImageSize size, string cachePath)
    {
        if (!File.Exists(originalImagePath))
        {
            _logger.LogError("Original image file not found: {Path}", originalImagePath);
            return null;
        }

        using var image = await Image.LoadAsync(originalImagePath);

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

        // Resize the image
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(newWidth, newHeight),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3 // High quality resampling
        }));

        // Save to cache
        await image.SaveAsJpegAsync(cachePath, new JpegEncoder
        {
            Quality = JpegQuality
        });

        // Return the cached version
        return await File.ReadAllBytesAsync(cachePath);
    }

    /// <summary>
    /// Calculates new dimensions for an image while maintaining aspect ratio.
    /// Returns original dimensions if the image is already smaller than the target.
    /// </summary>
    /// <param name="originalWidth">The original width of the image in pixels.</param>
    /// <param name="originalHeight">The original height of the image in pixels.</param>
    /// <param name="maxDimension">The maximum allowed dimension for either width or height.</param>
    /// <returns>A tuple containing the calculated width and height.</returns>
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
    /// Generates a unique cache key for an image and size combination.
    /// </summary>
    /// <param name="originalPath">Full path to the original image file.</param>
    /// <param name="size">The size variant being cached.</param>
    /// <returns>A unique filename to use as the cache key.</returns>
    private static string GenerateCacheKey(string originalPath, ImageSize size)
    {
        // Create a unique key based on file path and size
        var fileHash = Path.GetFileNameWithoutExtension(originalPath);
        return $"{fileHash}_{size.ToString().ToLower()}.jpg";
    }

    /// <summary>
    /// Clears all cached resized images from the cache directory.
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
    /// Clears cache entries that haven't been accessed within the specified number of days.
    /// </summary>
    /// <param name="olderThanDays">Number of days since last access after which entries are deleted.</param>
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
