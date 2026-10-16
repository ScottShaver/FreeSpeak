using FluentAssertions;
using FreeSpeakWeb.Services;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="PlainTextExcerpt"/>.
    /// </summary>
    public class PlainTextExcerptTests
    {
        /// <summary>Tags are stripped and entities decoded.</summary>
        [Fact]
        public void Create_StripsTagsAndDecodesEntities()
        {
            PlainTextExcerpt.Create("<p>Hello &amp; <b>world</b></p>", 100).Should().Be("Hello & world");
        }

        /// <summary>Long text is truncated with an ellipsis.</summary>
        [Fact]
        public void Create_LongText_IsTruncated()
        {
            var result = PlainTextExcerpt.Create(new string('a', 50), 10);

            result.Should().HaveLength(10).And.EndWith("…");
        }

        /// <summary>Null or empty input yields an empty string.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_NullOrBlank_ReturnsEmpty(string? html)
        {
            PlainTextExcerpt.Create(html, 10).Should().BeEmpty();
        }
    }
}
