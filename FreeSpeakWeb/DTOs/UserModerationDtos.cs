using FreeSpeakWeb.Data;

namespace FreeSpeakWeb.DTOs
{
    /// <summary>
    /// The reasons a user moderation action (warn, suspend or lift) can be refused.
    /// </summary>
    public enum UserModerationError
    {
        /// <summary>No error.</summary>
        None = 0,

        /// <summary>The caller is not a system moderator or administrator.</summary>
        NotAuthorized,

        /// <summary>The target user does not exist.</summary>
        UserNotFound,

        /// <summary>A moderator cannot act on themselves.</summary>
        CannotModerateSelf,

        /// <summary>The target holds a system role the caller may not act against.</summary>
        CannotModerateStaff,

        /// <summary>A reason is required.</summary>
        ReasonRequired,

        /// <summary>The reason exceeds the maximum length.</summary>
        ReasonTooLong,

        /// <summary>The suspension length is not positive.</summary>
        InvalidDuration,

        /// <summary>The suspension is longer than a system moderator may issue.</summary>
        DurationExceedsLimit,

        /// <summary>Only administrators can suspend permanently.</summary>
        PermanentNotAllowed,

        /// <summary>The user is already suspended.</summary>
        AlreadySuspended,

        /// <summary>The suspension or warning does not exist.</summary>
        NotFound,

        /// <summary>The suspension is no longer active.</summary>
        NotActive,

        /// <summary>The caller may not lift this suspension.</summary>
        CannotLift,

        /// <summary>The operation failed unexpectedly.</summary>
        Failed
    }

    /// <summary>
    /// The outcome of a user moderation action.
    /// </summary>
    /// <param name="Success">True when the action was applied.</param>
    /// <param name="Error">The reason the action was refused, or <see cref="UserModerationError.None"/>.</param>
    /// <param name="RecordId">The ID of the created or changed warning or suspension.</param>
    public sealed record UserModerationResult(bool Success, UserModerationError Error, int? RecordId)
    {
        /// <summary>Creates a successful result.</summary>
        /// <param name="recordId">The warning or suspension ID.</param>
        /// <returns>The result.</returns>
        public static UserModerationResult Ok(int recordId) => new(true, UserModerationError.None, recordId);

        /// <summary>Creates a failed result.</summary>
        /// <param name="error">The reason.</param>
        /// <returns>The result.</returns>
        public static UserModerationResult Fail(UserModerationError error) => new(false, error, null);
    }

    /// <summary>
    /// A user found through the moderator user lookup.
    /// </summary>
    /// <param name="UserId">The user's ID.</param>
    /// <param name="UserName">The user name.</param>
    /// <param name="DisplayName">The first and last name.</param>
    public sealed record ModerationUserSummary(string UserId, string UserName, string DisplayName);

    /// <summary>
    /// A warning as shown in the user history.
    /// </summary>
    public sealed record UserWarningItem(
        int Id,
        string Reason,
        int? RelatedReportId,
        DateTime CreatedAt,
        DateTime? AcknowledgedAt,
        string IssuedByUserId,
        string IssuedByName);

    /// <summary>
    /// A suspension as shown in the user history.
    /// </summary>
    public sealed record UserSuspensionItem(
        int Id,
        string Reason,
        int? RelatedReportId,
        DateTime CreatedAt,
        DateTime? EndsAt,
        DateTime? LiftedAt,
        string? LiftNote,
        string SuspendedByUserId,
        string SuspendedByName,
        bool IsActive,
        bool CanLift);

    /// <summary>
    /// A report filed against a user's content, as shown in the user history.
    /// </summary>
    public sealed record UserReportItem(int ReportId, ReportReason Reason, ReportStatus Status, DateTime CreatedAt);

    /// <summary>
    /// The moderation history of one user, shown to moderators.
    /// </summary>
    /// <param name="User">The user.</param>
    /// <param name="Warnings">Warnings, newest first.</param>
    /// <param name="Suspensions">Suspensions, newest first.</param>
    /// <param name="Reports">Reports against the user's content, newest first.</param>
    /// <param name="IsSuspended">True when the user is currently suspended.</param>
    /// <param name="CanModerate">True when the caller may warn or suspend this user.</param>
    /// <param name="CanSuspendLong">True when the caller may suspend beyond the moderator limit or permanently.</param>
    /// <param name="MaxModeratorDays">The longest suspension, in days, a system moderator may issue.</param>
    public sealed record UserModerationHistory(
        ModerationUserSummary User,
        IReadOnlyList<UserWarningItem> Warnings,
        IReadOnlyList<UserSuspensionItem> Suspensions,
        IReadOnlyList<UserReportItem> Reports,
        bool IsSuspended,
        bool CanModerate,
        bool CanSuspendLong,
        int MaxModeratorDays);

    /// <summary>
    /// The active suspension of a user, shown to the suspended user at sign-in.
    /// </summary>
    /// <param name="Reason">The reason recorded by the moderator.</param>
    /// <param name="EndsAt">When the suspension ends (UTC), or null when permanent.</param>
    public sealed record ActiveSuspensionInfo(string Reason, DateTime? EndsAt)
    {
        /// <summary>Gets a value indicating whether the suspension has no end date.</summary>
        public bool IsPermanent => EndsAt == null;
    }
}
