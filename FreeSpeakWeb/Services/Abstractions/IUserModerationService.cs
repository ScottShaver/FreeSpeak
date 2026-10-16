using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Services.Abstractions
{
    /// <summary>
    /// Warnings, suspensions and the user history used by system moderators and administrators.
    /// Every method enforces the caller's role and the duration limits in the service itself.
    /// </summary>
    public interface IUserModerationService
    {
        /// <summary>
        /// Finds users by user name or name for the moderator user lookup.
        /// </summary>
        /// <param name="actorId">The acting user's ID; must be a system moderator or administrator.</param>
        /// <param name="term">The search text (at least two characters).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Matching users, or an empty list when the caller is not authorized or the term is too short.</returns>
        Task<IReadOnlyList<ModerationUserSummary>> SearchUsersAsync(string actorId, string term, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the warnings, suspensions and reports of a user, plus what the caller may do.
        /// </summary>
        /// <param name="actorId">The acting user's ID; must be a system moderator or administrator.</param>
        /// <param name="userId">The user whose history is requested.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The history, or null when the caller is not authorized or the user does not exist.</returns>
        Task<UserModerationHistory?> GetUserHistoryAsync(string actorId, string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Issues a warning and notifies the user.
        /// </summary>
        /// <param name="actorId">The acting moderator or administrator.</param>
        /// <param name="targetUserId">The user to warn.</param>
        /// <param name="reason">The reason shown to the user.</param>
        /// <param name="relatedReportId">The feed report that led to the warning, if any.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome; <see cref="UserModerationResult.RecordId"/> is the warning ID.</returns>
        Task<UserModerationResult> WarnUserAsync(string actorId, string targetUserId, string reason, int? relatedReportId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Suspends a user. System moderators are limited to <c>Moderation:MaxModeratorSuspensionDays</c>; only
        /// administrators may suspend for longer or permanently.
        /// </summary>
        /// <param name="actorId">The acting moderator or administrator.</param>
        /// <param name="targetUserId">The user to suspend.</param>
        /// <param name="reason">The reason shown to the user at sign-in.</param>
        /// <param name="duration">The suspension length, or null for a permanent suspension.</param>
        /// <param name="relatedReportId">The feed report that led to the suspension, if any.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome; <see cref="UserModerationResult.RecordId"/> is the suspension ID.</returns>
        Task<UserModerationResult> SuspendUserAsync(string actorId, string targetUserId, string reason, TimeSpan? duration, int? relatedReportId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lifts an active suspension. Administrators can lift any suspension; moderators only the ones they issued.
        /// </summary>
        /// <param name="actorId">The acting moderator or administrator.</param>
        /// <param name="suspensionId">The suspension to lift.</param>
        /// <param name="note">An optional note.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The outcome.</returns>
        Task<UserModerationResult> LiftSuspensionAsync(string actorId, int suspensionId, string? note, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the suspension that currently blocks a user, used at sign-in and for session enforcement.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The active suspension, or null when the user is not suspended.</returns>
        Task<ActiveSuspensionInfo?> GetActiveSuspensionAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records that a user acknowledged one of their warnings.
        /// </summary>
        /// <param name="userId">The warned user's ID.</param>
        /// <param name="warningId">The warning ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when the warning belongs to the user.</returns>
        Task<bool> AcknowledgeWarningAsync(string userId, int warningId, CancellationToken cancellationToken = default);
    }
}
