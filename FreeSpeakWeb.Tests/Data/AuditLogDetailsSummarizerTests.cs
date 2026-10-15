using FreeSpeakWeb.Data.AuditLogDetails;

namespace FreeSpeakWeb.Tests.Data
{
    /// <summary>
    /// Tests for <see cref="AuditLogDetailsSummarizer"/>.
    /// </summary>
    public class AuditLogDetailsSummarizerTests
    {
        [Fact]
        public void Summarize_UsesDescriptionWhenPresent()
        {
            var result = AuditLogDetailsSummarizer.Summarize("{\"operationType\":\"Update\",\"description\":\"Changed role to Admin\"}");

            Assert.Equal("Changed role to Admin", result);
        }

        [Fact]
        public void Summarize_FallsBackToOperationAndScalarsAndHidesClientInfo()
        {
            var result = AuditLogDetailsSummarizer.Summarize("{\"operationType\":\"Create\",\"ipAddress\":\"1.2.3.4\",\"userAgent\":\"x\",\"postId\":5,\"visibility\":\"Public\"}");

            Assert.Equal("Create, postId: 5, visibility: Public", result);
        }

        [Fact]
        public void Summarize_HandlesEmptyAndInvalidJson()
        {
            Assert.Equal(string.Empty, AuditLogDetailsSummarizer.Summarize(""));
            Assert.Equal("not json", AuditLogDetailsSummarizer.Summarize("not json"));
        }
    }
}
