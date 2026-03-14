namespace FreeSpeakWeb;

/// <summary>
/// Configuration settings for initializing the system administrator user account during application startup.
/// These values are typically bound from the "SystemAdministratorInitInfo" section in appsettings.json.
/// </summary>
public class SystemAdministratorInitInfo
{
    /// <summary>
    /// Gets or sets the username for the system administrator account.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address for the system administrator account.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password for the system administrator account.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the phone number for the system administrator account.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the first name for the system administrator account.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last name for the system administrator account.
    /// </summary>
    public string LastName { get; set; } = string.Empty;
}
