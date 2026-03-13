using Microsoft.JSInterop;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service for managing application themes with localStorage persistence.
    /// Supports multiple themes and provides theme change notifications.
    /// </summary>
    public class ThemeService
    {
        private readonly IJSRuntime _jsRuntime;
        private string _currentTheme = "default";

        /// <summary>
        /// Event raised when the theme changes. Subscribe to update UI components.
        /// </summary>
        public event Action? OnThemeChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeService"/> class.
        /// </summary>
        /// <param name="jsRuntime">The JavaScript runtime for localStorage and DOM manipulation.</param>
        public ThemeService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Gets the current theme name
        /// </summary>
        public string CurrentTheme => _currentTheme;

        /// <summary>
        /// Gets all available themes
        /// </summary>
        public static Dictionary<string, string> AvailableThemes => new()
        {
            { "default", "Default" },
            { "dark", "Dark Mode" },
            { "light", "Light Mode" },
            { "ocean", "Ocean Blue" },
            { "forest", "Forest Green" },
            { "sunset", "Sunset Orange" },
            { "purple", "Purple Dream" },
            { "high-contrast", "High Contrast" }
        };

        /// <summary>
        /// Initializes the theme service by loading the saved theme from localStorage.
        /// Falls back to the default theme if no saved preference exists or an error occurs.
        /// </summary>
        /// <returns>A task representing the asynchronous initialization operation.</returns>
        public async Task InitializeAsync()
        {
            try
            {
                var savedTheme = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "selectedTheme");
                if (!string.IsNullOrEmpty(savedTheme) && AvailableThemes.ContainsKey(savedTheme))
                {
                    _currentTheme = savedTheme;
                    await ApplyThemeAsync(_currentTheme);
                }
            }
            catch
            {
                // If localStorage fails, use default theme
                _currentTheme = "default";
            }
        }

        /// <summary>
        /// Sets and applies a new theme, persisting the selection to localStorage.
        /// Raises the <see cref="OnThemeChanged"/> event on success.
        /// </summary>
        /// <param name="themeName">The name of the theme to apply. Must be a valid key in <see cref="AvailableThemes"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SetThemeAsync(string themeName)
        {
            if (!AvailableThemes.ContainsKey(themeName))
            {
                return;
            }

            _currentTheme = themeName;

            // Save to localStorage
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "selectedTheme", themeName);
            }
            catch
            {
                // Ignore localStorage errors
            }

            await ApplyThemeAsync(themeName);
            OnThemeChanged?.Invoke();
        }

        /// <summary>
        /// Applies a theme by setting the data-theme attribute on the document root element.
        /// </summary>
        /// <param name="themeName">The name of the theme to apply.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ApplyThemeAsync(string themeName)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("eval", $"document.documentElement.setAttribute('data-theme', '{themeName}')");
            }
            catch
            {
                // Ignore if JS runtime not available yet
            }
        }
    }
}
