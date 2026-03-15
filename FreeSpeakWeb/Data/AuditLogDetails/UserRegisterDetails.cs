namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user registration audit log entries.
    /// Tracks new user account creation events.
    /// </summary>
    public class UserRegisterDetails
    {
        /// <summary>
        /// Gets or sets the registration method used.
        /// Examples: "Local", "Google", "Microsoft", "External".
        /// </summary>
        public string RegistrationMethod { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the email address used for registration.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the username chosen during registration.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the IP address from which the registration occurred.
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether email confirmation is required.
        /// </summary>
        public bool RequiresEmailConfirmation { get; set; }
    }
}
