using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents a user's preference or setting for customizing their experience.
    /// Preferences are stored as key-value pairs with string values that can be JSON for complex data.
    /// </summary>
    public class UserPreference
    {
        /// <summary>
        /// Gets or sets the unique identifier for the preference record.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who owns this preference.
        /// Foreign key to AspNetUsers table.
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the navigation property to the user.
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        /// <summary>
        /// Gets or sets the type of preference (e.g., Theme, Language, NotificationSettings).
        /// Used to categorize and retrieve specific preference types.
        /// </summary>
        [Required]
        public PreferenceType PreferenceType { get; set; }

        /// <summary>
        /// Gets or sets the value of the preference.
        /// Stored as a string with a maximum length of 500 characters.
        /// Can be JSON-serialized for complex values.
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string PreferenceValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when the preference was first created.
        /// Defaults to UTC now.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the timestamp when the preference was last updated.
        /// Defaults to UTC now and should be updated whenever PreferenceValue changes.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
