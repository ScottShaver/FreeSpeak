using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace FreeSpeakWeb.Services;

public class ProfilePictureService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ProfilePictureService> _logger;
    private const int TargetSize = 168;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    public ProfilePictureService(IWebHostEnvironment environment, ILogger<ProfilePictureService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage, string? RelativeUrl)> SaveProfilePictureAsync(
        Stream imageStream, 
        string userId)
    {
        try
        {
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

            var relativeUrl = $"/api/secure-files/profile-picture/{userId}";
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
