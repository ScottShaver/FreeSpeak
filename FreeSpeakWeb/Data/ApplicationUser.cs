using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace FreeSpeakWeb.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        [MaxLength(75)]
        public string? ProfilePictureUrl { get; set; }

        // Required profile fields
        [PersonalData]
        [Required]
        [MaxLength(75)]
        public string FirstName { get; set; } = string.Empty;

        [PersonalData]
        [Required]
        [MaxLength(75)]
        public string LastName { get; set; } = string.Empty;

        // Optional profile fields
        [PersonalData]
        [MaxLength(75)]
        public string? NameSuffix { get; set; }

        [PersonalData]
        public DateOnly? DateOfBirth { get; set; }

        [PersonalData]
        [MaxLength(75)]
        public string? City { get; set; }

        [PersonalData]
        [MaxLength(75)]
        public string? State { get; set; }

        [PersonalData]
        [MaxLength(75)]
        public string? Occupation { get; set; }
    }

}
