namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Contains details for user login audit log entries.
    /// Tracks successful user authentication events.
    /// </summary>
    public class UserLoginDetails : AuditLogDetailsBase
    {
        /// <summary>
        /// Gets or sets the authentication method used for login.
        /// Examples: "Password", "Google", "Microsoft", "TwoFactor".
        /// </summary>
        public string AuthenticationMethod { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this login was from a new device.
        /// </summary>
        public bool IsNewDevice { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user selected "Remember Me".
        /// </summary>
        public bool RememberMe { get; set; }
    }
}
