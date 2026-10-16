using System.ComponentModel.DataAnnotations;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// The kind of moderation action recorded in a group's moderation log.
    /// Values are persisted as integers; never renumber existing values.
    /// </summary>
    public enum GroupModerationAction
    {
        /// <summary>A pending post was approved.</summary>
        PostApproved = 0,
        /// <summary>A pending post was declined.</summary>
        PostDeclined = 1,
        /// <summary>A post was removed by a moderator.</summary>
        PostRemoved = 2,
        /// <summary>A comment was removed by a moderator.</summary>
        CommentRemoved = 3,
        /// <summary>A pending file was approved.</summary>
        FileApproved = 4,
        /// <summary>A pending file was declined.</summary>
        FileDeclined = 5,
        /// <summary>A file was removed by a moderator.</summary>
        FileRemoved = 6,
        /// <summary>A content report was dismissed.</summary>
        ReportDismissed = 7,
        /// <summary>A content report was resolved or had action taken.</summary>
        ReportResolved = 8,
        /// <summary>A member was banned.</summary>
        MemberBanned = 9,
        /// <summary>A member was unbanned.</summary>
        MemberUnbanned = 10,
        /// <summary>A member was removed.</summary>
        MemberRemoved = 11,
        /// <summary>A member's role was changed.</summary>
        MemberRoleChanged = 12,
        /// <summary>A join request was approved.</summary>
        JoinRequestApproved = 13,
        /// <summary>A join request was denied.</summary>
        JoinRequestDenied = 14,
        /// <summary>Group settings were changed.</summary>
        GroupSettingsChanged = 15,
        /// <summary>A group rule was added.</summary>
        RuleAdded = 16,
        /// <summary>A group rule was edited.</summary>
        RuleEdited = 17,
        /// <summary>A group rule was deleted.</summary>
        RuleDeleted = 18
    }

    /// <summary>
    /// The type of entity a moderation action was applied to.
    /// </summary>
    public enum GroupModerationTargetType
    {
        /// <summary>A group post.</summary>
        Post = 0,
        /// <summary>A comment on a group post.</summary>
        Comment = 1,
        /// <summary>A group file.</summary>
        File = 2,
        /// <summary>A group member.</summary>
        Member = 3,
        /// <summary>A content report.</summary>
        Report = 4,
        /// <summary>The group itself.</summary>
        Group = 5
    }

    /// <summary>
    /// A single, read-only record of a moderation action taken in a group.
    /// Actor and target users are stored as snapshots without foreign keys so history survives user deletion.
    /// </summary>
    public class GroupModerationLogEntry
    {
        /// <summary>Maximum length of user id columns.</summary>
        public const int MaxUserIdLength = 450;

        /// <summary>Maximum length of display name snapshot columns.</summary>
        public const int MaxDisplayNameLength = 200;

        /// <summary>Maximum length of <see cref="Summary"/>.</summary>
        public const int MaxSummaryLength = 500;

        /// <summary>Maximum length of <see cref="Reason"/>.</summary>
        public const int MaxReasonLength = 1000;

        /// <summary>Gets or sets the primary key.</summary>
        public long Id { get; set; }

        /// <summary>Gets or sets the group the action happened in.</summary>
        public int GroupId { get; set; }

        /// <summary>Gets or sets the group navigation property.</summary>
        public Group? Group { get; set; }

        /// <summary>Gets or sets the id of the moderator or admin who acted.</summary>
        [Required]
        [MaxLength(MaxUserIdLength)]
        public string ActorUserId { get; set; } = string.Empty;

        /// <summary>Gets or sets the actor display name at the time of the action.</summary>
        [Required]
        [MaxLength(MaxDisplayNameLength)]
        public string ActorDisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the action taken.</summary>
        public GroupModerationAction Action { get; set; }

        /// <summary>Gets or sets the type of the target entity.</summary>
        public GroupModerationTargetType TargetType { get; set; }

        /// <summary>Gets or sets the id of the target entity.</summary>
        [MaxLength(MaxUserIdLength)]
        public string? TargetId { get; set; }

        /// <summary>Gets or sets the affected member or content author.</summary>
        [MaxLength(MaxUserIdLength)]
        public string? TargetUserId { get; set; }

        /// <summary>Gets or sets the target user's display name at the time of the action.</summary>
        [MaxLength(MaxDisplayNameLength)]
        public string? TargetUserDisplayName { get; set; }

        /// <summary>Gets or sets a short plain-text excerpt or file name.</summary>
        [MaxLength(MaxSummaryLength)]
        public string? Summary { get; set; }

        /// <summary>Gets or sets the moderator-entered reason or notes.</summary>
        [MaxLength(MaxReasonLength)]
        public string? Reason { get; set; }

        /// <summary>Gets or sets the UTC time the action succeeded. Always set by the server.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Gets or sets whether the action was taken at system level by a system moderator or administrator acting as a backstop for the group.</summary>
        public bool IsSystemAction { get; set; }
    }
}
