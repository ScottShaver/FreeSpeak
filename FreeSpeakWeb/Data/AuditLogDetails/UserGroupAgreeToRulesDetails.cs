namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user agree to group rules audit log entries.
    /// Tracks when users agree to a group's rules to participate.
    /// </summary>
    public class UserGroupAgreeToRulesDetails : AuditLogDetailsBase
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
        /// Gets or sets the number of rules the user agreed to.
        /// </summary>
        public int RuleCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the user's first time agreeing to rules.
        /// </summary>
        public bool IsFirstAgreement { get; set; }
    }
}
