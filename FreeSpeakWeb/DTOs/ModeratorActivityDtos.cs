using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.DTOs
{
    /// <summary>
    /// Filters for the system moderator activity log. Dates are UTC calendar days and both ends are inclusive.
    /// </summary>
    /// <param name="ModeratorUserId">Restrict to one acting moderator, or null for all.</param>
    /// <param name="Action">Restrict to one action category, or null for all moderator actions.</param>
    /// <param name="FromDateUtc">First UTC day to include, or null.</param>
    /// <param name="ToDateUtc">Last UTC day to include, or null.</param>
    public sealed record ModeratorActivityFilter(
        string? ModeratorUserId = null,
        ActionCategory? Action = null,
        DateTime? FromDateUtc = null,
        DateTime? ToDateUtc = null);

    /// <summary>
    /// One row of the system moderator activity log.
    /// </summary>
    /// <param name="Id">The audit entry id.</param>
    /// <param name="TimestampUtc">When the action happened (UTC).</param>
    /// <param name="ModeratorUserId">The acting moderator's user id.</param>
    /// <param name="ModeratorName">The acting moderator's display name.</param>
    /// <param name="Action">The action category.</param>
    /// <param name="TargetUserId">The user the action concerned, when known.</param>
    /// <param name="TargetUserName">The display name of the target user, when known.</param>
    /// <param name="Detail">A short plain-text detail such as the reason, decision or content summary.</param>
    /// <param name="GroupId">The group involved, for group content actions.</param>
    /// <param name="GroupName">The group name, for group content actions.</param>
    public sealed record ModeratorActivityEntry(
        long Id,
        DateTime TimestampUtc,
        string ModeratorUserId,
        string ModeratorName,
        ActionCategory Action,
        string? TargetUserId,
        string? TargetUserName,
        string? Detail,
        int? GroupId,
        string? GroupName);

    /// <summary>
    /// A page of the moderator activity log.
    /// </summary>
    /// <param name="IsAuthorized">False when the requester may not view the log.</param>
    /// <param name="Items">The entries on this page, newest first.</param>
    /// <param name="TotalCount">The total number of entries matching the filter.</param>
    public sealed record ModeratorActivityPage(bool IsAuthorized, IReadOnlyList<ModeratorActivityEntry> Items, int TotalCount);

    /// <summary>
    /// A moderator who appears in the activity log, for the filter drop-down.
    /// </summary>
    /// <param name="UserId">The user id.</param>
    /// <param name="DisplayName">The display name.</param>
    public sealed record ModeratorOption(string UserId, string DisplayName);

    /// <summary>
    /// The result of a CSV export of the moderator activity log.
    /// </summary>
    /// <param name="IsAuthorized">False when the requester may not export the log.</param>
    /// <param name="Csv">The CSV text.</param>
    /// <param name="RowCount">Number of data rows exported.</param>
    /// <param name="Truncated">True when more rows matched than the export cap allows.</param>
    public sealed record ModeratorActivityExportResult(bool IsAuthorized, string Csv, int RowCount, bool Truncated);
}
