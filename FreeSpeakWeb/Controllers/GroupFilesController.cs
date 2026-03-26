using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FreeSpeakWeb.Services.Abstractions;
using System.Security.Claims;

namespace FreeSpeakWeb.Controllers;

/// <summary>
/// API controller for managing group file downloads.
/// Provides secure file download with authentication and authorization checks.
/// </summary>
[Authorize]
[ApiController]
[Route("api/group-files")]
[EnableRateLimiting("file-download")]
public class GroupFilesController : ControllerBase
{
    private readonly IGroupFileService _groupFileService;
    private readonly ILogger<GroupFilesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupFilesController"/> class.
    /// </summary>
    /// <param name="groupFileService">Service for group file operations.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public GroupFilesController(
        IGroupFileService groupFileService,
        ILogger<GroupFilesController> logger)
    {
        _groupFileService = groupFileService;
        _logger = logger;
    }

    /// <summary>
    /// Downloads a group file. Requires group membership and file access permissions.
    /// </summary>
    /// <param name="fileId">The ID of the file to download.</param>
    /// <returns>The file stream with appropriate content type and disposition headers.</returns>
    [HttpGet("{fileId:int}/download")]
    public async Task<IActionResult> DownloadFile(int fileId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User not authenticated.");
            }

            var result = await _groupFileService.GetFileForDownloadAsync(fileId, userId);

            if (!result.Success)
            {
                _logger.LogWarning("File download failed for user {UserId}, file {FileId}: {Error}",
                    userId, fileId, result.ErrorMessage);
                return NotFound(result.ErrorMessage);
            }

            if (result.FileStream == null)
            {
                _logger.LogError("File stream is null for file {FileId}", fileId);
                return NotFound("File not found.");
            }

            // Set headers for download
            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{result.FileName}\"");
            Response.Headers.Append("X-Content-Type-Options", "nosniff");

            return File(result.FileStream, result.ContentType ?? "application/octet-stream", result.FileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileId}", fileId);
            return StatusCode(500, "An error occurred while downloading the file.");
        }
    }
}
