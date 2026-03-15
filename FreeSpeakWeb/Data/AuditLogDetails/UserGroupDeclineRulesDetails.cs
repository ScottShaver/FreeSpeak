namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user decline group rules audit log entries.
    /// Tracks when users decline to accept a group's rules.
    /// </summary>
    public class UserGroupDeclineRulesDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the unique identifier of the group.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the name of the group.
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resulting action after declining.
        /// Examples: "LeftGroup", "RemainingReadOnly", "RequestCancelled".
        /// </summary>
        public string ResultingAction { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of rules that were presented.
        /// </summary>
        public int RuleCount { get; set; }
    }
}
