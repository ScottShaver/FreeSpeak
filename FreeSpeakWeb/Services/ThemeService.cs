using Microsoft.JSInterop;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service for managing application themes
    /// </summary>
    public class ThemeService
    {
        private readonly IJSRuntime _jsRuntime;
        private string _currentTheme = "default";

        public event Action? OnThemeChanged;

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
        /// Initialize theme from localStorage
        /// </summary>
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
        /// Set and apply a new theme
        /// </summary>
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
        /// Apply theme by setting data attribute on document
        /// </summary>
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
