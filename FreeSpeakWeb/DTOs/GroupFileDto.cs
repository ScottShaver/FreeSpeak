using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.DTOs
{
    /// <summary>
    /// Data Transfer Object for group file list views.
    /// Reduces data transfer by selecting only necessary fields instead of loading full entities.
    /// Includes formatted uploader name based on user display preferences.
    /// </summary>
    /// <param name="Id">The unique identifier of the file.</param>
    /// <param name="GroupId">The ID of the group this file belongs to.</param>
    /// <param name="UploaderId">The ID of the user who uploaded the file.</param>
    /// <param name="UploaderName">The formatted display name of the uploader.</param>
    /// <param name="UploaderImageUrl">The URL for the uploader's profile picture.</param>
    /// <param name="OriginalFileName">The original filename as uploaded by the user.</param>
    /// <param name="Description">The description of the file.</param>
    /// <param name="FileSize">The size of the file in bytes.</param>
    /// <param name="ContentType">The MIME type of the file.</param>
    /// <param name="UploadedAt">The timestamp when the file was uploaded.</param>
    /// <param name="Status">The current status of the file (Pending, Approved, Declined).</param>
    /// <param name="DownloadCount">The number of times this file has been downloaded.</param>
    /// <param name="VirusScanCompleted">Whether virus scanning has completed.</param>
    /// <param name="VirusScanPassed">Whether the file passed virus scanning (null if not scanned).</param>
    public record GroupFileDto(
        int Id,
        int GroupId,
        string UploaderId,
        string UploaderName,
        string? UploaderImageUrl,
        string OriginalFileName,
        string? Description,
        long FileSize,
        string ContentType,
        DateTime UploadedAt,
        GroupFileStatus Status,
        int DownloadCount,
        bool VirusScanCompleted,
        bool? VirusScanPassed
    )
    {
        /// <summary>
        /// Gets the formatted file size (e.g., "2.5 MB").
        /// </summary>
        public string FormattedFileSize => FormatBytes(FileSize);

        /// <summary>
        /// Gets the file extension from the original filename.
        /// </summary>
        public string FileExtension => Path.GetExtension(OriginalFileName).TrimStart('.').ToUpperInvariant();

        /// <summary>
        /// Gets whether the file is an image based on content type.
        /// </summary>
        public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether the file is a PDF.
        /// </summary>
        public bool IsPdf => ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether the file is a document (Word, Excel, etc.).
        /// </summary>
        public bool IsDocument => ContentType.Contains("document", StringComparison.OrdinalIgnoreCase) ||
                                   ContentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) ||
                                   ContentType.Contains("presentation", StringComparison.OrdinalIgnoreCase) ||
                                   ContentType.Contains("msword", StringComparison.OrdinalIgnoreCase) ||
                                   ContentType.Contains("ms-excel", StringComparison.OrdinalIgnoreCase) ||
                                   ContentType.Contains("ms-powerpoint", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether the file is an archive (zip, rar, etc.).
        /// </summary>
        public bool IsArchive => ContentType.Contains("zip", StringComparison.OrdinalIgnoreCase) ||
                                  ContentType.Contains("rar", StringComparison.OrdinalIgnoreCase) ||
                                  ContentType.Contains("tar", StringComparison.OrdinalIgnoreCase) ||
                                  ContentType.Contains("7z", StringComparison.OrdinalIgnoreCase) ||
                                  ContentType.Contains("archive", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the appropriate Bootstrap icon class for the file type.
        /// </summary>
        public string FileIcon
        {
            get
            {
                if (IsImage) return "bi-file-image";
                if (IsPdf) return "bi-file-pdf";
                if (IsDocument) return "bi-file-earmark-text";
                if (IsArchive) return "bi-file-zip";
                if (ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return "bi-file-play";
                if (ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return "bi-file-music";
                if (ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return "bi-file-code";
                return "bi-file-earmark";
            }
        }

        /// <summary>
        /// Gets the relative time since upload (e.g., "2 hours ago").
        /// </summary>
        public string RelativeUploadTime => GetRelativeTime(UploadedAt);

        /// <summary>
        /// Creates a GroupFileDto from a GroupFile entity.
        /// </summary>
        /// <param name="file">The file entity.</param>
        /// <param name="formattedUploaderName">The formatted display name for the uploader.</param>
        /// <returns>A new GroupFileDto instance.</returns>
        public static GroupFileDto FromEntity(GroupFile file, string formattedUploaderName)
        {
            return new GroupFileDto(
                file.Id,
                file.GroupId,
                file.UploaderId,
                formattedUploaderName,
                file.Uploader?.ProfilePictureUrl,
                file.OriginalFileName,
                file.Description,
                file.FileSize,
                file.ContentType,
                file.UploadedAt,
                file.Status,
                file.DownloadCount,
                file.VirusScanCompleted,
                file.VirusScanPassed
            );
        }

        /// <summary>
        /// Formats bytes into a human-readable string.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Gets a relative time string from a DateTime.
        /// </summary>
        private static string GetRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays}d ago";

            return dateTime.ToString("MMM dd, yyyy");
        }
    }
}
