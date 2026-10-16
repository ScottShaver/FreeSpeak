namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for system moderator activity log CSV export audit log entries.
    /// </summary>
    public class SystemModeratorActivityExportDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the number of rows exported.
        /// </summary>
        public int RowCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the export was truncated at the row cap.
        /// </summary>
        public bool Truncated { get; set; }

        /// <summary>
        /// Gets or sets the server-set UTC time of the export.
        /// </summary>
        public DateTime ExportedAtUtc { get; set; }
    }
}
