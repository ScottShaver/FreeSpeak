namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the status of a content report submitted by a user.
    /// Used to track the lifecycle of reports from submission through resolution.
    /// </summary>
    public enum ReportStatus
    {
        /// <summary>
        /// The report has been submitted but not yet reviewed by a moderator or administrator.
        /// This is the initial status for all new reports.
        /// </summary>
        NotReviewed = 0,

        /// <summary>
        /// The report is currently being reviewed by a moderator or administrator.
        /// </summary>
        UnderReview = 1,

        /// <summary>
        /// The report was reviewed and the content was found to violate rules.
        /// Appropriate action has been or will be taken against the content.
        /// </summary>
        ActionTaken = 2,

        /// <summary>
        /// The report was reviewed and dismissed as not violating any rules.
        /// No action will be taken against the reported content.
        /// </summary>
        Dismissed = 3,

        /// <summary>
        /// The report was reviewed and resolved, but no action was required.
        /// The content may have been edited or removed by the author.
        /// </summary>
        Resolved = 4
    }
}
