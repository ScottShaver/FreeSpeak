using Microsoft.AspNetCore.Identity;

namespace FreeSpeakWeb.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        public string? ProfilePictureUrl { get; set; }

        // Required profile fields
        [PersonalData]
        public string FirstName { get; set; } = string.Empty;

        [PersonalData]
        public string LastName { get; set; } = string.Empty;

        // Optional profile fields
        [PersonalData]
        public string? NameSuffix { get; set; }

        [PersonalData]
        public DateOnly? DateOfBirth { get; set; }

        [PersonalData]
        public string? City { get; set; }

        [PersonalData]
        public string? State { get; set; }

        [PersonalData]
        public string? Occupation { get; set; }
    }

}
