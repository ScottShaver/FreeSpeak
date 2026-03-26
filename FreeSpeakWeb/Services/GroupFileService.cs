using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service for managing group file uploads and sharing.
    /// Handles file upload, download, search, approval workflow, and administration.
    /// Files are stored with GUID-based filenames in a dedicated group files directory.
    /// </summary>
    public class GroupFileService : IGroupFileService
    {
        private readonly IGroupFileRepository _fileRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupFileService> _logger;
        private readonly IFileSignatureValidator _fileSignatureValidator;
        private readonly IVirusScanService _virusScanService;
        private readonly UserPreferenceService _userPreferenceService;
        private readonly GroupMemberService _groupMemberService;
        private readonly string _uploadsBasePath;

        /// <summary>
        /// Maximum allowed size for a single file upload (50MB).
        /// </summary>
        private const long MaxFileSizeBytes = 50 * 1024 * 1024;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupFileService"/> class.
        /// </summary>
        /// <param name="fileRepository">Repository for file operations.</param>
        /// <param name="groupRepository">Repository for group operations.</param>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording service operations.</param>
        /// <param name="environment">Web hosting environment for determining file paths.</param>
        /// <param name="fileSignatureValidator">Validator for file magic bytes.</param>
        /// <param name="virusScanService">Service for scanning files for viruses/malware.</param>
        /// <param name="userPreferenceService">Service for user preference operations.</param>
        /// <param name="groupMemberService">Service for group member operations.</param>
        public GroupFileService(
            IGroupFileRepository fileRepository,
            IGroupRepository groupRepository,
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupFileService> logger,
            IWebHostEnvironment environment,
            IFileSignatureValidator fileSignatureValidator,
            IVirusScanService virusScanService,
            UserPreferenceService userPreferenceService,
            GroupMemberService groupMemberService)
        {
            _fileRepository = fileRepository;
            _groupRepository = groupRepository;
            _contextFactory = contextFactory;
            _logger = logger;
            _fileSignatureValidator = fileSignatureValidator;
            _virusScanService = virusScanService;
            _userPreferenceService = userPreferenceService;
            _groupMemberService = groupMemberService;
            // SECURITY: Store uploads outside wwwroot to prevent direct access
            _uploadsBasePath = Path.Combine(environment.ContentRootPath, "AppData", "uploads", "groupfiles");
        }

        #region File Upload

        /// <inheritdoc />
        public async Task<GroupFileUploadResult> UploadFileAsync(
            int groupId,
            string uploaderId,
            string fileName,
            string? description,
            Stream fileStream,
            string contentType,
            long fileSize,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(uploaderId) || !Guid.TryParse(uploaderId, out _))
                {
                    return GroupFileUploadResult.Failed("Invalid user ID format.");
                }

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return GroupFileUploadResult.Failed("Filename is required.");
                }

                if (fileSize <= 0)
                {
                    return GroupFileUploadResult.Failed("Invalid file size.");
                }

                if (fileSize > MaxFileSizeBytes)
                {
                    return GroupFileUploadResult.Failed($"File exceeds the maximum size of {MaxFileSizeBytes / 1024 / 1024}MB.");
                }

                // Check if user can upload files
                if (!await CanUploadFilesAsync(groupId, uploaderId))
                {
                    return GroupFileUploadResult.Failed("You do not have permission to upload files to this group.");
                }

                // Get group to check settings
                var group = await _groupRepository.GetByIdAsync(groupId);
                if (group == null)
                {
                    return GroupFileUploadResult.Failed("Group not found.");
                }

                if (!group.EnableFileUploads)
                {
                    return GroupFileUploadResult.Failed("File uploads are not enabled for this group.");
                }

                // Read file bytes for validation
                using var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream, cancellationToken);
                var fileBytes = memoryStream.ToArray();

                // SECURITY: Validate file signature (magic bytes) for group files
                // This allows documents and archives but blocks executables/scripts
                var signatureValidation = _fileSignatureValidator.ValidateGroupFileSignature(fileBytes, fileName);
                if (!signatureValidation.IsValid)
                {
                    _logger.LogWarning("File {FileName} failed signature validation for user {UploaderId}: {Error}",
                        fileName, uploaderId, signatureValidation.ErrorMessage);
                    return GroupFileUploadResult.Failed($"File validation failed: {signatureValidation.ErrorMessage}");
                }

                // SECURITY: Scan for viruses/malware (synchronous for immediate feedback)
                bool isPendingVirusScan = false;
                var virusScanAvailable = await _virusScanService.IsAvailableAsync(cancellationToken);

                if (virusScanAvailable)
                {
                    var virusScanResult = await _virusScanService.ScanAsync(fileBytes, fileName, cancellationToken);
                    if (!virusScanResult.IsClean)
                    {
                        _logger.LogWarning("File {FileName} failed virus scan for user {UploaderId}: {Virus}",
                            fileName, uploaderId, virusScanResult.VirusName ?? virusScanResult.ErrorMessage);
                        var reason = virusScanResult.VirusName != null
                            ? $"Malware detected: {virusScanResult.VirusName}"
                            : virusScanResult.ErrorMessage ?? "Virus scan failed";
                        return GroupFileUploadResult.Failed($"File rejected: {reason}");
                    }
                }
                else
                {
                    // Virus scanning not available - mark for later scan
                    isPendingVirusScan = true;
                }

                // Generate unique filename
                var fileExtension = Path.GetExtension(fileName);
                var storedFileName = $"{Guid.NewGuid()}{fileExtension}";

                // Create group files directory
                var groupFilesPath = GetGroupFilesPath(groupId);
                if (!Directory.Exists(groupFilesPath))
                {
                    Directory.CreateDirectory(groupFilesPath);
                    _logger.LogInformation("Created group files directory: {Path}", groupFilesPath);
                }

                // Save file to disk
                var filePath = Path.Combine(groupFilesPath, storedFileName);
                await File.WriteAllBytesAsync(filePath, fileBytes, cancellationToken);

                // Determine initial status
                var status = group.RequiresFileApproval ? GroupFileStatus.Pending : GroupFileStatus.Approved;
                var isPendingApproval = status == GroupFileStatus.Pending;

                // Create database record
                var groupFile = new GroupFile
                {
                    GroupId = groupId,
                    UploaderId = uploaderId,
                    OriginalFileName = fileName,
                    StoredFileName = storedFileName,
                    Description = description,
                    FileSize = fileSize,
                    ContentType = contentType,
                    UploadedAt = DateTime.UtcNow,
                    Status = status,
                    VirusScanCompleted = !isPendingVirusScan,
                    VirusScanPassed = isPendingVirusScan ? null : true
                };

                var savedFile = await _fileRepository.AddAsync(groupFile);

                _logger.LogInformation("File {FileName} uploaded as {StoredFileName} to group {GroupId} by user {UploaderId}",
                    fileName, storedFileName, groupId, uploaderId);

                return GroupFileUploadResult.Succeeded(savedFile, isPendingApproval, isPendingVirusScan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {FileName} to group {GroupId}", fileName, groupId);
                return GroupFileUploadResult.Failed("An error occurred while uploading the file.");
            }
        }

        #endregion

        #region File Download

        /// <inheritdoc />
        public async Task<GroupFileDownloadResult> GetFileForDownloadAsync(int fileId, string userId)
        {
            try
            {
                var file = await _fileRepository.GetByIdAsync(fileId, includeUploader: true);
                if (file == null)
                {
                    return GroupFileDownloadResult.Failed("File not found.");
                }

                // Check if user is a member of the group
                var isMember = await _groupMemberService.IsMemberAsync(file.GroupId, userId);
                if (!isMember)
                {
                    return GroupFileDownloadResult.Failed("You do not have permission to access this file.");
                }

                // Check file status - only approved files or user's own files
                if (file.Status != GroupFileStatus.Approved && file.UploaderId != userId)
                {
                    // Allow admins/moderators to download pending files
                    var canManage = await CanManageFilesAsync(file.GroupId, userId);
                    if (!canManage)
                    {
                        return GroupFileDownloadResult.Failed("This file is not yet available for download.");
                    }
                }

                // Get file path
                var filePath = Path.Combine(GetGroupFilesPath(file.GroupId), file.StoredFileName);
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File {FileId} physical file not found at {Path}", fileId, filePath);
                    return GroupFileDownloadResult.Failed("File not found on disk.");
                }

                // Increment download count (fire and forget)
                _ = _fileRepository.IncrementDownloadCountAsync(fileId);

                // Open file stream
                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                return GroupFileDownloadResult.Succeeded(fileStream, file.OriginalFileName, file.ContentType, file.FileSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file {FileId} for download", fileId);
                return GroupFileDownloadResult.Failed("An error occurred while accessing the file.");
            }
        }

        /// <inheritdoc />
        public async Task<GroupFile?> GetFileByIdAsync(int fileId)
        {
            return await _fileRepository.GetByIdAsync(fileId, includeUploader: true, includeReviewer: true);
        }

        #endregion

        #region File Listing and Search

        /// <inheritdoc />
        public async Task<List<GroupFileDto>> GetGroupFilesAsync(int groupId, int skip = 0, int take = 20)
        {
            var files = await _fileRepository.GetFilesByGroupAsync(groupId, GroupFileStatus.Approved, skip, take);
            return await ConvertToFileDtosAsync(files);
        }

        /// <inheritdoc />
        public async Task<List<GroupFileDto>> SearchFilesAsync(int groupId, string searchTerm, int skip = 0, int take = 20)
        {
            var files = await _fileRepository.SearchFilesAsync(groupId, searchTerm, skip, take);
            return await ConvertToFileDtosAsync(files);
        }

        /// <inheritdoc />
        public async Task<int> GetFileCountAsync(int groupId)
        {
            return await _fileRepository.GetFileCountAsync(groupId, GroupFileStatus.Approved);
        }

        /// <inheritdoc />
        public async Task<int> GetSearchResultCountAsync(int groupId, string searchTerm)
        {
            return await _fileRepository.GetSearchResultCountAsync(groupId, searchTerm);
        }

        /// <inheritdoc />
        public async Task<List<GroupFileDto>> GetUserFilesAsync(int groupId, string uploaderId, bool includeAllStatuses = false, int skip = 0, int take = 20)
        {
            var files = await _fileRepository.GetFilesByUploaderAsync(groupId, uploaderId, includeAllStatuses, skip, take);
            return await ConvertToFileDtosAsync(files);
        }

        #endregion

        #region Approval Workflow

        /// <inheritdoc />
        public async Task<List<GroupFileDto>> GetPendingFilesAsync(int groupId, int skip = 0, int take = 20)
        {
            var files = await _fileRepository.GetPendingFilesAsync(groupId, skip, take);
            return await ConvertToFileDtosAsync(files);
        }

        /// <inheritdoc />
        public async Task<int> GetPendingFileCountAsync(int groupId)
        {
            return await _fileRepository.GetFileCountAsync(groupId, GroupFileStatus.Pending);
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> ApproveFileAsync(int fileId, string reviewerId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
            {
                return (false, "File not found.");
            }

            var canManage = await CanManageFilesAsync(file.GroupId, reviewerId);
            if (!canManage)
            {
                return (false, "You do not have permission to approve files.");
            }

            var result = await _fileRepository.ApproveFileAsync(fileId, reviewerId);

            if (result.Success)
            {
                _logger.LogInformation("File {FileId} approved by user {ReviewerId}", fileId, reviewerId);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> DeclineFileAsync(int fileId, string reviewerId, string? reason)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
            {
                return (false, "File not found.");
            }

            var canManage = await CanManageFilesAsync(file.GroupId, reviewerId);
            if (!canManage)
            {
                return (false, "You do not have permission to decline files.");
            }

            var result = await _fileRepository.DeclineFileAsync(fileId, reviewerId, reason);

            if (result.Success && result.StoredFileName != null)
            {
                // Delete the physical file
                await DeletePhysicalFileAsync(file.GroupId, result.StoredFileName);
                _logger.LogInformation("File {FileId} declined and deleted by user {ReviewerId}. Reason: {Reason}",
                    fileId, reviewerId, reason);
            }

            return (result.Success, result.ErrorMessage);
        }

        #endregion

        #region File Administration

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> DeleteFileAsync(int fileId, string deleterId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
            {
                return (false, "File not found.");
            }

            // Check permissions - uploader can delete their own files, or admins/mods
            if (file.UploaderId != deleterId)
            {
                var canManage = await CanManageFilesAsync(file.GroupId, deleterId);
                if (!canManage)
                {
                    return (false, "You do not have permission to delete this file.");
                }
            }

            var result = await _fileRepository.DeleteFileAsync(fileId, deleterId);

            if (result.Success && result.StoredFileName != null)
            {
                // Delete the physical file
                await DeletePhysicalFileAsync(file.GroupId, result.StoredFileName);
                _logger.LogInformation("File {FileId} deleted by user {DeleterId}", fileId, deleterId);
            }

            return (result.Success, result.ErrorMessage);
        }

        /// <inheritdoc />
        public async Task<bool> CanManageFilesAsync(int groupId, string userId)
        {
            // Group admins and moderators can manage files
            var isAdmin = await _groupMemberService.IsAdminAsync(groupId, userId);
            if (isAdmin) return true;

            var isModerator = await _groupMemberService.IsModeratorAsync(groupId, userId);
            if (isModerator) return true;

            // Check if user is the group creator
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group != null && group.CreatorId == userId)
            {
                return true;
            }

            // TODO: Check system-level admin/moderator roles if needed

            return false;
        }

        /// <inheritdoc />
        public async Task<bool> CanUploadFilesAsync(int groupId, string userId)
        {
            // Check if user is a member
            var isMember = await _groupMemberService.IsMemberAsync(groupId, userId);
            if (!isMember) return false;

            // Check if file uploads are enabled for the group
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null || !group.EnableFileUploads)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Statistics and Storage

        /// <inheritdoc />
        public async Task<GroupFileStorageStats> GetStorageStatsAsync(int groupId)
        {
            var totalStorage = await _fileRepository.GetGroupStorageUsedAsync(groupId);
            var totalCount = await _fileRepository.GetFileCountAsync(groupId);
            var approvedCount = await _fileRepository.GetFileCountAsync(groupId, GroupFileStatus.Approved);
            var pendingCount = await _fileRepository.GetFileCountAsync(groupId, GroupFileStatus.Pending);

            return new GroupFileStorageStats
            {
                TotalStorageBytes = totalStorage,
                TotalFileCount = totalCount,
                ApprovedFileCount = approvedCount,
                PendingFileCount = pendingCount
            };
        }

        /// <inheritdoc />
        public async Task<long> GetUserStorageUsedAsync(int groupId, string userId)
        {
            return await _fileRepository.GetUserStorageUsedAsync(groupId, userId);
        }

        #endregion

        #region Virus Scanning

        /// <inheritdoc />
        public async Task<int> ProcessPendingVirusScansAsync(CancellationToken cancellationToken = default)
        {
            var processedCount = 0;

            try
            {
                var filesAwaitingScan = await _fileRepository.GetFilesAwaitingVirusScanAsync(10);

                foreach (var file in filesAwaitingScan)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    try
                    {
                        var filePath = Path.Combine(GetGroupFilesPath(file.GroupId), file.StoredFileName);
                        if (!File.Exists(filePath))
                        {
                            // File doesn't exist - mark as failed
                            await _fileRepository.UpdateVirusScanStatusAsync(file.Id, false);
                            continue;
                        }

                        var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                        var scanResult = await _virusScanService.ScanAsync(fileBytes, file.OriginalFileName, cancellationToken);

                        await _fileRepository.UpdateVirusScanStatusAsync(file.Id, scanResult.IsClean);

                        if (!scanResult.IsClean)
                        {
                            // Delete infected file
                            _logger.LogWarning("File {FileId} ({FileName}) failed virus scan: {Virus}. Deleting.",
                                file.Id, file.OriginalFileName, scanResult.VirusName);

                            await DeletePhysicalFileAsync(file.GroupId, file.StoredFileName);

                            // Update status to declined
                            await _fileRepository.DeclineFileAsync(file.Id, "SYSTEM", $"Virus detected: {scanResult.VirusName}");
                        }

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error scanning file {FileId}", file.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending virus scans");
            }

            return processedCount;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Gets the physical path for a group's files directory.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <returns>The full path to the group's files directory.</returns>
        private string GetGroupFilesPath(int groupId)
        {
            return Path.Combine(_uploadsBasePath, groupId.ToString());
        }

        /// <summary>
        /// Deletes a physical file from disk.
        /// </summary>
        /// <param name="groupId">The ID of the group.</param>
        /// <param name="storedFileName">The stored filename to delete.</param>
        private async Task DeletePhysicalFileAsync(int groupId, string storedFileName)
        {
            try
            {
                var filePath = Path.Combine(GetGroupFilesPath(groupId), storedFileName);
                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath));
                    _logger.LogInformation("Deleted physical file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting physical file {StoredFileName} for group {GroupId}",
                    storedFileName, groupId);
            }
        }

        /// <summary>
        /// Converts a list of GroupFile entities to GroupFileDto objects.
        /// Applies user display name formatting.
        /// </summary>
        /// <param name="files">The files to convert.</param>
        /// <returns>A list of file DTOs.</returns>
        private async Task<List<GroupFileDto>> ConvertToFileDtosAsync(List<GroupFile> files)
        {
            if (!files.Any()) return [];

            // Collect unique uploader details for batch formatting
            var uploaderDetails = new Dictionary<string, (string firstName, string lastName, string userName)>();
            foreach (var file in files)
            {
                if (!string.IsNullOrEmpty(file.UploaderId) && !uploaderDetails.ContainsKey(file.UploaderId))
                {
                    uploaderDetails[file.UploaderId] = (
                        file.Uploader?.FirstName ?? "",
                        file.Uploader?.LastName ?? "",
                        file.Uploader?.UserName ?? ""
                    );
                }
            }

            // Batch format display names
            var formattedNames = await _userPreferenceService.FormatUserDisplayNamesAsync(uploaderDetails);

            // Convert to DTOs
            var dtos = new List<GroupFileDto>();
            foreach (var file in files)
            {
                var uploaderName = formattedNames.TryGetValue(file.UploaderId, out var name)
                    ? name
                    : $"{file.Uploader?.FirstName} {file.Uploader?.LastName}".Trim();

                if (string.IsNullOrWhiteSpace(uploaderName))
                {
                    uploaderName = file.Uploader?.UserName ?? "Unknown";
                }

                dtos.Add(GroupFileDto.FromEntity(file, uploaderName));
            }

            return dtos;
        }

        #endregion
    }
}
