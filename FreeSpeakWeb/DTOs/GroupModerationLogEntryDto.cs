using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.DTOs
{
    /// <summary>
    /// Read-only view of a group moderation log entry for the UI.
    /// </summary>
    /// <param name="Id">Entry id.</param>
    /// <param name="GroupId">Group id.</param>
    /// <param name="ActorUserId">Acting moderator id.</param>
    /// <param name="ActorDisplayName">Acting moderator display name snapshot.</param>
    /// <param name="Action">Action taken.</param>
    /// <param name="TargetType">Type of target.</param>
    /// <param name="TargetId">Target entity id.</param>
    /// <param name="TargetUserId">Affected user id.</param>
    /// <param name="TargetUserDisplayName">Affected user display name snapshot.</param>
    /// <param name="Summary">Short content excerpt or file name.</param>
    /// <param name="Reason">Moderator-entered reason.</param>
    /// <param name="CreatedAtUtc">Server-set UTC timestamp.</param>
    /// <param name="IsSystemAction">True when the action was taken at system level by a system moderator or administrator.</param>
    public sealed record GroupModerationLogEntryDto(
        long Id,
        int GroupId,
        string ActorUserId,
        string ActorDisplayName,
        GroupModerationAction Action,
        GroupModerationTargetType TargetType,
        string? TargetId,
        string? TargetUserId,
        string? TargetUserDisplayName,
        string? Summary,
        string? Reason,
        DateTime CreatedAtUtc,
        bool IsSystemAction = false);

    /// <summary>
    /// Optional filters applied when reading or exporting a group's moderation log.
    /// </summary>
    public sealed class GroupModerationLogFilter
    {
        /// <summary>Gets or sets the action to match, or null for all actions.</summary>
        public GroupModerationAction? Action { get; set; }

        /// <summary>Gets or sets the acting user id to match, or null for all actors.</summary>
        public string? ActorUserId { get; set; }

        /// <summary>Gets or sets the inclusive lower bound (UTC), or null.</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Gets or sets the inclusive upper bound (UTC), or null.</summary>
        public DateTime? ToDate { get; set; }
    }

    /// <summary>
    /// A page of moderation log entries.
    /// </summary>
    /// <param name="Items">The entries, newest first.</param>
    /// <param name="TotalCount">Total entries matching the filter.</param>
    /// <param name="IsAuthorized">False when the requester may not view the log.</param>
    public sealed record GroupModerationLogPage(
        IReadOnlyList<GroupModerationLogEntryDto> Items,
        int TotalCount,
        bool IsAuthorized);

    /// <summary>
    /// Input for recording a moderation action. Timestamps are always set by the server.
    /// </summary>
    public sealed class GroupModerationLogRequest
    {
        /// <summary>Gets or sets the group id.</summary>
        public int GroupId { get; set; }

        /// <summary>Gets or sets the acting user id.</summary>
        public string ActorUserId { get; set; } = string.Empty;

        /// <summary>Gets or sets the action taken.</summary>
        public GroupModerationAction Action { get; set; }

        /// <summary>Gets or sets the target type.</summary>
        public GroupModerationTargetType TargetType { get; set; }

        /// <summary>Gets or sets the target entity id.</summary>
        public string? TargetId { get; set; }

        /// <summary>Gets or sets the affected user id.</summary>
        public string? TargetUserId { get; set; }

        /// <summary>Gets or sets a short excerpt or file name.</summary>
        public string? Summary { get; set; }

        /// <summary>Gets or sets the moderator-entered reason.</summary>
        public string? Reason { get; set; }

        /// <summary>Gets or sets whether the action is a system-level action by a system moderator or administrator.</summary>
        public bool IsSystemAction { get; set; }
    }
}
