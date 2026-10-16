using System.ComponentModel.DataAnnotations;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Records a suspension of a user account by a system moderator or administrator: who suspended the user,
    /// why, and until when. The sign-in block itself is the Identity lockout set through the lockout service.
    /// </summary>
    public class UserSuspension
    {
        /// <summary>
        /// Gets or sets the unique identifier of the suspension.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the suspended user.
        /// </summary>
        [MaxLength(450)]
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the suspended user.
        /// </summary>
        public ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Gets or sets the ID of the moderator or administrator who suspended the user.
        /// </summary>
        [MaxLength(450)]
        public required string SuspendedByUserId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the suspending moderator.
        /// </summary>
        public ApplicationUser SuspendedBy { get; set; } = null!;

        /// <summary>
        /// Gets or sets the reason shown to the suspended user.
        /// </summary>
        [MaxLength(2000)]
        public required string Reason { get; set; }

        /// <summary>
        /// Gets or sets the ID of the feed content report that led to the suspension, if any.
        /// </summary>
        public int? RelatedReportId { get; set; }

        /// <summary>
        /// Gets or sets the server-set UTC time the suspension started.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC time the suspension ends, or null for a permanent suspension.
        /// </summary>
        public DateTime? EndsAt { get; set; }

        /// <summary>
        /// Gets or sets the UTC time the suspension was lifted early, or null when it was not lifted.
        /// </summary>
        public DateTime? LiftedAt { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who lifted the suspension, if it was lifted.
        /// </summary>
        [MaxLength(450)]
        public string? LiftedByUserId { get; set; }

        /// <summary>
        /// Gets or sets the optional note recorded when the suspension was lifted.
        /// </summary>
        [MaxLength(2000)]
        public string? LiftNote { get; set; }
    }
}
