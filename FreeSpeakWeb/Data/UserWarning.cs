using System.ComponentModel.DataAnnotations;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a warning issued to a user by a system moderator or administrator.
    /// </summary>
    public class UserWarning
    {
        /// <summary>
        /// Gets or sets the unique identifier of the warning.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the warned user.
        /// </summary>
        [MaxLength(450)]
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the warned user.
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the moderator or administrator who issued the warning.
        /// </summary>
        [MaxLength(450)]
        public required string IssuedByUserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the issuing moderator.
        /// </summary>
        public ApplicationUser IssuedBy { get; set; } = null!;

        /// <summary>
        /// Gets or sets the reason shown to the warned user.
        /// </summary>
        [MaxLength(2000)]
        public required string Reason { get; set; }

        /// <summary>
        /// Gets or sets the ID of the feed content report that led to the warning, if any.
        /// </summary>
        public int? RelatedReportId { get; set; }

        /// <summary>
        /// Gets or sets the server-set UTC time the warning was issued.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC time the warned user acknowledged the warning, or null when not yet acknowledged.
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }
    }
}
