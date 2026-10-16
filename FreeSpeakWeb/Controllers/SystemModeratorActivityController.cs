using System.Security.Claims;
using System.Text;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FreeSpeakWeb.Controllers;

/// <summary>
/// API controller for exporting the system moderator activity log as CSV.
/// Only system moderators and administrators may export; the service enforces this.
/// </summary>
[Authorize]
[ApiController]
[Route("api/system-moderator/activity")]
[EnableRateLimiting("file-download")]
public class SystemModeratorActivityController : ControllerBase
{
    private readonly ISystemModeratorActivityService _activityService;
    private readonly ILogger<SystemModeratorActivityController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemModeratorActivityController"/> class.
    /// </summary>
    /// <param name="activityService">Service for reading and exporting moderator activity.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public SystemModeratorActivityController(
        ISystemModeratorActivityService activityService,
        ILogger<SystemModeratorActivityController> logger)
    {
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Exports the filtered moderator activity as a CSV file.
    /// </summary>
    /// <param name="moderator">Optional acting moderator user id.</param>
    /// <param name="action">Optional action category.</param>
    /// <param name="from">Optional first UTC day to include.</param>
    /// <param name="to">Optional last UTC day to include.</param>
    /// <returns>The CSV file, 401 when unauthenticated or 403 when the caller may not view the log.</returns>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? moderator,
        [FromQuery] ActionCategory? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User not authenticated.");
        }

        var filter = new ModeratorActivityFilter(
            string.IsNullOrWhiteSpace(moderator) ? null : moderator,
            action,
            from?.Date,
            to?.Date);

        var result = await _activityService.ExportCsvAsync(userId, filter);
        if (!result.IsAuthorized)
        {
            _logger.LogWarning("User {UserId} was denied the moderator activity export", userId);
            return Forbid();
        }

        Response.Headers["X-Moderation-Log-Truncated"] = result.Truncated ? "true" : "false";
        var fileName = $"moderator-activity-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(result.Csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
