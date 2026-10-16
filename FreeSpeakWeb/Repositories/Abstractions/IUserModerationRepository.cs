using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Repositories.Abstractions
{
    /// <summary>
    /// Data access for user warnings, suspensions and the user lookup used by system moderators.
    /// </summary>
    public interface IUserModerationRepository
    {
        /// <summary>
        /// Finds users by user name or name.
        /// </summary>
        /// <param name="term">The search text.</param>
        /// <param name="take">The maximum number of users to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Matching users ordered by user name.</returns>
        Task<IReadOnlyList<ModerationUserSummary>> SearchUsersAsync(string term, int take, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a user's identity summary.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The summary, or null when the user does not exist.</returns>
        Task<ModerationUserSummary?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a warning.
        /// </summary>
        /// <param name="warning">The warning to store.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stored warning.</returns>
        Task<UserWarning> AddWarningAsync(UserWarning warning, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a suspension record.
        /// </summary>
        /// <param name="suspension">The suspension to store.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stored suspension.</returns>
        Task<UserSuspension> AddSuspensionAsync(UserSuspension suspension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a suspension record, used to roll back a suspension whose sign-in block could not be applied.
        /// </summary>
        /// <param name="suspensionId">The suspension ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the record is removed.</returns>
        Task DeleteSuspensionAsync(int suspensionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a suspension record.
        /// </summary>
        /// <param name="suspensionId">The suspension ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The suspension, or null when it does not exist.</returns>
        Task<UserSuspension?> GetSuspensionAsync(int suspensionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a suspension lifted, only when it has not been lifted before.
        /// </summary>
        /// <param name="suspensionId">The suspension ID.</param>
        /// <param name="liftedByUserId">The user lifting it.</param>
        /// <param name="liftedAtUtc">The server-set UTC time.</param>
        /// <param name="note">An optional note.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when the suspension was lifted by this call.</returns>
        Task<bool> MarkSuspensionLiftedAsync(int suspensionId, string liftedByUserId, DateTime liftedAtUtc, string? note, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the most recent suspension of a user that has not been lifted and has not ended.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="nowUtc">The current UTC time.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The suspension, or null.</returns>
        Task<UserSuspension?> GetActiveSuspensionAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a user's warnings, newest first.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="take">The maximum number to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The warnings with the issuer's name.</returns>
        Task<IReadOnlyList<UserWarningItem>> GetWarningsAsync(string userId, int take, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a user's suspensions, newest first. <see cref="UserSuspensionItem.IsActive"/> and
        /// <see cref="UserSuspensionItem.CanLift"/> are left false; the service computes them.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="take">The maximum number to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The suspensions with the suspending moderator's name.</returns>
        Task<IReadOnlyList<UserSuspensionItem>> GetSuspensionsAsync(string userId, int take, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the feed content reports filed against a user's content, newest first.
        /// </summary>
        /// <param name="userId">The content author's ID.</param>
        /// <param name="take">The maximum number to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The reports.</returns>
        Task<IReadOnlyList<UserReportItem>> GetReportsAgainstUserAsync(string userId, int take, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records that the warned user acknowledged a warning, only the first time.
        /// </summary>
        /// <param name="warningId">The warning ID.</param>
        /// <param name="userId">The warned user's ID; the warning must belong to this user.</param>
        /// <param name="acknowledgedAtUtc">The server-set UTC time.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when the warning exists for the user (including when it was already acknowledged).</returns>
        Task<bool> AcknowledgeWarningAsync(int warningId, string userId, DateTime acknowledgedAtUtc, CancellationToken cancellationToken = default);
    }
}
