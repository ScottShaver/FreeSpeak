using System.ComponentModel.DataAnnotations;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a report submitted by a user against a feed post or feed comment.
    /// Exactly one of <see cref="PostId"/> or <see cref="CommentId"/> is set.
    /// Feed reports are reviewed by system moderators and administrators.
    /// </summary>
    public class FeedContentReport
    {
        /// <summary>
        /// Gets or sets the unique identifier for the report.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the reported feed post. Null when the report is for a comment.
        /// </summary>
        public int? PostId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the reported post.
        /// </summary>
        public Post? Post { get; set; }

        /// <summary>
        /// Gets or sets the ID of the reported comment. Null when the report is for a post.
        /// </summary>
        public int? CommentId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the reported comment.
        /// </summary>
        public Comment? Comment { get; set; }

        /// <summary>
        /// Gets or sets a snapshot of the content author's user ID, kept for review and repeat-offender lookups.
        /// </summary>
        [MaxLength(450)]
        public required string ContentAuthorId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who submitted the report.
        /// </summary>
        [MaxLength(450)]
        public required string ReporterId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the reporting user.
        /// </summary>
        public ApplicationUser Reporter { get; set; } = null!;

        /// <summary>
        /// Gets or sets the reason category. Group-specific reasons are not valid for feed reports.
        /// </summary>
        public ReportReason Reason { get; set; }

        /// <summary>
        /// Gets or sets optional context provided by the reporter.
        /// </summary>
        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the review status of the report.
        /// </summary>
        public ReportStatus Status { get; set; } = ReportStatus.NotReviewed;

        /// <summary>
        /// Gets or sets the UTC timestamp when the report was created. Set by the server.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the ID of the system moderator or administrator who reviewed the report.
        /// </summary>
        [MaxLength(450)]
        public string? ReviewerId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the reviewer.
        /// </summary>
        public ApplicationUser? Reviewer { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the status last changed. Set by the server.
        /// </summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>
        /// Gets or sets notes added by the reviewer.
        /// </summary>
        [MaxLength(2000)]
        public string? ReviewerNotes { get; set; }
    }
}
