using FluentAssertions;

namespace FreeSpeakWeb.Tests.Components.Layout;

/// <summary>
/// Guards the nav menu layout rules that keep icons and expander chevrons aligned
/// regardless of the label length or culture.
/// </summary>
public class NavMenuLayoutTests
{
    /// <summary>
    /// Locates the FreeSpeakWeb layout folder by walking up from the test output directory.
    /// </summary>
    /// <returns>The absolute path of the Components/Layout folder.</returns>
    private static string FindLayoutDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "FreeSpeakWeb", "Components", "Layout");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate FreeSpeakWeb/Components/Layout.");
    }

    /// <summary>
    /// Icons must reset the inherited nav-link line-height so font glyphs are not pushed below their box.
    /// </summary>
    [Fact]
    public void NavMenuCss_IconRule_ResetsLineHeight()
    {
        // Arrange
        var css = File.ReadAllText(Path.Combine(FindLayoutDirectory(), "NavMenu.razor.css"));

        // Act
        var start = css.IndexOf(".bi {", StringComparison.Ordinal);
        var block = css.Substring(start, css.IndexOf('}', start) - start);

        // Assert
        block.Should().Contain("line-height: 1");
    }

    /// <summary>
    /// The expander label must not wrap so the chevron stays on the same row for long translations.
    /// </summary>
    [Fact]
    public void NavMenu_ExpanderLabelAndChevron_UseNonWrappingClasses()
    {
        // Arrange
        var css = File.ReadAllText(Path.Combine(FindLayoutDirectory(), "NavMenu.razor.css"));
        var razor = File.ReadAllText(Path.Combine(FindLayoutDirectory(), "NavMenu.razor"));

        // Assert
        css.Should().Contain(".nav-expander-label");
        css.Should().Contain("white-space: nowrap");
        css.Should().Contain(".nav-expander-chevron");
        razor.Should().Contain("nav-expander-label");
        razor.Should().Contain("nav-expander-chevron");
    }
}
