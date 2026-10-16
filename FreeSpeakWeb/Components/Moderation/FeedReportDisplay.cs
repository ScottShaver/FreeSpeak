using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Components.Moderation
{
    /// <summary>
    /// Maps feed report enums to localization keys and icons for the moderator dashboard components.
    /// </summary>
    public static class FeedReportDisplay
    {
        /// <summary>
        /// Gets the localization key for a report reason.
        /// </summary>
        /// <param name="reason">The report reason.</param>
        /// <returns>The resource key.</returns>
        public static string ReasonKey(ReportReason reason) => reason switch
        {
            ReportReason.ViolatesGroupRule => "ReasonViolatesGroupRule",
            ReportReason.Spam => "ReasonSpam",
            ReportReason.HarassmentOrBullying => "ReasonHarassment",
            ReportReason.HateSpeech => "ReasonHateSpeech",
            ReportReason.Misinformation => "ReasonMisinformation",
            ReportReason.AdultContent => "ReasonAdultContent",
            ReportReason.ViolenceOrGraphicContent => "ReasonViolence",
            ReportReason.IntellectualPropertyViolation => "ReasonCopyright",
            ReportReason.OffTopic => "ReasonOffTopic",
            ReportReason.PrivacyViolation => "ReasonPrivacy",
            ReportReason.SelfHarmOrSuicide => "ReasonSelfHarm",
            ReportReason.IllegalActivity => "ReasonIllegal",
            _ => "ReasonOther"
        };

        /// <summary>
        /// Gets the Bootstrap icon classes for a report reason.
        /// </summary>
        /// <param name="reason">The report reason.</param>
        /// <returns>The icon CSS classes.</returns>
        public static string ReasonIcon(ReportReason reason) => reason switch
        {
            ReportReason.ViolatesGroupRule => "bi bi-list-check",
            ReportReason.Spam => "bi bi-envelope-exclamation",
            ReportReason.HarassmentOrBullying => "bi bi-person-x",
            ReportReason.HateSpeech => "bi bi-chat-square-x",
            ReportReason.Misinformation => "bi bi-exclamation-diamond",
            ReportReason.AdultContent => "bi bi-eye-slash",
            ReportReason.ViolenceOrGraphicContent => "bi bi-shield-exclamation",
            ReportReason.IntellectualPropertyViolation => "bi bi-c-circle",
            ReportReason.OffTopic => "bi bi-chat-left-dots",
            ReportReason.PrivacyViolation => "bi bi-person-lock",
            ReportReason.SelfHarmOrSuicide => "bi bi-heart-pulse",
            ReportReason.IllegalActivity => "bi bi-slash-circle",
            ReportReason.Other => "bi bi-three-dots",
            _ => "bi bi-flag"
        };

        /// <summary>
        /// Gets the localization key for a report status.
        /// </summary>
        /// <param name="status">The report status.</param>
        /// <returns>The resource key.</returns>
        public static string StatusKey(ReportStatus status) => status switch
        {
            ReportStatus.NotReviewed => "StatusNotReviewed",
            ReportStatus.UnderReview => "StatusUnderReview",
            ReportStatus.ActionTaken => "StatusActionTaken",
            ReportStatus.Dismissed => "StatusDismissed",
            _ => "StatusResolved"
        };

        /// <summary>
        /// Gets the Bootstrap badge class for a report status.
        /// </summary>
        /// <param name="status">The report status.</param>
        /// <returns>The badge CSS class.</returns>
        public static string StatusBadge(ReportStatus status) => status switch
        {
            ReportStatus.NotReviewed => "bg-warning text-dark",
            ReportStatus.UnderReview => "bg-info text-dark",
            ReportStatus.ActionTaken => "bg-danger",
            ReportStatus.Dismissed => "bg-secondary",
            _ => "bg-success"
        };

        /// <summary>
        /// Formats a UTC timestamp as a localized relative time such as "3 d ago".
        /// </summary>
        /// <param name="utc">The UTC timestamp.</param>
        /// <param name="nowUtc">The current UTC time.</param>
        /// <param name="localizer">The localizer for the feed reports resource set.</param>
        /// <returns>The relative time text.</returns>
        public static string Ago(DateTime utc, DateTime nowUtc, Microsoft.Extensions.Localization.IStringLocalizer localizer)
        {
            var span = nowUtc - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            if (span.TotalMinutes < 1) return localizer["AgoJustNow"].Value;
            if (span.TotalHours < 1) return string.Format(localizer["AgoMinutes"].Value, (int)span.TotalMinutes);
            if (span.TotalDays < 1) return string.Format(localizer["AgoHours"].Value, (int)span.TotalHours);
            return string.Format(localizer["AgoDays"].Value, (int)span.TotalDays);
        }

        /// <summary>
        /// Formats a UTC timestamp as a local date and time for display.
        /// </summary>
        /// <param name="utc">The UTC timestamp.</param>
        /// <returns>The formatted local time.</returns>
        public static string Absolute(DateTime utc) =>
            DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("g");

        /// <summary>
        /// Gets the localization key for a failed report operation.
        /// </summary>
        /// <param name="error">The error.</param>
        /// <returns>The resource key.</returns>
        public static string ErrorKey(FeedReportError error) => error switch
        {
            FeedReportError.NotAuthorized or FeedReportError.NotAuthenticated => "ErrNotAuthorized",
            FeedReportError.AlreadyReviewed => "ErrAlreadyReviewed",
            FeedReportError.ReportNotFound or FeedReportError.ContentNotFound => "ErrNotFound",
            FeedReportError.OwnContent => "ErrOwnContent",
            FeedReportError.OwnReport => "ErrOwnReport",
            FeedReportError.RemovalFailed => "ErrRemovalFailed",
            FeedReportError.DescriptionTooLong => "ErrTooLong",
            _ => "ErrGeneric"
        };
    }
}
