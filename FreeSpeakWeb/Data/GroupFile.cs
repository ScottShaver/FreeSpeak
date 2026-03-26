namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a file uploaded to a group for sharing with other group members.
    /// Files are stored with GUID-based filenames but display the original filename to users.
    /// Supports an optional approval workflow where files must be approved before becoming visible.
    /// </summary>
    public class GroupFile
    {
        /// <summary>
        /// Gets or sets the unique identifier for the group file.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the group this file belongs to.
        /// </summary>
        public required int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the parent group.
        /// </summary>
        public Group Group { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the user who uploaded the file.
        /// </summary>
        public required string UploaderId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who uploaded the file.
        /// </summary>
        public ApplicationUser Uploader { get; set; } = null!;

        /// <summary>
        /// Gets or sets the original filename as provided by the user during upload.
        /// This is the name displayed to users when viewing or downloading the file.
        /// </summary>
        public required string OriginalFileName { get; set; }

        /// <summary>
        /// Gets or sets the GUID-based filename used for storage on the file system.
        /// This ensures unique filenames and prevents conflicts or security issues with user-provided names.
        /// </summary>
        public required string StoredFileName { get; set; }

        /// <summary>
        /// Gets or sets the user-provided description of the file.
        /// Helps other group members understand the file's purpose or contents.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the size of the file in bytes.
        /// Used for display purposes and potential quota management.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Gets or sets the MIME content type of the file (e.g., "application/pdf", "image/png").
        /// Used for proper content-type headers during download.
        /// </summary>
        public required string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the file was uploaded.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the approval status of the file.
        /// Determines whether the file is visible to other group members.
        /// Defaults to Pending when file approval is required for the group.
        /// </summary>
        public GroupFileStatus Status { get; set; } = GroupFileStatus.Pending;

        /// <summary>
        /// Gets or sets the ID of the administrator or moderator who approved or declined the file.
        /// Null if the file has not been reviewed yet or if approval is not required.
        /// </summary>
        public string? ReviewedById { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who reviewed the file.
        /// </summary>
        public ApplicationUser? ReviewedBy { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the file was reviewed (approved or declined).
        /// Null if the file has not been reviewed yet.
        /// </summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>
        /// Gets or sets the reason provided when declining a file.
        /// Helps uploaders understand why their file was not approved.
        /// Null for approved files or files that have not been reviewed.
        /// </summary>
        public string? DeclinedReason { get; set; }

        /// <summary>
        /// Gets or sets the number of times this file has been downloaded.
        /// Useful for tracking file popularity and usage.
        /// </summary>
        public int DownloadCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether the file has been scanned for viruses.
        /// True if virus scanning completed (regardless of result), false if not scanned yet.
        /// </summary>
        public bool VirusScanCompleted { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the file passed virus scanning.
        /// Null if not scanned, true if clean, false if threats detected.
        /// </summary>
        public bool? VirusScanPassed { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when virus scanning was completed.
        /// Null if the file has not been scanned yet.
        /// </summary>
        public DateTime? VirusScanCompletedAt { get; set; }
    }
}
