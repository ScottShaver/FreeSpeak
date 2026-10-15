using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// A single administrator-managed configuration override stored in the database.
    /// The <see cref="Key"/> uses the same key path as appsettings.json (for example <c>VirusScan:ClamAvHost</c>),
    /// so the value overrides the file value when configuration is built.
    /// </summary>
    [Table("SystemSettings")]
    public class SystemSetting
    {
        /// <summary>
        /// Maximum length of <see cref="Key"/>.
        /// </summary>
        public const int MaxKeyLength = 200;

        /// <summary>
        /// Gets or sets the configuration key path (primary key), e.g. <c>Smtp:Server</c>.
        /// </summary>
        [Key]
        [MaxLength(MaxKeyLength)]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stored value. When <see cref="IsSecret"/> is true this is the Data Protection
        /// ciphertext, never the plain value.
        /// </summary>
        [Required]
        [Column(TypeName = "text")]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="Value"/> is encrypted.
        /// </summary>
        public bool IsSecret { get; set; }

        /// <summary>
        /// Gets or sets the UTC time of the last change.
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the administrator who last changed the setting.
        /// Not a foreign key so history survives user deletion.
        /// </summary>
        [MaxLength(450)]
        public string? UpdatedByUserId { get; set; }
    }
}
