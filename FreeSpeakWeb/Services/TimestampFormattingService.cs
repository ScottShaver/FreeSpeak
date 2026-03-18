using Microsoft.Extensions.Localization;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Service for formatting timestamps into human-readable relative time strings with localization support.
/// </summary>
public class TimestampFormattingService
{
    private readonly IStringLocalizer<Resources.Shared.TimeFormatting> _localizer;

    public TimestampFormattingService(IStringLocalizer<Resources.Shared.TimeFormatting> localizer)
    {
        _localizer = localizer;
    }

    /// <summary>
    /// Formats a timestamp as a relative time string (e.g., "5m ago", "2h ago").
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>A localized human-readable relative time string.</returns>
    public string FormatTimeAgo(DateTime timestamp)
    {
        var now = DateTime.UtcNow;
        var utcTimestamp = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();
        var timeSpan = now - utcTimestamp;

        if (timeSpan.TotalMinutes < 1)
            return _localizer["JustNow"];
        if (timeSpan.TotalMinutes < 60)
            return string.Format(_localizer["MinutesAgo"], (int)timeSpan.TotalMinutes);
        if (timeSpan.TotalHours < 24)
            return string.Format(_localizer["HoursAgo"], (int)timeSpan.TotalHours);
        if (timeSpan.TotalDays < 7)
            return string.Format(_localizer["DaysAgo"], (int)timeSpan.TotalDays);
        if (timeSpan.TotalDays < 30)
            return string.Format(_localizer["WeeksAgo"], (int)(timeSpan.TotalDays / 7));
        if (timeSpan.TotalDays < 365)
            return string.Format(_localizer["MonthsAgo"], (int)(timeSpan.TotalDays / 30));

        return timestamp.ToString("MMM d, yyyy");
    }

    /// <summary>
    /// Formats an expiration timestamp as a relative time until expiration string.
    /// </summary>
    /// <param name="expiresAt">The expiration timestamp.</param>
    /// <returns>A localized human-readable expiration time string.</returns>
    public string FormatExpirationTime(DateTime expiresAt)
    {
        var timeUntilExpiry = expiresAt - DateTime.UtcNow;

        if (timeUntilExpiry.TotalMinutes <= 0)
            return _localizer["ExpiresSoon"];
        if (timeUntilExpiry.TotalMinutes < 60)
            return string.Format(_localizer["ExpiresInMinutes"], (int)timeUntilExpiry.TotalMinutes);
        if (timeUntilExpiry.TotalHours < 24)
            return string.Format(_localizer["ExpiresInHours"], (int)timeUntilExpiry.TotalHours);
        if (timeUntilExpiry.TotalDays < 7)
            return string.Format(_localizer["ExpiresInDays"], (int)timeUntilExpiry.TotalDays);
        if (timeUntilExpiry.TotalDays < 30)
            return string.Format(_localizer["ExpiresInWeeks"], (int)(timeUntilExpiry.TotalDays / 7));

        return string.Format(_localizer["ExpiresOn"], expiresAt.ToString("MMM d"));
    }
}
