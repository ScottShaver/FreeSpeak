namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user file upload audit log entries.
    /// Tracks when users upload files or media to the system.
    /// </summary>
    public class UserUploadDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the type of upload.
        /// Examples: "ProfilePicture", "CoverPhoto", "PostImage", "PostVideo", "Document".
        /// </summary>
        public string UploadType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original file name of the uploaded file.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of the uploaded file.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Gets or sets the size of the uploaded file in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the upload was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the failure reason if the upload was not successful.
        /// </summary>
        public string? FailureReason { get; set; }

        /// <summary>
        /// Gets or sets the storage location or path where the file was saved.
        /// </summary>
        public string? StoragePath { get; set; }
    }
}
