using System.ComponentModel.DataAnnotations;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a report submitted by a user against content (post or comment) in a group.
    /// Reports are reviewed by moderators or administrators who can take action on the content.
    /// </summary>
    public class GroupContentReport
    {
        /// <summary>
        /// Gets or sets the unique identifier for the report.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the group where the reported content exists.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the group.
        /// </summary>
        public Group Group { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the reported post, if the report is for a post.
        /// Null if the report is for a comment.
        /// </summary>
        public int? PostId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the reported post.
        /// </summary>
        public GroupPost? Post { get; set; }

        /// <summary>
        /// Gets or sets the ID of the reported comment, if the report is for a comment.
        /// Null if the report is for a post.
        /// </summary>
        public int? CommentId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the reported comment.
        /// </summary>
        public GroupPostComment? Comment { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who submitted the report.
        /// </summary>
        public required string ReporterId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the user who submitted the report.
        /// </summary>
        public ApplicationUser Reporter { get; set; } = null!;

        /// <summary>
        /// Gets or sets the reason category for the report.
        /// </summary>
        public ReportReason Reason { get; set; }

        /// <summary>
        /// Gets or sets the ID of the group rule that was violated.
        /// Only applicable when Reason is ViolatesGroupRule.
        /// </summary>
        public int? ViolatedRuleId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the violated group rule.
        /// </summary>
        public GroupRule? ViolatedRule { get; set; }

        /// <summary>
        /// Gets or sets additional description or context provided by the reporter.
        /// </summary>
        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the current status of the report.
        /// </summary>
        public ReportStatus Status { get; set; } = ReportStatus.NotReviewed;

        /// <summary>
        /// Gets or sets the timestamp when the report was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the ID of the user who reviewed the report.
        /// Null if the report has not been reviewed yet.
        /// </summary>
        public string? ReviewerId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the reviewer.
        /// </summary>
        public ApplicationUser? Reviewer { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the report was reviewed.
        /// Null if the report has not been reviewed yet.
        /// </summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>
        /// Gets or sets any notes added by the reviewer when processing the report.
        /// </summary>
        [MaxLength(2000)]
        public string? ReviewerNotes { get; set; }
    }
}
