using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Repository interface for managing group file uploads.
    /// Provides methods for CRUD operations, file search, approval workflow, and statistics.
    /// </summary>
    public interface IGroupFileRepository : IRepository<GroupFile>
    {
        /// <summary>
        /// Retrieves a group file by its unique identifier with optional related data.
        /// </summary>
        /// <param name="fileId">The ID of the file.</param>
        /// <param name="includeUploader">Whether to include the uploader's user information. Default is false.</param>
        /// <param name="includeReviewer">Whether to include the reviewer's user information. Default is false.</param>
        /// <returns>The file if found; otherwise, null.</returns>
        Task<GroupFile?> GetByIdAsync(int fileId, bool includeUploader = false, bool includeReviewer = false);

        /// <summary>
        /// Retrieves all files for a specific group with pagination support.
        /// Only returns files with the specified status (defaults to Approved).
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="status">The file status to filter by. Default is Approved.</param>
        /// <param name="skip">Number of files to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of files to retrieve. Default is 20.</param>
        /// <returns>A list of files sorted by upload date descending.</returns>
        Task<List<GroupFile>> GetFilesByGroupAsync(int groupId, GroupFileStatus status = GroupFileStatus.Approved, int skip = 0, int take = 20);

        /// <summary>
        /// Retrieves files uploaded by a specific user within a group.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="uploaderId">The ID of the user who uploaded the files.</param>
        /// <param name="includeAllStatuses">If true, returns files of all statuses; otherwise, only approved files.</param>
        /// <param name="skip">Number of files to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of files to retrieve. Default is 20.</param>
        /// <returns>A list of files uploaded by the user.</returns>
        Task<List<GroupFile>> GetFilesByUploaderAsync(int groupId, string uploaderId, bool includeAllStatuses = false, int skip = 0, int take = 20);

        /// <summary>
        /// Retrieves pending files for a group that require moderator/admin review.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="skip">Number of files to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of files to retrieve. Default is 20.</param>
        /// <returns>A list of pending files sorted by upload date ascending (oldest first).</returns>
        Task<List<GroupFile>> GetPendingFilesAsync(int groupId, int skip = 0, int take = 20);

        /// <summary>
        /// Searches for files within a group by filename or description using partial matching.
        /// Only searches approved files visible to members.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="searchTerm">The search term to match against filename and description.</param>
        /// <param name="skip">Number of files to skip for pagination. Default is 0.</param>
        /// <param name="take">Number of files to retrieve. Default is 20.</param>
        /// <returns>A list of files matching the search criteria.</returns>
        Task<List<GroupFile>> SearchFilesAsync(int groupId, string searchTerm, int skip = 0, int take = 20);

        /// <summary>
        /// Gets the total count of files in a group with the specified status.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="status">The file status to count. If null, counts all files.</param>
        /// <returns>The count of files matching the criteria.</returns>
        Task<int> GetFileCountAsync(int groupId, GroupFileStatus? status = null);

        /// <summary>
        /// Gets the count of files matching a search term within a group.
        /// Only counts approved files.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="searchTerm">The search term to match.</param>
        /// <returns>The count of files matching the search criteria.</returns>
        Task<int> GetSearchResultCountAsync(int groupId, string searchTerm);

        /// <summary>
        /// Approves a pending file, making it visible to all group members.
        /// </summary>
        /// <param name="fileId">The ID of the file to approve.</param>
        /// <param name="reviewerId">The ID of the user approving the file.</param>
        /// <returns>A tuple indicating success and an optional error message.</returns>
        Task<(bool Success, string? ErrorMessage)> ApproveFileAsync(int fileId, string reviewerId);

        /// <summary>
        /// Declines a pending file with a reason.
        /// The file should be removed from storage after being declined.
        /// </summary>
        /// <param name="fileId">The ID of the file to decline.</param>
        /// <param name="reviewerId">The ID of the user declining the file.</param>
        /// <param name="reason">The reason for declining the file.</param>
        /// <returns>A tuple indicating success, an optional error message, and the stored filename for cleanup.</returns>
        Task<(bool Success, string? ErrorMessage, string? StoredFileName)> DeclineFileAsync(int fileId, string reviewerId, string? reason);

        /// <summary>
        /// Deletes a file record and returns information needed for file system cleanup.
        /// </summary>
        /// <param name="fileId">The ID of the file to delete.</param>
        /// <param name="deleterId">The ID of the user deleting the file.</param>
        /// <returns>A tuple indicating success, an optional error message, and the stored filename for cleanup.</returns>
        Task<(bool Success, string? ErrorMessage, string? StoredFileName)> DeleteFileAsync(int fileId, string deleterId);

        /// <summary>
        /// Increments the download count for a file.
        /// </summary>
        /// <param name="fileId">The ID of the file that was downloaded.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task IncrementDownloadCountAsync(int fileId);

        /// <summary>
        /// Updates the virus scan status for a file.
        /// </summary>
        /// <param name="fileId">The ID of the file.</param>
        /// <param name="passed">Whether the virus scan passed (true = clean, false = threat detected).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpdateVirusScanStatusAsync(int fileId, bool passed);

        /// <summary>
        /// Gets files that are pending virus scan completion.
        /// </summary>
        /// <param name="take">Maximum number of files to retrieve. Default is 10.</param>
        /// <returns>A list of files awaiting virus scan.</returns>
        Task<List<GroupFile>> GetFilesAwaitingVirusScanAsync(int take = 10);

        /// <summary>
        /// Checks if a file with the same original filename already exists in the group.
        /// Useful for warning users about potential duplicates.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="originalFileName">The original filename to check.</param>
        /// <returns>True if a file with the same name exists; otherwise, false.</returns>
        Task<bool> FileNameExistsInGroupAsync(int groupId, string originalFileName);

        /// <summary>
        /// Gets the total storage used by a group's files in bytes.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>The total file size in bytes.</returns>
        Task<long> GetGroupStorageUsedAsync(int groupId);

        /// <summary>
        /// Gets the storage used by a specific user within a group in bytes.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="uploaderId">The ID of the user.</param>
        /// <returns>The total file size uploaded by the user in bytes.</returns>
        Task<long> GetUserStorageUsedAsync(int groupId, string uploaderId);
    }
}
