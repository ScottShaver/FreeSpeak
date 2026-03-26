using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// Repository implementation for GroupFile entities.
    /// Provides operations for file upload management, search, approval workflow,
    /// virus scan tracking, and storage statistics within groups.
    /// </summary>
    public class GroupFileRepository : IGroupFileRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<GroupFileRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupFileRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for recording repository operations.</param>
        public GroupFileRepository(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<GroupFileRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        #region Basic CRUD Operations

        /// <inheritdoc />
        public async Task<GroupFile?> GetByIdAsync(int id)
        {
            return await GetByIdAsync(id, false, false);
        }

        /// <inheritdoc />
        public async Task<GroupFile?> GetByIdAsync(int fileId, bool includeUploader = false, bool includeReviewer = false)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                IQueryable<GroupFile> query = context.GroupFiles;

                if (includeUploader)
                {
                    query = query.Include(f => f.Uploader);
                }

                if (includeReviewer)
                {
                    query = query.Include(f => f.ReviewedBy);
                }

                return await query.FirstOrDefaultAsync(f => f.Id == fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group file {FileId}", fileId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<GroupFile>> GetAllAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupFiles
                    .Include(f => f.Uploader)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all group files");
                return [];
            }
        }

        /// <inheritdoc />
        public async Task<GroupFile> AddAsync(GroupFile entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.GroupFiles.Add(entity);
                await context.SaveChangesAsync();
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding group file to group {GroupId}", entity.GroupId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UpdateAsync(GroupFile entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.GroupFiles.Update(entity);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group file {FileId}", entity.Id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(GroupFile entity)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                context.GroupFiles.Remove(entity);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group file {FileId}", entity.Id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(int id)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupFiles.AnyAsync(f => f.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of group file {FileId}", id);
                return false;
            }
        }

        #endregion

        #region File Retrieval Methods

        /// <inheritdoc />
        public async Task<List<GroupFile>> GetFilesByGroupAsync(int groupId, GroupFileStatus status = GroupFileStatus.Approved, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupFiles
                    .Include(f => f.Uploader)
                    .Where(f => f.GroupId == groupId && f.Status == status)
                    .OrderByDescending(f => f.UploadedAt)
                    .Skip(skip)
                    .Take(take)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving files for group {GroupId} with status {Status}", groupId, status);
                return [];
            }
        }

        /// <inheritdoc />
        public async Task<List<GroupFile>> GetFilesByUploaderAsync(int groupId, string uploaderId, bool includeAllStatuses = false, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.GroupFiles
                    .Include(f => f.Uploader)
                    .Where(f => f.GroupId == groupId && f.UploaderId == uploaderId);

                if (!includeAllStatuses)
                {
                    query = query.Where(f => f.Status == GroupFileStatus.Approved);
                }

                return await query
                    .OrderByDescending(f => f.UploadedAt)
                    .Skip(skip)
                    .Take(take)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving files for uploader {UploaderId} in group {GroupId}", uploaderId, groupId);
                return [];
            }
        }

        /// <inheritdoc />
        public async Task<List<GroupFile>> GetPendingFilesAsync(int groupId, int skip = 0, int take = 20)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupFiles
                    .Include(f => f.Uploader)
                    .Where(f => f.GroupId == groupId && f.Status == GroupFileStatus.Pending)
                    .OrderBy(f => f.UploadedAt) // Oldest first for fair review order
                    .Skip(skip)
                    .Take(take)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending files for group {GroupId}", groupId);
                return [];
            }
        }

        /// <inheritdoc />
        public async Task<List<GroupFile>> SearchFilesAsync(int groupId, string searchTerm, int skip = 0, int take = 20)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetFilesByGroupAsync(groupId, GroupFileStatus.Approved, skip, take);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var searchPattern = $"%{searchTerm}%";

                return await context.GroupFiles
                    .Include(f => f.Uploader)
                    .Where(f => f.GroupId == groupId &&
                                f.Status == GroupFileStatus.Approved &&
                                (EF.Functions.ILike(f.OriginalFileName, searchPattern) ||
                                 (f.Description != null && EF.Functions.ILike(f.Description, searchPattern))))
                    .OrderByDescending(f => f.UploadedAt)
                    .Skip(skip)
                    .Take(take)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching files in group {GroupId} with term '{SearchTerm}'", groupId, searchTerm);
                return [];
            }
        }

        #endregion

        #region Count Methods

        /// <inheritdoc />
        public async Task<int> GetFileCountAsync(int groupId, GroupFileStatus? status = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.GroupFiles.Where(f => f.GroupId == groupId);

                if (status.HasValue)
                {
                    query = query.Where(f => f.Status == status.Value);
                }

                return await query.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting files for group {GroupId} with status {Status}", groupId, status);
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<int> GetSearchResultCountAsync(int groupId, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetFileCountAsync(groupId, GroupFileStatus.Approved);
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var searchPattern = $"%{searchTerm}%";

                return await context.GroupFiles
                    .Where(f => f.GroupId == groupId &&
                                f.Status == GroupFileStatus.Approved &&
                                (EF.Functions.ILike(f.OriginalFileName, searchPattern) ||
                                 (f.Description != null && EF.Functions.ILike(f.Description, searchPattern))))
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting search results in group {GroupId} with term '{SearchTerm}'", groupId, searchTerm);
                return 0;
            }
        }

        #endregion

        #region Approval Workflow Methods

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage)> ApproveFileAsync(int fileId, string reviewerId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var file = await context.GroupFiles.FindAsync(fileId);
                if (file == null)
                {
                    return (false, "File not found.");
                }

                if (file.Status != GroupFileStatus.Pending)
                {
                    return (false, "File is not pending approval.");
                }

                file.Status = GroupFileStatus.Approved;
                file.ReviewedById = reviewerId;
                file.ReviewedAt = DateTime.UtcNow;
                file.DeclinedReason = null;

                await context.SaveChangesAsync();

                _logger.LogInformation("File {FileId} approved by user {ReviewerId}", fileId, reviewerId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving file {FileId}", fileId);
                return (false, "An error occurred while approving the file.");
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage, string? StoredFileName)> DeclineFileAsync(int fileId, string reviewerId, string? reason)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var file = await context.GroupFiles.FindAsync(fileId);
                if (file == null)
                {
                    return (false, "File not found.", null);
                }

                if (file.Status != GroupFileStatus.Pending)
                {
                    return (false, "File is not pending approval.", null);
                }

                var storedFileName = file.StoredFileName;

                file.Status = GroupFileStatus.Declined;
                file.ReviewedById = reviewerId;
                file.ReviewedAt = DateTime.UtcNow;
                file.DeclinedReason = reason;

                await context.SaveChangesAsync();

                _logger.LogInformation("File {FileId} declined by user {ReviewerId}. Reason: {Reason}", fileId, reviewerId, reason);
                return (true, null, storedFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining file {FileId}", fileId);
                return (false, "An error occurred while declining the file.", null);
            }
        }

        /// <inheritdoc />
        public async Task<(bool Success, string? ErrorMessage, string? StoredFileName)> DeleteFileAsync(int fileId, string deleterId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var file = await context.GroupFiles.FindAsync(fileId);
                if (file == null)
                {
                    return (false, "File not found.", null);
                }

                var storedFileName = file.StoredFileName;

                context.GroupFiles.Remove(file);
                await context.SaveChangesAsync();

                _logger.LogInformation("File {FileId} deleted by user {DeleterId}", fileId, deleterId);
                return (true, null, storedFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileId}", fileId);
                return (false, "An error occurred while deleting the file.", null);
            }
        }

        #endregion

        #region Utility Methods

        /// <inheritdoc />
        public async Task IncrementDownloadCountAsync(int fileId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Use raw SQL for efficient atomic increment without loading the entity
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"GroupFiles\" SET \"DownloadCount\" = \"DownloadCount\" + 1 WHERE \"Id\" = {fileId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing download count for file {FileId}", fileId);
                // Don't throw - download count is not critical
            }
        }

        /// <inheritdoc />
        public async Task UpdateVirusScanStatusAsync(int fileId, bool passed)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var file = await context.GroupFiles.FindAsync(fileId);
                if (file != null)
                {
                    file.VirusScanCompleted = true;
                    file.VirusScanPassed = passed;
                    file.VirusScanCompletedAt = DateTime.UtcNow;

                    await context.SaveChangesAsync();

                    _logger.LogInformation("Virus scan completed for file {FileId}. Passed: {Passed}", fileId, passed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating virus scan status for file {FileId}", fileId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<GroupFile>> GetFilesAwaitingVirusScanAsync(int take = 10)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupFiles
                    .Where(f => !f.VirusScanCompleted)
                    .OrderBy(f => f.UploadedAt)
                    .Take(take)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving files awaiting virus scan");
                return [];
            }
        }

        /// <inheritdoc />
        public async Task<bool> FileNameExistsInGroupAsync(int groupId, string originalFileName)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupFiles
                    .AnyAsync(f => f.GroupId == groupId &&
                                   f.Status == GroupFileStatus.Approved &&
                                   EF.Functions.ILike(f.OriginalFileName, originalFileName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking filename existence in group {GroupId}", groupId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<long> GetGroupStorageUsedAsync(int groupId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupFiles
                    .Where(f => f.GroupId == groupId && f.Status != GroupFileStatus.Declined)
                    .SumAsync(f => f.FileSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating storage used for group {GroupId}", groupId);
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<long> GetUserStorageUsedAsync(int groupId, string uploaderId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                return await context.GroupFiles
                    .Where(f => f.GroupId == groupId &&
                                f.UploaderId == uploaderId &&
                                f.Status != GroupFileStatus.Declined)
                    .SumAsync(f => f.FileSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating storage used by user {UploaderId} in group {GroupId}", uploaderId, groupId);
                return 0;
            }
        }

        #endregion
    }
}
