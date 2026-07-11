namespace FreeSpeakWeb;

/// <summary>
/// Configuration settings for SMTP email sending.
/// Used for sending notifications, account management emails, and other system emails.
/// </summary>
public class SmtpSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Smtp";

    /// <summary>
    /// Gets or sets the SMTP server hostname or IP address.
    /// Example: smtp.gmail.com, smtp.office365.com, or localhost.
    /// </summary>
    public string Server { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the SMTP server port number.
    /// Common ports: 25 (unencrypted), 587 (STARTTLS), 465 (SSL).
    /// Default: 587
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// Gets or sets the display name for the email sender.
    /// This appears as the sender's name in received emails.
    /// </summary>
    public string SenderName { get; set; } = "FreeSpeak System";

    /// <summary>
    /// Gets or sets the email address that appears in the "From" field.
    /// This should be a valid email address authorized to send from the SMTP server.
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether SMTP authentication is required.
    /// </summary>
    public bool AuthenticationRequired { get; set; } = true;

    /// <summary>
    /// Gets or sets the SMTP account username for authentication.
    /// Often this is the same as the sender email address.
    /// </summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SMTP account password for authentication.
    /// For security, consider using user secrets or environment variables in production.
    /// If using Gmail with 2FA, this should be an App Password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to use SSL for the SMTP connection.
    /// </summary>
    public bool UseSsl { get; set; } = true;
}
