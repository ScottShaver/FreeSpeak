using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Tests for the file-type icon selection in <see cref="GroupFileDto"/>.
    /// </summary>
    public class GroupFileDtoIconTests
    {
        private static GroupFileDto Make(string fileName, string contentType) =>
            new(1, 1, "u", "User", null, fileName, null, 10, contentType, DateTime.UtcNow, GroupFileStatus.Approved, 0, true, true);

        [Theory]
        [InlineData("report.pdf", "application/octet-stream", "bi-file-earmark-pdf")]
        [InlineData("notes.DOCX", "application/octet-stream", "bi-file-earmark-word")]
        [InlineData("data.xlsx", "application/octet-stream", "bi-file-earmark-excel")]
        [InlineData("pack.zip", "application/octet-stream", "bi-file-earmark-zip")]
        [InlineData("pic.png", "application/octet-stream", "bi-file-earmark-image")]
        public void FileIcon_UsesExtension(string name, string contentType, string expected)
        {
            Assert.Equal(expected, Make(name, contentType).FileIcon);
        }

        [Fact]
        public void FileIcon_FallsBackToContentTypeThenGeneric()
        {
            Assert.Equal("bi-file-image", Make("noext", "image/png").FileIcon);
            Assert.Equal("bi-file-earmark", Make("noext", "application/octet-stream").FileIcon);
        }
    }
}
