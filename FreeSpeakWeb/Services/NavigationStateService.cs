namespace FreeSpeakWeb.Services;

/// <summary>
/// Service for tracking and managing navigation state across the application.
/// Monitors URL changes and updates the active menu item accordingly.
/// </summary>
public class NavigationStateService : IDisposable
{
    private string _activeMenuItem = string.Empty;
    private readonly Microsoft.AspNetCore.Components.NavigationManager _navigationManager;

    /// <summary>
    /// Event raised when the navigation state changes.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationStateService"/> class.
    /// </summary>
    /// <param name="navigationManager">The navigation manager for URL tracking.</param>
    public NavigationStateService(Microsoft.AspNetCore.Components.NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;

        // Listen to navigation changes
        _navigationManager.LocationChanged += OnLocationChanged;

        // Initialize based on current URL
        UpdateActiveMenuItemFromUrl();
    }

    /// <summary>
    /// Gets or sets the active menu item identifier.
    /// Setting this property triggers the OnChange event.
    /// </summary>
    public string ActiveMenuItem
    {
        get => _activeMenuItem;
        set
        {
            if (_activeMenuItem != value)
            {
                _activeMenuItem = value;
                NotifyStateChanged();
            }
        }
    }

    /// <summary>
    /// Handles navigation location changes.
    /// Updates the active menu item based on the new URL.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The location changed event arguments.</param>
    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        UpdateActiveMenuItemFromUrl();
        NotifyStateChanged();
    }

    /// <summary>
    /// Updates the active menu item based on the current URL.
    /// Maps URL paths to menu item identifiers.
    /// </summary>
    private void UpdateActiveMenuItemFromUrl()
    {
        var uri = _navigationManager.ToBaseRelativePath(_navigationManager.Uri);

        // Remove query string if present
        var path = uri.Split('?')[0].TrimStart('/');

        // Ignore not-found page (Blazor 404) - don't update menu highlighting for it
        if (path.Equals("not-found", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Map URL paths to menu item identifiers
        if (string.IsNullOrEmpty(path))
        {
            _activeMenuItem = "/";
        }
        else if (path.Equals("Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            _activeMenuItem = "/Account/Login";
        }
        else if (path.Equals("Account/Register", StringComparison.OrdinalIgnoreCase))
        {
            _activeMenuItem = "/Account/Register";
        }
        else if (path.Equals("Account/Manage", StringComparison.OrdinalIgnoreCase))
        {
            _activeMenuItem = "/Account/Manage";
        }
        else if (path.Equals("friends", StringComparison.OrdinalIgnoreCase))
        {
            _activeMenuItem = "friends";
        }
        else if (path.Equals("technology-credits", StringComparison.OrdinalIgnoreCase))
        {
            _activeMenuItem = "technology-credits";
        }
        else
        {
            _activeMenuItem = "/" + path;
        }
    }

    /// <summary>
    /// Notifies subscribers that the navigation state has changed.
    /// </summary>
    private void NotifyStateChanged() => OnChange?.Invoke();

    /// <summary>
    /// Disposes the service and unsubscribes from navigation events.
    /// </summary>
    public void Dispose()
    {
        _navigationManager.LocationChanged -= OnLocationChanged;
    }
}
