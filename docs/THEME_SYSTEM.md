# Theme System Implementation Guide

## Overview

FreeSpeak now features a comprehensive theme system with 8 beautiful color schemes. Users can instantly switch between themes using the theme selector in the upper left corner of the navigation bar.

## Available Themes

### 1. Default Theme
- **Primary**: Blue gradient (original FreeSpeak colors)
- **Best for**: General use, professional appearance
- **Colors**: Deep blue to purple gradient navigation

### 2. Dark Mode
- **Primary**: Dark surfaces with bright blue accents
- **Best for**: Low-light environments, reducing eye strain
- **Colors**: Charcoal backgrounds, bright text

### 3. Light Mode  
- **Primary**: Clean white/light gray
- **Best for**: Bright environments, maximum readability
- **Colors**: White surfaces, subtle gray accents

### 4. Ocean Blue
- **Primary**: Cyan and teal tones
- **Best for**: Calming, professional ocean vibe
- **Colors**: Turquoise accents, light blue backgrounds

### 5. Forest Green
- **Primary**: Natural earth tones
- **Best for**: Organic, eco-friendly aesthetic
- **Colors**: Green gradient, natural tones

### 6. Sunset Orange
- **Primary**: Warm oranges and ambers
- **Best for**: Energetic, creative atmosphere
- **Colors**: Orange gradient, warm earth tones

### 7. Purple Dream
- **Primary**: Rich purple gradients
- **Best for**: Creative, artistic feel
- **Colors**: Deep purple to lavender

### 8. High Contrast
- **Primary**: Maximum contrast for accessibility
- **Best for**: Visually impaired users, WCAG compliance
- **Colors**: Pure black/white with vibrant accents

## Architecture

### CSS Custom Properties (CSS Variables)

Each theme defines a consistent set of CSS variables:

```css
:root, [data-theme="default"] {
    /* Primary colors */
    --primary-color: #0d6efd;
    --primary-hover: #0b5ed7;
    
    /* Semantic colors */
    --secondary-color: #6c757d;
    --success-color: #198754;
    --danger-color: #dc3545;
    --warning-color: #ffc107;
    --info-color: #0dcaf0;
    
    /* Surface colors */
    --surface-color: #ffffff;
    --surface-secondary: #f8f9fa;
    --surface-hover: #f2f2f2;
    
    /* Text colors */
    --text-primary: #050505;
    --text-secondary: #65676b;
    --text-muted: #8a8d91;
    
    /* Borders and effects */
    --border-color: #e4e6eb;
    --hover-overlay: rgba(0, 0, 0, 0.05);
    
    /* Navigation gradient */
    --nav-gradient-start: rgb(5, 39, 103);
    --nav-gradient-end: #3a0647;
    
    /* Shadows */
    --shadow-sm: 0 1px 3px rgba(0, 0, 0, 0.12);
    --shadow-md: 0 4px 6px rgba(0, 0, 0, 0.1);
    --shadow-lg: 0 10px 15px rgba(0, 0, 0, 0.1);
}
```

### Service Layer

**ThemeService.cs** manages theme state:

```csharp
public class ThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private string _currentTheme = "default";
    
    public event Action? OnThemeChanged;
    
    public string CurrentTheme => _currentTheme;
    
    public static Dictionary<string, string> AvailableThemes { get; }
    
    public Task InitializeAsync(); // Load from localStorage
    public Task SetThemeAsync(string themeName); // Apply theme
}
```

### UI Components

**ThemeSelector.razor** - Dropdown menu component
- Sun icon button in navigation bar
- Dropdown shows all themes with color previews
- Active theme indicated with checkmark
- Smooth animations and transitions

## File Structure

```
FreeSpeakWeb/
├── Services/
│   └── ThemeService.cs                      # Theme state management
├── Components/
│   └── Shared/
│       ├── ThemeSelector.razor              # Theme picker UI
│       └── ThemeSelector.razor.css          # Theme picker styles
├── wwwroot/
│   └── css/
│       ├── themes.css                       # All 8 theme definitions
│       └── theme-bootstrap.css              # Bootstrap component theming
└── Components/
    ├── App.razor                            # CSS references
    └── Layout/
        └── NavMenu.razor                    # Theme selector placement
```

