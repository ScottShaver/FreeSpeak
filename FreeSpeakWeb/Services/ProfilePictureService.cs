using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Service for managing user profile pictures including upload, retrieval, and deletion.
/// Handles image resizing, format conversion, and secure storage outside wwwroot.
/// </summary>
public class ProfilePictureService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ProfilePictureService> _logger;

    /// <summary>
    /// Target size in pixels for profile picture dimensions (square).
    /// </summary>
    private const int TargetSize = 168;

    /// <summary>
    /// Maximum allowed file size for profile picture uploads (5MB).
    /// </summary>
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfilePictureService"/> class.
    /// </summary>
    /// <param name="environment">The web host environment for accessing content paths.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public ProfilePictureService(IWebHostEnvironment environment, ILogger<ProfilePictureService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Saves a profile picture for a user, resizing it to the target dimensions.
    /// The image is cropped to a square and saved as JPEG format.
    /// </summary>
    /// <param name="imageStream">The stream containing the image data.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>
    /// A tuple containing:
    /// - Success: Whether the operation completed successfully.
    /// - ErrorMessage: Error description if the operation failed, null otherwise.
    /// - RelativeUrl: The secure API URL for accessing the profile picture if successful.
    /// </returns>
    public async Task<(bool Success, string? ErrorMessage, string? RelativeUrl)> SaveProfilePictureAsync(
        Stream imageStream, 
        string userId)
    {
        try
        {
            // Validate userId length to ensure generated URL won't exceed database limit
            if (string.IsNullOrWhiteSpace(userId))
            {
                return (false, "User ID is required.", null);
            }

            // The generated URL format is: /api/secure-files/profile-picture/{userId}
            // Ensure total URL length doesn't exceed 75 characters (database constraint)
            var relativeUrl = $"/api/secure-files/profile-picture/{userId}";
            if (relativeUrl.Length > 75)
            {
                _logger.LogError("Generated profile picture URL exceeds 75 character limit for user {UserId}. URL: {Url}", userId, relativeUrl);
                return (false, "Unable to generate profile picture URL. User ID is too long.", null);
            }

            // Check file size
            if (imageStream.Length > MaxFileSizeBytes)
            {
                return (false, "Image file size must not exceed 5MB.", null);
            }

            // Ensure the profiles directory exists
            // SECURITY: Store outside wwwroot to prevent direct access
            var profilesPath = Path.Combine(_environment.ContentRootPath, "AppData", "images", "profiles");
            Directory.CreateDirectory(profilesPath);

            // Buffer the stream to prevent Blazor Server SignalR timeout issues
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // Load and process the image
            using var image = await Image.LoadAsync(memoryStream);

            // Resize to 168x168
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(TargetSize, TargetSize),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center
            }));

            // Save as JPEG
            var fileName = $"{userId}.jpg";
            var filePath = Path.Combine(profilesPath, fileName);

            await image.SaveAsJpegAsync(filePath, new JpegEncoder
            {
                Quality = 85
            });

            _logger.LogInformation("Profile picture saved for user {UserId}: {FilePath}", userId, relativeUrl);

            return (true, null, relativeUrl);
        }
        catch (UnknownImageFormatException)
        {
            return (false, "Invalid image format. Please upload a valid image file.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving profile picture for user {UserId}", userId);
            return (false, "An error occurred while processing the image.", null);
        }
    }

    /// <summary>
    /// Retrieves the profile picture for a user as a byte array.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>The profile picture as a byte array, or null if not found or an error occurs.</returns>
    public async Task<byte[]?> GetProfilePictureAsync(string userId)
    {
        try
        {
            // SECURITY: Read from secure directory outside wwwroot
            var profilesPath = Path.Combine(_environment.ContentRootPath, "AppData", "images", "profiles");
            var filePath = Path.Combine(profilesPath, $"{userId}.jpg");

            if (File.Exists(filePath))
            {
                return await File.ReadAllBytesAsync(filePath);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading profile picture for user {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Checks whether a profile picture exists for the specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>True if the profile picture exists, false otherwise.</returns>
    public bool ProfilePictureExists(string userId)
    {
        try
        {
            // SECURITY: Check in secure directory outside wwwroot
            var profilesPath = Path.Combine(_environment.ContentRootPath, "AppData", "images", "profiles");
            var filePath = Path.Combine(profilesPath, $"{userId}.jpg");
            return File.Exists(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking profile picture existence for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Deletes the profile picture for the specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    public void DeleteProfilePicture(string userId)
    {
        try
        {
            // SECURITY: Delete from secure directory outside wwwroot
            var profilesPath = Path.Combine(_environment.ContentRootPath, "AppData", "images", "profiles");
            var filePath = Path.Combine(profilesPath, $"{userId}.jpg");

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Profile picture deleted for user {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting profile picture for user {UserId}", userId);
        }
    }
}
