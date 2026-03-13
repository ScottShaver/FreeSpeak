using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Represents an application user with extended profile information beyond the base IdentityUser.
    /// Includes personal details such as name, location, occupation, and profile picture.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// Gets or sets the URL to the user's profile picture.
        /// This is stored as personal data and can be up to 75 characters.
        /// </summary>
        [PersonalData]
        [MaxLength(75)]
        public string? ProfilePictureUrl { get; set; }

        /// <summary>
        /// Gets or sets the user's first name.
        /// This is a required field with a maximum length of 75 characters.
        /// </summary>
        [PersonalData]
        [Required]
        [MaxLength(75)]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's last name.
        /// This is a required field with a maximum length of 75 characters.
        /// </summary>
        [PersonalData]
        [Required]
        [MaxLength(75)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's name suffix (e.g., "Jr.", "Sr.", "III").
        /// This is an optional field with a maximum length of 75 characters.
        /// </summary>
        [PersonalData]
        [MaxLength(75)]
        public string? NameSuffix { get; set; }

        /// <summary>
        /// Gets or sets the user's date of birth.
        /// This is an optional field stored as personal data.
        /// </summary>
        [PersonalData]
        public DateOnly? DateOfBirth { get; set; }

        /// <summary>
        /// Gets or sets the city where the user resides.
        /// This is an optional field with a maximum length of 75 characters.
        /// </summary>
        [PersonalData]
        [MaxLength(75)]
        public string? City { get; set; }

        /// <summary>
        /// Gets or sets the state or region where the user resides.
        /// This is an optional field with a maximum length of 75 characters.
        /// </summary>
        [PersonalData]
        [MaxLength(75)]
        public string? State { get; set; }

        /// <summary>
        /// Gets or sets the user's occupation or job title.
        /// This is an optional field with a maximum length of 75 characters.
        /// </summary>
        [PersonalData]
        [MaxLength(75)]
        public string? Occupation { get; set; }
    }

}
