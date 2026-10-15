using FreeSpeakWeb.Data;
using FreeSpeakWeb.Data.AuditLogDetails;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service implementation for managing user roles in ASP.NET Core Identity.
    /// Wraps UserManager and RoleManager functionality for role operations.
    /// </summary>
    public class RoleService : IRoleService
    {
        private const string SystemAdministratorRole = "SystemAdministrator";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<RoleService> _logger;
        private readonly IAuditLogRepository _auditLogRepository;

        /// <summary>
        /// Initializes a new instance of the RoleService class.
        /// </summary>
        /// <param name="userManager">The UserManager for managing users.</param>
        /// <param name="roleManager">The RoleManager for managing roles.</param>
        /// <param name="logger">Logger for recording role operations and errors.</param>
        /// <param name="auditLogRepository">Repository used to record role changes in the audit log.</param>
        public RoleService(
            UserManager<ApplicationUser> userManager, 
            RoleManager<IdentityRole> roleManager,
            ILogger<RoleService> logger,
            IAuditLogRepository auditLogRepository)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _auditLogRepository = auditLogRepository;
        }

        /// <summary>
        /// Records a role change (or a blocked attempt) in the audit log under both the actor and the target user,
        /// without failing the operation if logging fails.
        /// </summary>
        /// <param name="actorUserId">The user performing the change.</param>
        /// <param name="target">The user whose role is changing.</param>
        /// <param name="roleName">The role added or removed.</param>
        /// <param name="operation">Whether the role is being added or removed.</param>
        /// <param name="blocked">True when the change was refused.</param>
        private async Task LogRoleChangeAsync(string actorUserId, ApplicationUser target, string roleName, OperationTypeEnum operation, bool blocked = false)
        {
            try
            {
                var actor = actorUserId == target.Id ? target : await _userManager.FindByIdAsync(actorUserId);
                var actorName = actor != null ? $"{actor.FirstName} {actor.LastName}".Trim() : string.Empty;
                if (string.IsNullOrEmpty(actorName))
                {
                    actorName = actorUserId;
                }

                var targetName = $"{target.FirstName} {target.LastName}".Trim();
                if (string.IsNullOrEmpty(targetName))
                {
                    targetName = target.Id;
                }

                var verb = operation == OperationTypeEnum.Add ? "added to" : "removed from";
                var description = blocked
                    ? $"Attempt by {actorName} to remove role '{roleName}' from {targetName} was blocked: last remaining system administrator."
                    : $"Role '{roleName}' was {verb} {targetName} by {actorName}.";

                var details = new SystemAdminChangeUserRoleDetails
                {
                    OperationType = operation.ToString(),
                    TargetUserId = target.Id,
                    TargetUserDisplayName = targetName,
                    RoleName = roleName,
                    ActorUserId = actorUserId,
                    ActorUserDisplayName = actorName,
                    Blocked = blocked,
                    Description = description
                };

                await _auditLogRepository.LogActionAsync(actorUserId, ActionCategory.SystemAdminChangeUserRole, details);

                // Also record the entry under the target so it shows in the target user's audit log
                if (actorUserId != target.Id)
                {
                    await _auditLogRepository.LogActionAsync(target.Id, ActionCategory.SystemAdminChangeUserRole, details);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit log for role {RoleName} change on user {UserId}", roleName, target.Id);
            }
        }

        /// <summary>
        /// Retrieves all available roles in the system.
        /// </summary>
        /// <returns>A list of role names.</returns>
        public async Task<List<string>> GetAllRolesAsync()
        {
            try
            {
                var roles = await _roleManager.Roles
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name!)
                    .ToListAsync();

                return roles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all roles");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all roles assigned to a specific user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A list of role names assigned to the user.</returns>
        public async Task<List<string>> GetUserRolesAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return new List<string>();
                }

                var roles = await _userManager.GetRolesAsync(user);
                return roles.OrderBy(r => r).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Adds a user to a specific role.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="roleName">The name of the role to add.</param>
        /// <param name="actorUserId">The identifier of the user performing the change.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public async Task<bool> AddUserToRoleAsync(string userId, string roleName, string actorUserId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return false;
                }

                // Check if role exists
                var roleExists = await _roleManager.RoleExistsAsync(roleName);
                if (!roleExists)
                {
                    _logger.LogWarning("Role does not exist: {RoleName}", roleName);
                    return false;
                }

                // Check if user is already in role
                var isInRole = await _userManager.IsInRoleAsync(user, roleName);
                if (isInRole)
                {
                    _logger.LogInformation("User {UserId} is already in role {RoleName}", userId, roleName);
                    return true;
                }

                var result = await _userManager.AddToRoleAsync(user, roleName);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Added user {UserId} to role {RoleName}", userId, roleName);
                    await LogRoleChangeAsync(actorUserId, user, roleName, OperationTypeEnum.Add);
                    return true;
                }

                _logger.LogWarning("Failed to add user {UserId} to role {RoleName}: {Errors}", 
                    userId, roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {UserId} to role {RoleName}", userId, roleName);
                throw;
            }
        }

        /// <summary>
        /// Removes a user from a specific role.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="roleName">The name of the role to remove.</param>
        /// <param name="actorUserId">The identifier of the user performing the change.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public async Task<bool> RemoveUserFromRoleAsync(string userId, string roleName, string actorUserId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return false;
                }

                // Check if user is in role
                var isInRole = await _userManager.IsInRoleAsync(user, roleName);
                if (!isInRole)
                {
                    _logger.LogInformation("User {UserId} is not in role {RoleName}", userId, roleName);
                    return true;
                }

                if (roleName == SystemAdministratorRole && await IsLastSystemAdministratorAsync(userId))
                {
                    _logger.LogWarning("Refused to remove {RoleName} from {UserId}: last remaining system administrator", roleName, userId);
                    await LogRoleChangeAsync(actorUserId, user, roleName, OperationTypeEnum.Remove, blocked: true);
                    return false;
                }

                var result = await _userManager.RemoveFromRoleAsync(user, roleName);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Removed user {UserId} from role {RoleName}", userId, roleName);
                    await LogRoleChangeAsync(actorUserId, user, roleName, OperationTypeEnum.Remove);
                    return true;
                }

                _logger.LogWarning("Failed to remove user {UserId} from role {RoleName}: {Errors}", 
                    userId, roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserId} from role {RoleName}", userId, roleName);
                throw;
            }
        }

        /// <summary>
        /// Determines whether the given user is the only remaining system administrator.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user is a system administrator and no other system administrator exists.</returns>
        public async Task<bool> IsLastSystemAdministratorAsync(string userId)
        {
            try
            {
                var admins = await _userManager.GetUsersInRoleAsync(SystemAdministratorRole);
                return admins.Count > 0 && admins.All(a => a.Id == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is the last system administrator", userId);
                throw;
            }
        }

        /// <summary>
        /// Checks if a role exists in the system.
        /// </summary>
        /// <param name="roleName">The name of the role to check.</param>
        /// <returns>True if the role exists; otherwise, false.</returns>
        public async Task<bool> RoleExistsAsync(string roleName)
        {
            try
            {
                return await _roleManager.RoleExistsAsync(roleName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if role exists: {RoleName}", roleName);
                throw;
            }
        }

        /// <summary>
        /// Checks if a user is in a specific role.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="roleName">The name of the role to check.</param>
        /// <returns>True if the user is in the role; otherwise, false.</returns>
        public async Task<bool> IsUserInRoleAsync(string userId, string roleName)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return false;
                }

                return await _userManager.IsInRoleAsync(user, roleName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is in role {RoleName}", userId, roleName);
                throw;
            }
        }

        /// <summary>
        /// Checks if a user is a system administrator.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user is a system administrator; otherwise, false.</returns>
        public async Task<bool> IsSystemAdministratorAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found when checking system administrator status: {UserId}", userId);
                    return false;
                }

                return await _userManager.IsInRoleAsync(user, "SystemAdministrator");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is a system administrator", userId);
                throw;
            }
        }
    }
}
