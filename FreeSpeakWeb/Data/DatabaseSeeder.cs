using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeSpeakWeb.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedTestUsersAsync(UserManager<ApplicationUser> userManager, ILogger logger)
        {
            try
            {
                // Check if we already have test users
                var existingUser = await userManager.FindByEmailAsync("testuser1@example.com");
                if (existingUser != null)
                {
                    logger.LogInformation("Test users already exist. Skipping seeding.");
                    return;
                }

            var testUsers = new[]
            {
                new { FirstName = "Emma", LastName = "Johnson", UserName = "emmaj", Email = "testuser1@example.com", City = "New York", State = "NY", Occupation = "Software Engineer" },
                new { FirstName = "Liam", LastName = "Smith", UserName = "liams", Email = "testuser2@example.com", City = "Los Angeles", State = "CA", Occupation = "Designer" },
                new { FirstName = "Olivia", LastName = "Williams", UserName = "oliviaw", Email = "testuser3@example.com", City = "Chicago", State = "IL", Occupation = "Teacher" },
                new { FirstName = "Noah", LastName = "Brown", UserName = "noahb", Email = "testuser4@example.com", City = "Houston", State = "TX", Occupation = "Doctor" },
                new { FirstName = "Ava", LastName = "Jones", UserName = "avaj", Email = "testuser5@example.com", City = "Phoenix", State = "AZ", Occupation = "Nurse" },
                new { FirstName = "Ethan", LastName = "Garcia", UserName = "ethang", Email = "testuser6@example.com", City = "Philadelphia", State = "PA", Occupation = "Lawyer" },
                new { FirstName = "Sophia", LastName = "Martinez", UserName = "sophiam", Email = "testuser7@example.com", City = "San Antonio", State = "TX", Occupation = "Accountant" },
                new { FirstName = "Mason", LastName = "Rodriguez", UserName = "masonr", Email = "testuser8@example.com", City = "San Diego", State = "CA", Occupation = "Chef" },
                new { FirstName = "Isabella", LastName = "Hernandez", UserName = "isabellah", Email = "testuser9@example.com", City = "Dallas", State = "TX", Occupation = "Artist" },
                new { FirstName = "James", LastName = "Lopez", UserName = "jamesl", Email = "testuser10@example.com", City = "San Jose", State = "CA", Occupation = "Musician" },
                new { FirstName = "Mia", LastName = "Gonzalez", UserName = "miag", Email = "testuser11@example.com", City = "Austin", State = "TX", Occupation = "Writer" },
                new { FirstName = "Lucas", LastName = "Wilson", UserName = "lucasw", Email = "testuser12@example.com", City = "Jacksonville", State = "FL", Occupation = "Engineer" },
                new { FirstName = "Charlotte", LastName = "Anderson", UserName = "charlottea", Email = "testuser13@example.com", City = "Fort Worth", State = "TX", Occupation = "Scientist" },
                new { FirstName = "Benjamin", LastName = "Thomas", UserName = "benjamint", Email = "testuser14@example.com", City = "Columbus", State = "OH", Occupation = "Manager" },
                new { FirstName = "Amelia", LastName = "Taylor", UserName = "ameliat", Email = "testuser15@example.com", City = "Charlotte", State = "NC", Occupation = "Consultant" },
                new { FirstName = "Henry", LastName = "Moore", UserName = "henrym", Email = "testuser16@example.com", City = "Indianapolis", State = "IN", Occupation = "Analyst" },
                new { FirstName = "Harper", LastName = "Jackson", UserName = "harperj", Email = "testuser17@example.com", City = "Seattle", State = "WA", Occupation = "Developer" },
                new { FirstName = "Alexander", LastName = "Martin", UserName = "alexanderm", Email = "testuser18@example.com", City = "Denver", State = "CO", Occupation = "Architect" },
                new { FirstName = "Evelyn", LastName = "Lee", UserName = "evelynl", Email = "testuser19@example.com", City = "Boston", State = "MA", Occupation = "Pharmacist" },
                new { FirstName = "Sebastian", LastName = "Perez", UserName = "sebastianp", Email = "testuser20@example.com", City = "Nashville", State = "TN", Occupation = "Entrepreneur" }
            };

            const string password = "Test123!"; // Same password for all test users

            foreach (var userData in testUsers)
            {
                var user = new ApplicationUser
                {
                    UserName = userData.UserName,
                    Email = userData.Email,
                    EmailConfirmed = true, // Auto-confirm emails for test users
                    FirstName = userData.FirstName,
                    LastName = userData.LastName,
                    City = userData.City,
                    State = userData.State,
                    Occupation = userData.Occupation,
                    DateOfBirth = DateOnly.FromDateTime(DateTime.Now.AddYears(-25 - Random.Shared.Next(0, 30))) // Random age between 25-55
                };

                var result = await userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    logger.LogInformation("Created test user: {UserName} ({FirstName} {LastName})", userData.UserName, userData.FirstName, userData.LastName);
                }
                else
                {
                    logger.LogWarning("Failed to create user {UserName}: {Errors}", userData.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            logger.LogInformation("Test user seeding completed!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{ExceptionType}] Error occurred while seeding test users. Exception: {ExceptionMessage}", ex.GetType().Name, ex.Message);
                throw;
            }
        }
    }
}
