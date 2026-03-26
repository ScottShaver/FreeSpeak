using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// Service interface for managing group file uploads and sharing.
    /// Provides methods for file upload, download, search, approval workflow, and administration.
    /// </summary>
    public interface IGroupFileService
    {
        #region File Upload

        /// <summary>
        /// Uploads a file to a group.
        /// Stores the file with a GUID-based filename and creates a database record.
        /// Optionally triggers virus scanning based on site settings.
        /// </summary>
        /// <param name="groupId">The ID of the group to upload to.</param>
        /// <param name="uploaderId">The ID of the user uploading the file.</param>
        /// <param name="fileName">The original filename provided by the user.</param>
        /// <param name="description">An optional description for the file.</param>
        /// <param name="fileStream">The file content stream.</param>
        /// <param name="contentType">The MIME type of the file.</param>
        /// <param name="fileSize">The size of the file in bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A result containing the created file record or an error message.</returns>
        Task<GroupFileUploadResult> UploadFileAsync(
            int groupId,
            string uploaderId,
            string fileName,
            string? description,
            Stream fileStream,
            string contentType,
            long fileSize,
            CancellationToken cancellationToken = default);

        #endregion

        #region File Download

        /// <summary>
        /// Gets file information and stream for download.
        /// Verifies the user has access to the file before returning.
        /// </summary>
        /// <param name="fileId">The ID of the file to download.</param>
        /// <param name="userId">The ID of the user requesting the download.</param>
        /// <returns>A result containing the file stream and metadata, or an error message.</returns>
        Task<GroupFileDownloadResult> GetFileForDownloadAsync(int fileId, string userId);

        /// <summary>
        /// Gets file metadata without the file content.
        /// </summary>
        /// <param name="fileId">The ID of the file.</param>
        /// <returns>The file record if found; otherwise, null.</returns>
        Task<GroupFile?> GetFileByIdAsync(int fileId);

        #endregion

        #region File Listing and Search

        /// <summary>
        /// Gets a paginated list of approved files in a group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="skip">Number of files to skip for pagination.</param>
        /// <param name="take">Number of files to retrieve.</param>
        /// <returns>A list of file DTOs with formatted uploader names.</returns>
        Task<List<GroupFileDto>> GetGroupFilesAsync(int groupId, int skip = 0, int take = 20);

        /// <summary>
        /// Searches for files in a group by filename or description.
        /// Uses case-insensitive partial matching.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="searchTerm">The search term to match.</param>
        /// <param name="skip">Number of files to skip for pagination.</param>
        /// <param name="take">Number of files to retrieve.</param>
        /// <returns>A list of file DTOs matching the search criteria.</returns>
        Task<List<GroupFileDto>> SearchFilesAsync(int groupId, string searchTerm, int skip = 0, int take = 20);

        /// <summary>
        /// Gets the total count of approved files in a group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>The total file count.</returns>
        Task<int> GetFileCountAsync(int groupId);

        /// <summary>
        /// Gets the count of files matching a search term.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="searchTerm">The search term to match.</param>
        /// <returns>The count of matching files.</returns>
        Task<int> GetSearchResultCountAsync(int groupId, string searchTerm);

        /// <summary>
        /// Gets files uploaded by a specific user in a group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="uploaderId">The ID of the uploader.</param>
        /// <param name="includeAllStatuses">If true, includes pending and declined files for the uploader.</param>
        /// <param name="skip">Number of files to skip.</param>
        /// <param name="take">Number of files to retrieve.</param>
        /// <returns>A list of file DTOs.</returns>
        Task<List<GroupFileDto>> GetUserFilesAsync(int groupId, string uploaderId, bool includeAllStatuses = false, int skip = 0, int take = 20);

        #endregion

        #region Approval Workflow

        /// <summary>
        /// Gets pending files awaiting approval in a group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="skip">Number of files to skip.</param>
        /// <param name="take">Number of files to retrieve.</param>
        /// <returns>A list of pending file DTOs.</returns>
        Task<List<GroupFileDto>> GetPendingFilesAsync(int groupId, int skip = 0, int take = 20);

        /// <summary>
        /// Gets the count of pending files in a group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>The count of pending files.</returns>
        Task<int> GetPendingFileCountAsync(int groupId);

        /// <summary>
        /// Approves a pending file, making it visible to all group members.
        /// </summary>
        /// <param name="fileId">The ID of the file to approve.</param>
        /// <param name="reviewerId">The ID of the user approving the file.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<(bool Success, string? ErrorMessage)> ApproveFileAsync(int fileId, string reviewerId);

        /// <summary>
        /// Declines a pending file with a reason.
        /// Removes the file from storage after declining.
        /// </summary>
        /// <param name="fileId">The ID of the file to decline.</param>
        /// <param name="reviewerId">The ID of the user declining the file.</param>
        /// <param name="reason">The reason for declining.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<(bool Success, string? ErrorMessage)> DeclineFileAsync(int fileId, string reviewerId, string? reason);

        #endregion

        #region File Administration

        /// <summary>
        /// Deletes a file from a group.
        /// Removes both the database record and the stored file.
        /// </summary>
        /// <param name="fileId">The ID of the file to delete.</param>
        /// <param name="deleterId">The ID of the user deleting the file.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<(bool Success, string? ErrorMessage)> DeleteFileAsync(int fileId, string deleterId);

        /// <summary>
        /// Checks if a user can manage files in a group (admin/moderator permissions).
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user can manage files; otherwise, false.</returns>
        Task<bool> CanManageFilesAsync(int groupId, string userId);

        /// <summary>
        /// Checks if a user can upload files to a group (membership and feature enabled).
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user can upload files; otherwise, false.</returns>
        Task<bool> CanUploadFilesAsync(int groupId, string userId);

        #endregion

        #region Statistics and Storage

        /// <summary>
        /// Gets storage statistics for a group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>Storage statistics including total size and file count.</returns>
        Task<GroupFileStorageStats> GetStorageStatsAsync(int groupId);

        /// <summary>
        /// Gets storage usage for a specific user in a group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The storage used in bytes.</returns>
        Task<long> GetUserStorageUsedAsync(int groupId, string userId);

        #endregion

        #region Virus Scanning

        /// <summary>
        /// Processes pending virus scans for uploaded files.
        /// Called by a background service to scan files asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The number of files processed.</returns>
        Task<int> ProcessPendingVirusScansAsync(CancellationToken cancellationToken = default);

        #endregion
    }

    /// <summary>
    /// Result of a file upload operation.
    /// </summary>
    public class GroupFileUploadResult
    {
        /// <summary>
        /// Gets whether the upload was successful.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets the error message if the upload failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Gets the created file record if successful.
        /// </summary>
        public GroupFile? File { get; init; }

        /// <summary>
        /// Gets whether the file is pending approval.
        /// </summary>
        public bool IsPendingApproval { get; init; }

        /// <summary>
        /// Gets whether virus scanning is pending.
        /// </summary>
        public bool IsPendingVirusScan { get; init; }

        /// <summary>
        /// Creates a successful upload result.
        /// </summary>
        public static GroupFileUploadResult Succeeded(GroupFile file, bool isPendingApproval = false, bool isPendingVirusScan = false) => new()
        {
            Success = true,
            File = file,
            IsPendingApproval = isPendingApproval,
            IsPendingVirusScan = isPendingVirusScan
        };

        /// <summary>
        /// Creates a failed upload result.
        /// </summary>
        public static GroupFileUploadResult Failed(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Result of a file download request.
    /// </summary>
    public class GroupFileDownloadResult
    {
        /// <summary>
        /// Gets whether the download request was successful.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets the error message if the request failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Gets the file stream for download.
        /// </summary>
        public Stream? FileStream { get; init; }

        /// <summary>
        /// Gets the original filename for the Content-Disposition header.
        /// </summary>
        public string? FileName { get; init; }

        /// <summary>
        /// Gets the content type for the Content-Type header.
        /// </summary>
        public string? ContentType { get; init; }

        /// <summary>
        /// Gets the file size in bytes.
        /// </summary>
        public long FileSize { get; init; }

        /// <summary>
        /// Creates a successful download result.
        /// </summary>
        public static GroupFileDownloadResult Succeeded(Stream fileStream, string fileName, string contentType, long fileSize) => new()
        {
            Success = true,
            FileStream = fileStream,
            FileName = fileName,
            ContentType = contentType,
            FileSize = fileSize
        };

        /// <summary>
        /// Creates a failed download result.
        /// </summary>
        public static GroupFileDownloadResult Failed(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Storage statistics for a group's files.
    /// </summary>
    public class GroupFileStorageStats
    {
        /// <summary>
        /// Gets the total storage used in bytes.
        /// </summary>
        public long TotalStorageBytes { get; init; }

        /// <summary>
        /// Gets the total number of files.
        /// </summary>
        public int TotalFileCount { get; init; }

        /// <summary>
        /// Gets the number of approved files.
        /// </summary>
        public int ApprovedFileCount { get; init; }

        /// <summary>
        /// Gets the number of pending files.
        /// </summary>
        public int PendingFileCount { get; init; }

        /// <summary>
        /// Gets the formatted total storage size (e.g., "2.5 GB").
        /// </summary>
        public string FormattedTotalStorage => FormatBytes(TotalStorageBytes);

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
    }
}
