using Microsoft.AspNetCore.Identity;

namespace FreeSpeakWeb.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public string? ProfilePictureUrl { get; set; }

        // Required profile fields
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        // Optional profile fields
        public string? NameSuffix { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Occupation { get; set; }
    }

}
