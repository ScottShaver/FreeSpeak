namespace FreeSpeakWeb.Services.Settings
{
    /// <summary>
    /// The outcome of a test email.
    /// </summary>
    /// <param name="Success">Whether the message was accepted by the SMTP server.</param>
    /// <param name="ErrorKey">The localization key of a sanitized failure reason, if any.</param>
    public sealed record TestEmailResult(bool Success, string? ErrorKey);

    /// <summary>
    /// Sends a test email using the current SMTP settings. Restricted to system administrators and
    /// limited to <see cref="EmailTestService.MaxTestsPerHour"/> sends per administrator per hour.
    /// </summary>
    public interface IEmailTestService
    {
        /// <summary>
        /// Sends a test email to <paramref name="recipient"/> using the current SMTP settings,
        /// regardless of the Enabled toggle, and audits the attempt.
        /// </summary>
        /// <param name="recipient">The recipient address.</param>
        /// <param name="adminUserId">The acting user.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The outcome; failure reasons are localization keys and never contain secrets.</returns>
        /// <exception cref="UnauthorizedAccessException">The user is not a system administrator.</exception>
        Task<TestEmailResult> SendTestEmailAsync(string? recipient, string adminUserId, CancellationToken cancellationToken = default);
    }
}
