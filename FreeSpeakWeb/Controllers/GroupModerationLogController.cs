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
/// API controller for exporting a group's moderation log as CSV.
/// Only group admins, group moderators and system administrators may export; the service enforces this.
/// </summary>
[Authorize]
[ApiController]
[Route("api/groups/{groupId:int}/moderation-log")]
[EnableRateLimiting("file-download")]
public class GroupModerationLogController : ControllerBase
{
    private readonly IGroupModerationLogService _moderationLogService;
    private readonly ILogger<GroupModerationLogController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupModerationLogController"/> class.
    /// </summary>
    /// <param name="moderationLogService">Service for reading and exporting the moderation log.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public GroupModerationLogController(
        IGroupModerationLogService moderationLogService,
        ILogger<GroupModerationLogController> logger)
    {
        _moderationLogService = moderationLogService;
        _logger = logger;
    }

    /// <summary>
    /// Exports the filtered moderation log of a group as a CSV file.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="action">Optional action filter.</param>
    /// <param name="from">Optional inclusive UTC lower bound.</param>
    /// <param name="to">Optional inclusive UTC upper bound.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The CSV file, 401 when unauthenticated or 403 when the caller may not view the log.</returns>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        int groupId,
        [FromQuery] GroupModerationAction? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User not authenticated.");
        }

        var filter = new GroupModerationLogFilter
        {
            Action = action,
            FromDate = from?.ToUniversalTime(),
            ToDate = to?.ToUniversalTime()
        };

        var result = await _moderationLogService.ExportCsvAsync(groupId, userId, filter, cancellationToken);
        if (!result.IsAuthorized)
        {
            _logger.LogWarning("User {UserId} was denied the moderation log export for group {GroupId}", userId, groupId);
            return Forbid();
        }

        Response.Headers["X-Moderation-Log-Truncated"] = result.Truncated ? "true" : "false";
        var fileName = $"group-{groupId}-moderation-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(result.Csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
