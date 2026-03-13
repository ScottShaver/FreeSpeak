using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace FreeSpeakWeb.Controllers;

/// <summary>
/// API controller for serving secure files (images and videos) with authentication,
/// authorization, rate limiting, and path traversal protection.
/// Files are stored outside wwwroot in the AppData directory for security.
/// </summary>
[Authorize]
[ApiController]
[Route("api/secure-files")]
[EnableRateLimiting("file-download")]
public class SecureFileController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SecureFileController> _logger;
    private readonly ProfilePictureService _profilePictureService;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly FriendsService _friendsService;
    private readonly ImageResizingService _imageResizingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureFileController"/> class.
    /// </summary>
    /// <param name="environment">The web host environment for accessing content paths.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    /// <param name="profilePictureService">Service for managing profile pictures.</param>
    /// <param name="contextFactory">Factory for creating database contexts.</param>
    /// <param name="friendsService">Service for checking friendship status.</param>
    /// <param name="imageResizingService">Service for resizing images.</param>
    public SecureFileController(
        IWebHostEnvironment environment,
        ILogger<SecureFileController> logger,
        ProfilePictureService profilePictureService,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        FriendsService friendsService,
        ImageResizingService imageResizingService)
    {
        _environment = environment;
        _logger = logger;
        _profilePictureService = profilePictureService;
        _contextFactory = contextFactory;
        _friendsService = friendsService;
        _imageResizingService = imageResizingService;
    }

    /// <summary>
    /// Serves profile pictures with optional resizing. Profile pictures are public and
    /// can be accessed without authentication for use on the public home page.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose profile picture to retrieve.</param>
    /// <param name="size">Image size: "thumbnail" (150px), "medium" (400px), or "full" (default: thumbnail).</param>
    /// <returns>The profile picture as a JPEG image, or NotFound if not available.</returns>
    [AllowAnonymous] // Profile pictures are public - needed for public home page
    [HttpGet("profile-picture/{userId}")]
    public async Task<IActionResult> GetProfilePicture(string userId, [FromQuery] string? size = null)
    {
        try
        {
            // SECURITY: Validate userId is a valid GUID
            if (!IsValidUserId(userId))
            {
                return BadRequest("Invalid user ID format");
            }

            // Parse size parameter (default to thumbnail for performance)
            var imageSize = ParseImageSize(size, ImageSize.Thumbnail);

            // Get the original profile picture path
            var profilesPath = Path.Combine(_environment.ContentRootPath, "AppData", "images", "profiles");
            var originalPath = Path.Combine(profilesPath, $"{userId}.jpg");

            if (!System.IO.File.Exists(originalPath))
            {
                return NotFound();
            }

            // Get resized image (or full if requested)
            var imageBytes = await _imageResizingService.GetResizedImageAsync(originalPath, imageSize);

            if (imageBytes == null)
            {
                return NotFound();
            }

            // Set secure headers with longer cache for thumbnails
            var maxAge = imageSize == ImageSize.Thumbnail ? 7200 : 3600; // 2 hours for thumbnails, 1 hour for others
            Response.Headers.Append("Cache-Control", $"private, max-age={maxAge}");
            Response.Headers.Append("X-Content-Type-Options", "nosniff");

            return File(imageBytes, "image/jpeg", enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetProfilePicture for user {UserId}: {Message}", userId, ex.Message);
            return StatusCode(500, "An error occurred while retrieving the profile picture");
        }
    }


    /// <summary>
    /// Serves post images with authentication and authorization.
    /// Public posts can be accessed without authentication; other posts require
    /// the requesting user to be the author or a friend (for FriendsOnly posts).
    /// </summary>
    /// <param name="userId">The unique identifier of the post author.</param>
    /// <param name="imageId">The unique identifier of the image.</param>
    /// <param name="filename">The filename of the image.</param>
    /// <param name="size">Image size: "thumbnail" (150px), "medium" (400px), or "full" (default: thumbnail).</param>
    /// <returns>The post image with appropriate content type, or an error status.</returns>
    [AllowAnonymous] // Allow access to public post images
    [HttpGet("post-image/{userId}/{imageId}/{filename}")]
    public async Task<IActionResult> GetPostImage(string userId, string imageId, string filename, [FromQuery] string? size = null)
    {
        try
        {
            // SECURITY: Get requesting user ID (may be null for anonymous users)
            var requestingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // SECURITY: Validate all inputs
            if (!IsValidUserId(userId))
            {
                _logger.LogWarning("Invalid userId format: {UserId}", userId);
                return BadRequest("Invalid user ID");
            }

            if (!Guid.TryParse(imageId, out _))
            {
                _logger.LogWarning("Invalid imageId format: {ImageId}", imageId);
                return BadRequest("Invalid image ID");
            }

            if (!IsValidFileName(filename))
            {
                _logger.LogWarning("Invalid filename format: {Filename}", filename);
                return BadRequest("Invalid filename");
            }

            // Parse size parameter (default to thumbnail for feed performance)
            var imageSize = ParseImageSize(size, ImageSize.Thumbnail);

            // SECURITY: Find the post image in database to verify permissions
            using var context = await _contextFactory.CreateDbContextAsync();
            var postImage = await context.PostImages
                .Include(pi => pi.Post)
                .ThenInclude(p => p.Author)
                .FirstOrDefaultAsync(pi => pi.ImageUrl.Contains(imageId));

            if (postImage == null)
            {
                _logger.LogWarning("Post image not found for imageId: {ImageId}", imageId);
                return NotFound();
            }

            // SECURITY: Check if user has permission to view this post
            // Public posts can be viewed by anyone (including anonymous users)
            // Other posts require authentication and appropriate permissions
            if (!await CanUserViewPostAsync(postImage.Post, requestingUserId))
            {
                _logger.LogWarning("User {RequestingUserId} denied access to post {PostId} (AudienceType: {AudienceType}) image {ImageId}", 
                    requestingUserId ?? "anonymous", postImage.PostId, postImage.Post.AudienceType, imageId);
                return Forbid();
            }

            // Construct the file path (now outside wwwroot)
            var filePath = Path.Combine(
                _environment.ContentRootPath,
                "AppData",
                "uploads",
                "posts",
                userId,
                "images",
                filename
            );

            // SECURITY: Verify path is within allowed directory
            var allowedDirectory = Path.Combine(_environment.ContentRootPath, "AppData", "uploads", "posts");
            if (!IsPathWithinAllowedDirectory(filePath, allowedDirectory))
            {
                _logger.LogWarning("Path traversal attempt detected: {FilePath}", filePath);
                return BadRequest("Invalid path");
            }

            // Ensure the file exists
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("Post image file not found: {ImageId}", imageId);
                return NotFound();
            }

            // Get resized image (or full if requested)
            var imageBytes = await _imageResizingService.GetResizedImageAsync(filePath, imageSize);

            if (imageBytes == null)
            {
                return NotFound();
            }

            // Determine content type
            var contentType = GetContentType(filename);

            // Set secure headers with longer cache for thumbnails
            var maxAge = imageSize == ImageSize.Thumbnail ? 7200 : 3600;
            Response.Headers.Append("Cache-Control", $"private, max-age={maxAge}");
            Response.Headers.Append("X-Content-Type-Options", "nosniff");

            // Return the file
            return File(imageBytes, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving post image for user {UserId}, imageId {ImageId}", userId, imageId);
            return StatusCode(500, "An error occurred while retrieving the image");
        }
    }

    /// <summary>
    /// Serves post videos with authentication and authorization.
    /// Currently returns NotFound as video entity support is not yet implemented.
    /// </summary>
    /// <param name="userId">The unique identifier of the post author.</param>
    /// <param name="videoId">The unique identifier of the video.</param>
    /// <param name="filename">The filename of the video.</param>
    /// <returns>The post video with appropriate content type, or NotFound until video support is implemented.</returns>
    [HttpGet("post-video/{userId}/{videoId}/{filename}")]
    public async Task<IActionResult> GetPostVideo(string userId, string videoId, string filename)
    {
        try
        {
            // SECURITY: Get requesting user ID
            var requestingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(requestingUserId))
            {
                _logger.LogWarning("Unauthorized access attempt to post video");
                return Unauthorized();
            }

            // SECURITY: Validate all inputs
            if (!IsValidUserId(userId))
            {
                _logger.LogWarning("Invalid userId format: {UserId}", userId);
                return BadRequest("Invalid user ID");
            }

            if (!Guid.TryParse(videoId, out _))
            {
                _logger.LogWarning("Invalid videoId format: {VideoId}", videoId);
                return BadRequest("Invalid video ID");
            }

            if (!IsValidFileName(filename))
            {
                _logger.LogWarning("Invalid filename format: {Filename}", filename);
                return BadRequest("Invalid filename");
            }

            // SECURITY: Find the post video in database to verify permissions
            // Note: You'll need to add a PostVideo entity similar to PostImage
            // For now, we'll use a similar approach with PostImage
            using var context = await _contextFactory.CreateDbContextAsync();

            // TODO: If you add video support, create PostVideo entity and check permissions
            // For now, this prevents access until proper video entities are implemented
            _logger.LogWarning("Video endpoint called but video entity support not yet implemented");
            return NotFound("Video support not yet implemented");

            // When implemented, follow same pattern as GetPostImage above
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving post video for user {UserId}, videoId {VideoId}", userId, videoId);
            return StatusCode(500, "An error occurred while retrieving the video");
        }
    }

    /// <summary>
    /// Gets the MIME content type for an image file based on its extension.
    /// </summary>
    /// <param name="filename">The filename to determine content type for.</param>
    /// <returns>The MIME content type string.</returns>
    private static string GetContentType(string filename)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Gets the MIME content type for a video file based on its extension.
    /// </summary>
    /// <param name="filename">The filename to determine content type for.</param>
    /// <returns>The MIME content type string.</returns>
    private static string GetVideoContentType(string filename)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        return extension switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".ogg" => "video/ogg",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Parses the size query parameter into an ImageSize enum value.
    /// Supports aliases like "thumb" for "thumbnail" and "med" for "medium".
    /// </summary>
    /// <param name="sizeParam">The size parameter from the query string.</param>
    /// <param name="defaultSize">The default size to use if the parameter is null or invalid.</param>
    /// <returns>The parsed ImageSize value.</returns>
    private static ImageSize ParseImageSize(string? sizeParam, ImageSize defaultSize)
    {
        if (string.IsNullOrWhiteSpace(sizeParam))
            return defaultSize;

        return sizeParam.ToLowerInvariant() switch
        {
            "thumbnail" or "thumb" or "small" => ImageSize.Thumbnail,
            "medium" or "med" => ImageSize.Medium,
            "full" or "large" or "original" => ImageSize.Full,
            _ => defaultSize
        };
    }

    #region Security Helper Methods

    /// <summary>
    /// Validates that a filename contains only safe characters to prevent path traversal attacks.
    /// Only allows alphanumeric characters, dash, underscore, and a single dot for the extension.
    /// </summary>
    /// <param name="filename">The filename to validate.</param>
    /// <returns>True if the filename is valid and safe, false otherwise.</returns>
    private static bool IsValidFileName(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename) || filename.Length > 255)
            return false;

        // Only allow alphanumeric, dash, underscore, and single dot for extension
        var allowedPattern = @"^[a-zA-Z0-9\-_]+\.[a-zA-Z0-9]+$";
        return Regex.IsMatch(filename, allowedPattern);
    }

    /// <summary>
    /// Validates that a userId is a valid GUID format to prevent injection attacks.
    /// </summary>
    /// <param name="userId">The user ID to validate.</param>
    /// <returns>True if the user ID is a valid GUID, false otherwise.</returns>
    private static bool IsValidUserId(string userId)
    {
        return Guid.TryParse(userId, out _);
    }

    /// <summary>
    /// Validates that the full path is within the allowed directory to prevent path traversal attacks.
    /// Uses normalized paths for comparison to handle relative path components.
    /// </summary>
    /// <param name="fullPath">The full file path to validate.</param>
    /// <param name="allowedDirectory">The allowed directory that must contain the path.</param>
    /// <returns>True if the path is within the allowed directory, false otherwise.</returns>
    private static bool IsPathWithinAllowedDirectory(string fullPath, string allowedDirectory)
    {
        try
        {
            var fullPathNormalized = Path.GetFullPath(fullPath);
            var allowedDirNormalized = Path.GetFullPath(allowedDirectory);
            return fullPathNormalized.StartsWith(allowedDirNormalized, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the requesting user has permission to view a post based on its audience type.
    /// Public posts are always accessible. MeOnly posts are only for the author.
    /// FriendsOnly posts require an active friendship with the author.
    /// </summary>
    /// <param name="post">The post to check access for.</param>
    /// <param name="requestingUserId">The ID of the user requesting access, or null for anonymous users.</param>
    /// <returns>True if the user has permission to view the post, false otherwise.</returns>
    private async Task<bool> CanUserViewPostAsync(Post post, string? requestingUserId)
    {
        // Public posts - always allowed for everyone (including anonymous users)
        if (post.AudienceType == AudienceType.Public)
            return true;

        // No requesting user means unauthorized for non-public posts
        if (string.IsNullOrEmpty(requestingUserId))
            return false;

        // Post is by the requesting user - always allowed
        if (post.AuthorId == requestingUserId)
            return true;

        // MeOnly posts - only the author
        if (post.AudienceType == AudienceType.MeOnly)
            return false;

        // FriendsOnly posts - check friendship
        if (post.AudienceType == AudienceType.FriendsOnly)
        {
            var areFriends = await _friendsService.AreFriendsAsync(post.AuthorId, requestingUserId);
            return areFriends;
        }

        return false;
    }

    #endregion
}