## Usage

### For Users

1. **Click** the sun icon ☀️ in the upper left corner
2. **Select** your preferred theme from the dropdown
3. **Theme applies instantly** with smooth 0.3s transitions
4. **Preference saved** to browser localStorage
5. **Persists across sessions** automatically

### For Developers

#### Using Theme Variables in CSS

```css
.my-component {
    background-color: var(--surface-color);
    color: var(--text-primary);
    border: 1px solid var(--border-color);
    box-shadow: var(--shadow-md);
}

.my-component:hover {
    background-color: var(--surface-hover);
}

.my-button {
    background-color: var(--primary-color);
    color: white;
}

.my-button:hover {
    background-color: var(--primary-hover);
}
```

#### Using color-mix for Dynamic Colors

```css
.header {
    /* Mix primary color with surface-secondary */
    background: linear-gradient(180deg, 
        color-mix(in srgb, var(--primary-color) 20%, var(--surface-secondary)), 
        color-mix(in srgb, var(--primary-color) 40%, var(--surface-secondary)) 70%);
}

.alert-custom {
    background-color: color-mix(in srgb, var(--primary-color) 15%, var(--surface-color));
    border-color: color-mix(in srgb, var(--primary-color) 30%, var(--border-color));
}
```

#### Accessing Theme Service in Components

```csharp
@inject ThemeService ThemeService

@code {
    protected override void OnInitialized()
    {
        ThemeService.OnThemeChanged += StateHasChanged;
    }
    
    private async Task CustomThemeLogic()
    {
        var currentTheme = ThemeService.CurrentTheme;
        
        if (currentTheme == "dark")
        {
            // Do something special for dark mode
        }
    }
    
    public void Dispose()
    {
        ThemeService.OnThemeChanged -= StateHasChanged;
    }
}
```

## Adding a New Theme

### Step 1: Define CSS Variables

Add a new theme block to `wwwroot/css/themes.css`:

```css
[data-theme="my-theme"] {
    --primary-color: #your-primary-color;
    --primary-hover: #your-hover-color;
    --secondary-color: #your-secondary;
    --success-color: #your-success;
    --danger-color: #your-danger;
    --warning-color: #your-warning;
    --info-color: #your-info;
    
    --surface-color: #your-surface;
    --surface-secondary: #your-surface-alt;
    --surface-hover: #your-hover-bg;
    
    --text-primary: #your-text;
    --text-secondary: #your-text-muted;
    --text-muted: #your-text-very-muted;
    
    --border-color: #your-border;
    --hover-overlay: rgba(your-rgb, 0.1);
    
    --nav-gradient-start: #your-nav-start;
    --nav-gradient-end: #your-nav-end;
    
    --shadow-sm: 0 1px 3px rgba(0, 0, 0, 0.X);
    --shadow-md: 0 4px 6px rgba(0, 0, 0, 0.X);
    --shadow-lg: 0 10px 15px rgba(0, 0, 0, 0.X);
}
```

### Step 2: Register in ThemeService

Add to `Services/ThemeService.cs`:

```csharp
public static Dictionary<string, string> AvailableThemes => new()
{
    // ... existing themes ...
    { "my-theme", "My Theme Name" }
};
```

### Step 3: Add Color Preview

Add to `Components/Shared/ThemeSelector.razor.css`:

```css
[data-theme-preview="my-theme"] {
    background: linear-gradient(135deg, #your-start 0%, #your-end 100%);
}
```

## Themed Components

All Bootstrap components automatically adapt to the active theme via `theme-bootstrap.css`:

- ✅ Buttons (all variants)
- ✅ Forms (inputs, selects, checkboxes)
- ✅ Cards
- ✅ Modals
- ✅ Dropdowns
- ✅ Alerts
- ✅ Badges
- ✅ Pagination
- ✅ Nav tabs/pills
- ✅ Tables
- ✅ List groups
- ✅ Breadcrumbs
- ✅ Progress bars
- ✅ Tooltips
- ✅ Popovers
- ✅ Accordions

