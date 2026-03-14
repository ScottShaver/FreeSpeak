using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service for handling image and video uploads for posts.
    /// Stores files securely outside wwwroot and returns authenticated API URLs.
    /// Includes DOS protection with file size and count limits.
    /// Validates file signatures (magic bytes) to prevent disguised malicious files.
    /// Scans files for viruses/malware when ClamAV is configured.
    /// </summary>
    public class ImageUploadService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<ImageUploadService> _logger;
        private readonly IFileSignatureValidator _fileSignatureValidator;
        private readonly IVirusScanService _virusScanService;
        private readonly string _uploadsBasePath;

        /// <summary>
        /// Maximum allowed size for a single image upload (10MB).
        /// </summary>
        private const long MaxImageSizeBytes = 10 * 1024 * 1024;

        /// <summary>
        /// Maximum allowed size for a single video upload (100MB).
        /// </summary>
        private const long MaxVideoSizeBytes = 100 * 1024 * 1024;

        /// <summary>
        /// Maximum number of images allowed per upload operation.
        /// </summary>
        private const int MaxImagesPerUpload = 10;

        /// <summary>
        /// Maximum number of videos allowed per upload operation.
        /// </summary>
        private const int MaxVideosPerUpload = 5;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageUploadService"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        /// <param name="environment">Web hosting environment for determining file paths.</param>
        /// <param name="fileSignatureValidator">Validator for file magic bytes.</param>
        /// <param name="virusScanService">Service for scanning files for viruses/malware.</param>
        public ImageUploadService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<ImageUploadService> logger,
            IWebHostEnvironment environment,
            IFileSignatureValidator fileSignatureValidator,
            IVirusScanService virusScanService)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _fileSignatureValidator = fileSignatureValidator;
            _virusScanService = virusScanService;
            // SECURITY: Store uploads outside wwwroot to prevent direct access
            _uploadsBasePath = Path.Combine(environment.ContentRootPath, "AppData", "uploads", "posts");
        }

        /// <summary>
        /// Uploads images for a specific user and returns their secure URLs.
        /// Images are organized by user: /uploads/posts/{userId}/images/{filename}.
        /// Returns secure API URLs that require authentication.
        /// </summary>
        /// <param name="userId">The unique identifier of the user uploading images.</param>
        /// <param name="images">List of images with filename, base64 data, and content type.</param>
        /// <param name="progress">Optional progress reporter for upload tracking.</param>
        /// <returns>A tuple containing success status, list of image URLs, and error message if failed.</returns>
        public async Task<(bool Success, List<string> ImageUrls, string? ErrorMessage)> UploadImagesAsync(
            string userId,
            List<(string FileName, string Base64Data, string ContentType)> images,
            IProgress<int>? progress = null)
        {
            // SECURITY: Validate userId is a valid GUID to prevent path traversal attacks
            if (!Guid.TryParse(userId, out _))
            {
                return (false, new List<string>(), "Invalid user ID format");
            }

            // DOS PROTECTION: Limit number of images to prevent resource exhaustion
            if (images == null || !images.Any())
            {
                return (false, new List<string>(), "No images provided");
            }

            if (images.Count > MaxImagesPerUpload)
            {
                return (false, new List<string>(), $"Maximum {MaxImagesPerUpload} images allowed per upload");
            }

            var imageUrls = new List<string>();
            var totalImages = images.Count;

            try
            {
                // Create user-specific images directory
                var userImagesPath = Path.Combine(_uploadsBasePath, userId, "images");
                if (!Directory.Exists(userImagesPath))
                {
                    Directory.CreateDirectory(userImagesPath);
                    _logger.LogInformation("Created images directory for user {UserId}", userId);
                }

                for (int i = 0; i < images.Count; i++)
                {
                    var image = images[i];

                    // Generate unique filename and image ID
                    var fileExtension = GetFileExtension(image.ContentType);
                    var imageId = Guid.NewGuid().ToString();
                    var uniqueFileName = $"{imageId}{fileExtension}";
                    var filePath = Path.Combine(userImagesPath, uniqueFileName);

                    // Convert base64 to bytes and save
                    var base64Data = image.Base64Data;
                    if (base64Data.Contains(","))
                    {
                        base64Data = base64Data.Split(',')[1];
                    }

                    var imageBytes = Convert.FromBase64String(base64Data);

                    // DOS PROTECTION: Validate file size after decoding
                    if (imageBytes.Length > MaxImageSizeBytes)
                    {
                        _logger.LogWarning("Image {FileName} exceeds size limit for user {UserId}", image.FileName, userId);
                        return (false, new List<string>(), $"Image {image.FileName} exceeds {MaxImageSizeBytes / 1024 / 1024}MB size limit");
                    }

                    // SECURITY: Validate file signature (magic bytes) to prevent disguised malicious files
                    var signatureValidation = _fileSignatureValidator.ValidateFileSignature(imageBytes, uniqueFileName);
                    if (!signatureValidation.IsValid)
                    {
                        _logger.LogWarning("Image {FileName} failed signature validation for user {UserId}: {Error}", 
                            image.FileName, userId, signatureValidation.ErrorMessage);
                        return (false, new List<string>(), $"Image {image.FileName}: {signatureValidation.ErrorMessage}");
                    }

                    // SECURITY: Scan for viruses/malware
                    var virusScanResult = await _virusScanService.ScanAsync(imageBytes, image.FileName);
                    if (!virusScanResult.IsClean)
                    {
                        _logger.LogWarning("Image {FileName} failed virus scan for user {UserId}: {Virus}", 
                            image.FileName, userId, virusScanResult.VirusName ?? virusScanResult.ErrorMessage);
                        var reason = virusScanResult.VirusName != null 
                            ? $"Malware detected: {virusScanResult.VirusName}" 
                            : virusScanResult.ErrorMessage ?? "Virus scan failed";
                        return (false, new List<string>(), $"Image {image.FileName}: {reason}");
                    }

                    await File.WriteAllBytesAsync(filePath, imageBytes);

                    // Return secure API URL that requires authentication
                    var imageUrl = $"/api/secure-files/post-image/{userId}/{imageId}/{uniqueFileName}";
                    imageUrls.Add(imageUrl);

                    // Report progress
                    if (progress != null)
                    {
                        var percentComplete = (int)((i + 1) / (double)totalImages * 100);
                        progress.Report(percentComplete);
                    }

                    _logger.LogInformation("Uploaded image {FileName} as {UniqueFileName} for user {UserId}", 
                        image.FileName, uniqueFileName, userId);
                }

                return (true, imageUrls, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading images for user {UserId}", userId);

                // Cleanup any uploaded files on error
                foreach (var url in imageUrls)
                {
                    try
                    {
                        var fileName = Path.GetFileName(url);
                        var userImagesPath = Path.Combine(_uploadsBasePath, userId, "images");
                        var filePath = Path.Combine(userImagesPath, fileName);
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                return (false, new List<string>(), ex.Message);
            }
        }

        /// <summary>
        /// Uploads videos for a specific user and returns their secure URLs.
        /// Videos are organized by user: /uploads/posts/{userId}/videos/{filename}.
        /// Returns secure API URLs that require authentication.
        /// </summary>
        /// <param name="userId">The unique identifier of the user uploading videos.</param>
        /// <param name="videos">List of videos with filename, base64 data, and content type.</param>
        /// <param name="progress">Optional progress reporter for upload tracking.</param>
        /// <returns>A tuple containing success status, list of video URLs, and error message if failed.</returns>
        public async Task<(bool Success, List<string> VideoUrls, string? ErrorMessage)> UploadVideosAsync(
            string userId,
            List<(string FileName, string Base64Data, string ContentType)> videos,
            IProgress<int>? progress = null)
        {
            // SECURITY: Validate userId is a valid GUID to prevent path traversal attacks
            if (!Guid.TryParse(userId, out _))
            {
                return (false, new List<string>(), "Invalid user ID format");
            }

            // DOS PROTECTION: Limit number of videos to prevent resource exhaustion
            if (videos == null || !videos.Any())
            {
                return (false, new List<string>(), "No videos provided");
            }

            if (videos.Count > MaxVideosPerUpload)
            {
                return (false, new List<string>(), $"Maximum {MaxVideosPerUpload} videos allowed per upload");
            }

            var videoUrls = new List<string>();
            var totalVideos = videos.Count;

            try
            {
                // Create user-specific videos directory
                var userVideosPath = Path.Combine(_uploadsBasePath, userId, "videos");
                if (!Directory.Exists(userVideosPath))
                {
                    Directory.CreateDirectory(userVideosPath);
                    _logger.LogInformation("Created videos directory for user {UserId}", userId);
                }

                for (int i = 0; i < videos.Count; i++)
                {
                    var video = videos[i];

                    // Generate unique filename and video ID
                    var fileExtension = GetVideoFileExtension(video.ContentType);
                    var videoId = Guid.NewGuid().ToString();
                    var uniqueFileName = $"{videoId}{fileExtension}";
                    var filePath = Path.Combine(userVideosPath, uniqueFileName);

                    // Convert base64 to bytes and save
                    var base64Data = video.Base64Data;
                    if (base64Data.Contains(","))
                    {
                        base64Data = base64Data.Split(',')[1];
                    }

                    var videoBytes = Convert.FromBase64String(base64Data);

                    // DOS PROTECTION: Validate file size after decoding
                    if (videoBytes.Length > MaxVideoSizeBytes)
                    {
                        _logger.LogWarning("Video {FileName} exceeds size limit for user {UserId}", video.FileName, userId);
                        return (false, new List<string>(), $"Video {video.FileName} exceeds {MaxVideoSizeBytes / 1024 / 1024}MB size limit");
                    }

                    // SECURITY: Validate file signature (magic bytes) to prevent disguised malicious files
                    var signatureValidation = _fileSignatureValidator.ValidateFileSignature(videoBytes, uniqueFileName);
                    if (!signatureValidation.IsValid)
                    {
                        _logger.LogWarning("Video {FileName} failed signature validation for user {UserId}: {Error}", 
                            video.FileName, userId, signatureValidation.ErrorMessage);
                        return (false, new List<string>(), $"Video {video.FileName}: {signatureValidation.ErrorMessage}");
                    }

                    // SECURITY: Scan for viruses/malware
                    var virusScanResult = await _virusScanService.ScanAsync(videoBytes, video.FileName);
                    if (!virusScanResult.IsClean)
                    {
                        _logger.LogWarning("Video {FileName} failed virus scan for user {UserId}: {Virus}", 
                            video.FileName, userId, virusScanResult.VirusName ?? virusScanResult.ErrorMessage);
                        var reason = virusScanResult.VirusName != null 
                            ? $"Malware detected: {virusScanResult.VirusName}" 
                            : virusScanResult.ErrorMessage ?? "Virus scan failed";
                        return (false, new List<string>(), $"Video {video.FileName}: {reason}");
                    }

                    await File.WriteAllBytesAsync(filePath, videoBytes);

                    // Return secure API URL that requires authentication
                    var videoUrl = $"/api/secure-files/post-video/{userId}/{videoId}/{uniqueFileName}";
                    videoUrls.Add(videoUrl);

                    // Report progress
                    if (progress != null)
                    {
                        var percentComplete = (int)((i + 1) / (double)totalVideos * 100);
                        progress.Report(percentComplete);
                    }

                    _logger.LogInformation("Uploaded video {FileName} as {UniqueFileName} for user {UserId}", 
                        video.FileName, uniqueFileName, userId);
                }

                return (true, videoUrls, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading videos for user {UserId}", userId);

                // Cleanup any uploaded files on error
                foreach (var url in videoUrls)
                {
                    try
                    {
                        var fileName = Path.GetFileName(url);
                        var userVideosPath = Path.Combine(_uploadsBasePath, userId, "videos");
                        var filePath = Path.Combine(userVideosPath, fileName);
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                return (false, new List<string>(), ex.Message);
            }
        }

        /// <summary>
        /// Gets the appropriate file extension for an image content type.
        /// </summary>
        /// <param name="contentType">The MIME content type of the image.</param>
        /// <returns>The file extension including the leading dot.</returns>
        private string GetFileExtension(string contentType)
        {
            return contentType.ToLower() switch
            {
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        /// <summary>
        /// Gets the appropriate file extension for a video content type.
        /// </summary>
        /// <param name="contentType">The MIME content type of the video.</param>
        /// <returns>The file extension including the leading dot.</returns>
        private string GetVideoFileExtension(string contentType)
        {
            return contentType.ToLower() switch
            {
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "video/ogg" => ".ogg",
                "video/quicktime" => ".mov",
                _ => ".mp4"
            };
        }
    }
}
