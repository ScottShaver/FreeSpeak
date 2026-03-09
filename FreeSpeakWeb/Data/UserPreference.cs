using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a user preference setting
    /// </summary>
    public class UserPreference
    {
        /// <summary>
        /// Unique identifier for the preference
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// User ID (foreign key to AspNetUsers)
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property to user
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        /// <summary>
        /// Type of preference
        /// </summary>
        [Required]
        public PreferenceType PreferenceType { get; set; }

        /// <summary>
        /// Value of the preference (stored as string, can be JSON for complex values)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string PreferenceValue { get; set; } = string.Empty;

        /// <summary>
        /// When the preference was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the preference was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