Custom components themed:

- ✅ Navigation bar
- ✅ Feed articles
- ✅ Comments
- ✅ Post detail modal
- ✅ Notifications
- ✅ Theme selector itself

## Best Practices

### DO:
✅ Use CSS variables for all colors  
✅ Test your component in all 8 themes  
✅ Use `color-mix()` for dynamic shades  
✅ Provide proper contrast ratios  
✅ Use semantic color variables (`--success-color`, `--danger-color`)  

### DON'T:
❌ Hardcode color values (`#ffffff`, `rgb(0,0,0)`)  
❌ Use inline styles with hardcoded colors  
❌ Override theme variables in component CSS  
❌ Assume a specific theme is active  
❌ Use JavaScript to apply colors (use CSS variables)  

## Accessibility

### High Contrast Theme
- Specifically designed for WCAG compliance
- Pure black (#000) on white (#fff)
- Maximum contrast ratios
- Highly visible borders and focus states

### All Themes
- Tested for minimum 4.5:1 contrast ratio
- Focus indicators use theme-appropriate colors
- Hover states clearly visible
- Text remains readable in all themes

## Performance

### CSS Variables
- Near-zero performance impact
- Instant theme switching (0.3s transition)
- No JavaScript color calculations
- Browser-native implementation

### LocalStorage
- Theme preference saved to `localStorage`
- Loads on app initialization
- Survives page refreshes and sessions
- Falls back gracefully if unavailable

## Browser Support

**Full Support:**
- Chrome/Edge 88+
- Firefox 87+
- Safari 15.4+
- Opera 74+

**Partial Support (colors-mix):**
- Chrome/Edge 111+
- Firefox 113+  
- Safari 16.2+

Fallback: Themes work without `color-mix()`, but some dynamic shades use solid colors.

## Troubleshooting

### Theme not applying
1. Check browser console for CSS errors
2. Verify `data-theme` attribute on `<html>` element
3. Clear browser cache
4. Check themes.css is loaded (Network tab)

### Colors look wrong
1. Verify CSS variable names match exactly
2. Check for hardcoded colors overriding variables
3. Inspect element to see computed values
4. Test in incognito mode (extensions can interfere)

### LocalStorage not persisting
1. Check browser privacy settings
2. Verify localStorage is enabled
3. Check for browser extensions blocking storage
4. Test in private/incognito mode

## Future Enhancements

### Planned Features
- [ ] User-created custom themes
- [ ] Theme import/export (JSON)
- [ ] Auto dark mode (system preference)
- [ ] Scheduled theme switching (day/night)
- [ ] Per-user theme settings (server-side)
- [ ] Theme preview before applying
- [ ] More themes (Nord, Dracula, Solarized, etc.)

### Optimization Opportunities
- [ ] CSS variables fallback for old browsers
- [ ] Preload theme based on localStorage
- [ ] Theme transition animations
- [ ] Reduced motion respect
- [ ] Print-specific theme

## Migration Guide

### Updating Existing Components

**Before:**
```css
.my-component {
    background-color: #f8f9fa;
    color: #000000;
    border: 1px solid #dee2e6;
}
```

**After:**
```css
.my-component {
    background-color: var(--surface-secondary);
    color: var(--text-primary);
    border: 1px solid var(--border-color);
}
```

### Component-Specific Overrides

If you need theme-specific behavior:

```css
/* Default for all themes */
.special-component {
    background: var(--surface-color);
}

/* Override for dark theme only */
[data-theme="dark"] .special-component {
    background: linear-gradient(var(--nav-gradient-start), var(--nav-gradient-end));
}

/* Override for high contrast only */
[data-theme="high-contrast"] .special-component {
    border: 3px solid var(--border-color);
}
```

---

**Last Updated**: January 2025  
**Version**: 1.0  
**Author**: FreeSpeak Development Team
