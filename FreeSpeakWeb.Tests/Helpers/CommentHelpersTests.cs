using FluentAssertions;
using FreeSpeakWeb.Components.SocialFeed;
using FreeSpeakWeb.Helpers;
using Xunit;

namespace FreeSpeakWeb.Tests.Helpers;

public class CommentHelpersTests
{
    #region GetInitials Tests

    [Theory]
    [InlineData(null, "U")]
    [InlineData("", "U")]
    [InlineData("   ", "U")]
    public void GetInitials_WithNullOrEmpty_ShouldReturnU(string? name, string expected)
    {
        // Act
        var result = CommentHelpers.GetInitials(name);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("John Doe", "JD")]
    [InlineData("Jane Smith", "JS")]
    [InlineData("Mary Anne Johnson", "MJ")]  // First and last initials
    public void GetInitials_WithFullName_ShouldReturnFirstAndLastInitials(string name, string expected)
    {
        // Act
        var result = CommentHelpers.GetInitials(name);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("John", "JO")]
    [InlineData("AB", "AB")]
    [InlineData("A", "A")]
    public void GetInitials_WithSingleWord_ShouldReturnUpToTwoCharacters(string name, string expected)
    {
        // Act
        var result = CommentHelpers.GetInitials(name);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetInitials_ShouldReturnUppercase()
    {
        // Arrange
        var name = "john doe";

        // Act
        var result = CommentHelpers.GetInitials(name);

        // Assert
        result.Should().Be("JD");
    }

    [Fact]
    public void GetInitials_WithExtraSpaces_ShouldTrimAndParse()
    {
        // Arrange
        var name = "  John   Doe  ";

        // Act
        var result = CommentHelpers.GetInitials(name);

        // Assert
        result.Should().Be("JD");
    }

    #endregion

    #region FindCommentById Tests

    [Fact]
    public void FindCommentById_WithMatchingId_ShouldReturnComment()
    {
        // Arrange
        var comments = new List<CommentDisplayModel>
        {
            new CommentDisplayModel { CommentId = 1, CommentText = "Comment 1" },
            new CommentDisplayModel { CommentId = 2, CommentText = "Comment 2" },
            new CommentDisplayModel { CommentId = 3, CommentText = "Comment 3" }
        };

        // Act
        var result = CommentHelpers.FindCommentById(comments, 2);

        // Assert
        result.Should().NotBeNull();
        result!.CommentId.Should().Be(2);
        result.CommentText.Should().Be("Comment 2");
    }

    [Fact]
    public void FindCommentById_WithNoMatch_ShouldReturnNull()
    {
        // Arrange
        var comments = new List<CommentDisplayModel>
        {
            new CommentDisplayModel { CommentId = 1, CommentText = "Comment 1" },
            new CommentDisplayModel { CommentId = 2, CommentText = "Comment 2" }
        };

        // Act
        var result = CommentHelpers.FindCommentById(comments, 999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FindCommentById_WithNestedReply_ShouldFindRecursively()
    {
        // Arrange
        var comments = new List<CommentDisplayModel>
        {
            new CommentDisplayModel
            {
                CommentId = 1,
                CommentText = "Parent",
                Replies = new List<CommentDisplayModel>
                {
                    new CommentDisplayModel
                    {
                        CommentId = 2,
                        CommentText = "Reply level 1",
                        Replies = new List<CommentDisplayModel>
                        {
                            new CommentDisplayModel
                            {
                                CommentId = 3,
                                CommentText = "Reply level 2"
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = CommentHelpers.FindCommentById(comments, 3);

        // Assert
        result.Should().NotBeNull();
        result!.CommentId.Should().Be(3);
        result.CommentText.Should().Be("Reply level 2");
    }

    [Fact]
    public void FindCommentById_WithEmptyList_ShouldReturnNull()
    {
        // Arrange
        var comments = new List<CommentDisplayModel>();

        // Act
        var result = CommentHelpers.FindCommentById(comments, 1);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region FindPostIdForComment Tests

    [Fact]
    public void FindPostIdForComment_WithMatchingComment_ShouldReturnPostId()
    {
        // Arrange
        var postComments = new Dictionary<int, List<CommentDisplayModel>>
        {
            { 100, new List<CommentDisplayModel> { new CommentDisplayModel { CommentId = 1 } } },
            { 200, new List<CommentDisplayModel> { new CommentDisplayModel { CommentId = 2 } } },
            { 300, new List<CommentDisplayModel> { new CommentDisplayModel { CommentId = 3 } } }
        };

        // Act
        var result = CommentHelpers.FindPostIdForComment(postComments, 2);

        // Assert
        result.Should().Be(200);
    }

    [Fact]
    public void FindPostIdForComment_WithNoMatch_ShouldReturnNull()
    {
        // Arrange
        var postComments = new Dictionary<int, List<CommentDisplayModel>>
        {
            { 100, new List<CommentDisplayModel> { new CommentDisplayModel { CommentId = 1 } } }
        };

        // Act
        var result = CommentHelpers.FindPostIdForComment(postComments, 999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FindPostIdForComment_WithNestedComment_ShouldFindRecursively()
    {
        // Arrange
        var postComments = new Dictionary<int, List<CommentDisplayModel>>
        {
            {
                100,
                new List<CommentDisplayModel>
                {
                    new CommentDisplayModel
                    {
                        CommentId = 1,
                        Replies = new List<CommentDisplayModel>
                        {
                            new CommentDisplayModel { CommentId = 10 }
                        }
                    }
                }
            }
        };

        // Act
        var result = CommentHelpers.FindPostIdForComment(postComments, 10);

        // Assert
        result.Should().Be(100);
    }

    #endregion

    #region FormatRelativeTimestamp Tests

    [Fact]
    public void FormatRelativeTimestamp_JustNow_ShouldReturnJustNow()
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddSeconds(-30);

        // Act
        var result = CommentHelpers.FormatRelativeTimestamp(timestamp);

        // Assert
        result.Should().Be("just now");
    }

    [Theory]
    [InlineData(-1, "1m ago")]
    [InlineData(-5, "5m ago")]
    [InlineData(-59, "59m ago")]
    public void FormatRelativeTimestamp_Minutes_ShouldFormatCorrectly(int minutes, string expected)
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddMinutes(minutes);

        // Act
        var result = CommentHelpers.FormatRelativeTimestamp(timestamp);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, "1h ago")]
    [InlineData(-5, "5h ago")]
    [InlineData(-23, "23h ago")]
    public void FormatRelativeTimestamp_Hours_ShouldFormatCorrectly(int hours, string expected)
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddHours(hours);

        // Act
        var result = CommentHelpers.FormatRelativeTimestamp(timestamp);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, "1d ago")]
    [InlineData(-5, "5d ago")]
    [InlineData(-6, "6d ago")]
    public void FormatRelativeTimestamp_Days_ShouldFormatCorrectly(int days, string expected)
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddDays(days);

        // Act
        var result = CommentHelpers.FormatRelativeTimestamp(timestamp);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatRelativeTimestamp_Weeks_ShouldFormatCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddDays(-14);

        // Act
        var result = CommentHelpers.FormatRelativeTimestamp(timestamp);

        // Assert
        result.Should().Be("2w ago");
    }

    [Fact]
    public void FormatRelativeTimestamp_Months_ShouldFormatCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddDays(-60);

        // Act
        var result = CommentHelpers.FormatRelativeTimestamp(timestamp);

        // Assert
        result.Should().Be("2mo ago");
    }

    [Fact]
    public void FormatRelativeTimestamp_Years_ShouldFormatCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddDays(-400);

        // Act
        var result = CommentHelpers.FormatRelativeTimestamp(timestamp);

        // Assert
        result.Should().Be("1y ago");
    }

    #endregion
}
