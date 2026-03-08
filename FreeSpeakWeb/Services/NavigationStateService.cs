namespace FreeSpeakWeb.Services;

public class NavigationStateService : IDisposable
{
    private string _activeMenuItem = string.Empty;
    private readonly Microsoft.AspNetCore.Components.NavigationManager _navigationManager;

    public event Action? OnChange;

    public NavigationStateService(Microsoft.AspNetCore.Components.NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;

        // Listen to navigation changes
        _navigationManager.LocationChanged += OnLocationChanged;

        // Initialize based on current URL
        UpdateActiveMenuItemFromUrl();
    }

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

    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        UpdateActiveMenuItemFromUrl();
        NotifyStateChanged();
    }

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

    private void NotifyStateChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        _navigationManager.LocationChanged -= OnLocationChanged;
    }
}
