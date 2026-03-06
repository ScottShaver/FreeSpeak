using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeSpeakWeb.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedTestUsersAsync(UserManager<ApplicationUser> userManager, ILogger logger)
        {
            await SeedTestUsersAsync(userManager, null, logger);
        }

        public static async Task SeedTestUsersAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext? dbContext, ILogger logger)
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

            // Seed posts and friendships if DbContext is provided
            if (dbContext != null)
            {
                await SeedPostsAndFriendshipsAsync(dbContext, logger);
            }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{ExceptionType}] Error occurred while seeding test users. Exception: {ExceptionMessage}", ex.GetType().Name, ex.Message);
                throw;
            }
        }

        private static async Task SeedPostsAndFriendshipsAsync(ApplicationDbContext dbContext, ILogger logger)
        {
            try
            {
                // Check if posts already exist
                var existingPosts = await dbContext.Posts.AnyAsync();
                if (existingPosts)
                {
                    logger.LogInformation("Posts already exist. Skipping post seeding.");
                    return;
                }

                // Get all seeded users
                var users = await dbContext.Users.ToListAsync();
                if (users.Count == 0)
                {
                    logger.LogWarning("No users found for seeding posts.");
                    return;
                }

                logger.LogInformation("Seeding friendships...");

                // Create some friendships (first user is friends with users 2-6)
                var firstUser = users[0];
                for (int i = 1; i < Math.Min(6, users.Count); i++)
                {
                    var friendship = new Friendship
                    {
                        RequesterId = firstUser.Id,
                        AddresseeId = users[i].Id,
                        Status = FriendshipStatus.Accepted,
                        RequestedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(30, 90)),
                        RespondedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30))
                    };
                    dbContext.Friendships.Add(friendship);
                }

                // Create some cross-friendships among other users
                for (int i = 1; i < Math.Min(10, users.Count); i += 2)
                {
                    if (i + 1 < users.Count)
                    {
                        var friendship = new Friendship
                        {
                            RequesterId = users[i].Id,
                            AddresseeId = users[i + 1].Id,
                            Status = FriendshipStatus.Accepted,
                            RequestedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(30, 90)),
                            RespondedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30))
                        };
                        dbContext.Friendships.Add(friendship);
                    }
                }

                await dbContext.SaveChangesAsync();
                logger.LogInformation("Friendships seeded successfully!");

                logger.LogInformation("Seeding posts...");

                // Sample post contents
                var postContents = new[]
                {
                    "Just finished an amazing project! Feeling accomplished 🎉",
                    "Beautiful sunset today. Nature never fails to amaze me.",
                    "Coffee and coding - the perfect combination ☕💻",
                    "Can't believe it's already Friday! This week flew by.",
                    "Trying out a new recipe tonight. Wish me luck! 🍳",
                    "Great workout this morning! Feeling energized for the day.",
                    "Reading an amazing book right now. Highly recommend!",
                    "Just watched an incredible movie. Still processing it!",
                    "Weekend plans: relax and recharge. What about you?",
                    "Learning something new every day. Growth mindset! 📚",
                    "Grateful for good friends and good times.",
                    "New adventures await! Excited for what's coming.",
                    "Sometimes the simple things bring the most joy.",
                    "Working on improving myself one day at a time.",
                    "Music makes everything better 🎵",
                    "Rainy days are perfect for staying cozy indoors.",
                    "Challenging myself to step out of my comfort zone.",
                    "The best is yet to come! Staying positive.",
                    "Found a great new spot in the city today!",
                    "Celebrating small wins. They all count!",
                    "Late night thoughts: Life is beautiful.",
                    "Morning motivation: You got this! 💪",
                    "Trying to be more mindful and present.",
                    "Good vibes only today and every day.",
                    "Throwback to an amazing memory. Missing those times!",
                    "New week, new opportunities. Let's make it count!",
                    "Taking a break to appreciate the little things.",
                    "Inspiration can come from anywhere. Stay open!",
                    "Living my best life, one moment at a time.",
                    "Remember to take care of yourself. You matter!"
                };

                var random = Random.Shared;
                var posts = new List<Post>();

                // Create 5-10 posts for each user
                foreach (var user in users)
                {
                    var postCount = random.Next(5, 11); // 5-10 posts per user
                    for (int i = 0; i < postCount; i++)
                    {
                        var daysAgo = random.Next(0, 60); // Posts from last 60 days
                        var hoursAgo = random.Next(0, 24);
                        var minutesAgo = random.Next(0, 60);

                        var post = new Post
                        {
                            AuthorId = user.Id,
                            Content = postContents[random.Next(postContents.Length)],
                            CreatedAt = DateTime.UtcNow
                                .AddDays(-daysAgo)
                                .AddHours(-hoursAgo)
                                .AddMinutes(-minutesAgo),
                            LikeCount = random.Next(0, 50),
                            CommentCount = random.Next(0, 20)
                        };
                        posts.Add(post);
                    }
                }

                dbContext.Posts.AddRange(posts);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Seeded {PostCount} posts for {UserCount} users!", posts.Count, users.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{ExceptionType}] Error occurred while seeding posts and friendships. Exception: {ExceptionMessage}", ex.GetType().Name, ex.Message);
                // Don't throw - let the app continue even if post seeding fails
            }
        }
    }
}
