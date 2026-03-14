using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Provides methods for seeding the database with test data for development purposes.
    /// Includes test users, friendships, posts, groups, and group memberships.
    /// </summary>
    public static class DatabaseSeeder
    {
        /// <summary>
        /// Seeds test users into the database using ASP.NET Core Identity.
        /// This overload is for seeding users only without DbContext access.
        /// </summary>
        /// <param name="userManager">The UserManager for creating and managing Identity users.</param>
        /// <param name="logger">Logger for recording seeding progress and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task SeedTestUsersAsync(UserManager<ApplicationUser> userManager, ILogger logger)
        {
            await SeedTestUsersAsync(userManager, null, logger);
        }

        /// <summary>
        /// Seeds the AspNetRoles table with predefined system roles and creates the system administrator user.
        /// Creates four roles: SystemAdministrator, SystemModerator, GroupModerator, and GroupAdministrator.
        /// Also creates a system administrator user from configuration settings and assigns the SystemAdministrator role.
        /// </summary>
        /// <param name="roleManager">The RoleManager for creating and managing Identity roles.</param>
        /// <param name="userManager">The UserManager for creating and managing Identity users.</param>
        /// <param name="systemAdminConfig">Configuration settings for the system administrator account.</param>
        /// <param name="logger">Logger for recording seeding progress and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task SeedRolesAndSystemAdminAsync(
            RoleManager<IdentityRole> roleManager, 
            UserManager<ApplicationUser> userManager,
            SystemAdministratorInitInfo systemAdminConfig,
            ILogger logger)
        {
            try
            {
                // Define the roles to be seeded
                var roles = new[] 
                { 
                    "SystemAdministrator", 
                    "SystemModerator", 
                    "GroupModerator", 
                    "GroupAdministrator" 
                };

                // Seed roles
                logger.LogInformation("Seeding user roles...");
                foreach (var roleName in roles)
                {
                    var roleExists = await roleManager.RoleExistsAsync(roleName);
                    if (!roleExists)
                    {
                        var role = new IdentityRole(roleName);
                        var result = await roleManager.CreateAsync(role);

                        if (result.Succeeded)
                        {
                            logger.LogInformation("Created role: {RoleName}", roleName);
                        }
                        else
                        {
                            logger.LogWarning("Failed to create role {RoleName}: {Errors}", 
                                roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                        }
                    }
                    else
                    {
                        logger.LogInformation("Role {RoleName} already exists, skipping.", roleName);
                    }
                }

                // Create system administrator user
                logger.LogInformation("Creating system administrator user...");

                // Validate configuration
                if (string.IsNullOrWhiteSpace(systemAdminConfig.UserName) ||
                    string.IsNullOrWhiteSpace(systemAdminConfig.Email) ||
                    string.IsNullOrWhiteSpace(systemAdminConfig.Password) ||
                    string.IsNullOrWhiteSpace(systemAdminConfig.FirstName) ||
                    string.IsNullOrWhiteSpace(systemAdminConfig.LastName))
                {
                    logger.LogWarning("System administrator configuration is incomplete. Skipping system admin user creation.");
                    return;
                }

                // Check if system admin already exists
                var existingAdmin = await userManager.FindByEmailAsync(systemAdminConfig.Email);
                if (existingAdmin != null)
                {
                    logger.LogInformation("System administrator user already exists: {Email}", systemAdminConfig.Email);

                    // Ensure the user has the SystemAdministrator role
                    if (!await userManager.IsInRoleAsync(existingAdmin, "SystemAdministrator"))
                    {
                        var addRoleResult = await userManager.AddToRoleAsync(existingAdmin, "SystemAdministrator");
                        if (addRoleResult.Succeeded)
                        {
                            logger.LogInformation("Added SystemAdministrator role to existing user: {Email}", systemAdminConfig.Email);
                        }
                        else
                        {
                            logger.LogWarning("Failed to add SystemAdministrator role to user {Email}: {Errors}",
                                systemAdminConfig.Email, string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
                        }
                    }
                    return;
                }

                // Create the system admin user
                var systemAdmin = new ApplicationUser
                {
                    UserName = systemAdminConfig.UserName,
                    Email = systemAdminConfig.Email,
                    EmailConfirmed = true,
                    PhoneNumber = systemAdminConfig.PhoneNumber,
                    PhoneNumberConfirmed = !string.IsNullOrWhiteSpace(systemAdminConfig.PhoneNumber),
                    FirstName = systemAdminConfig.FirstName,
                    LastName = systemAdminConfig.LastName
                };

                var createResult = await userManager.CreateAsync(systemAdmin, systemAdminConfig.Password);

                if (createResult.Succeeded)
                {
                    logger.LogInformation("Successfully created system administrator user: {UserName} ({Email})", 
                        systemAdminConfig.UserName, systemAdminConfig.Email);

                    // Assign SystemAdministrator role
                    var roleResult = await userManager.AddToRoleAsync(systemAdmin, "SystemAdministrator");
                    if (roleResult.Succeeded)
                    {
                        logger.LogInformation("Successfully assigned SystemAdministrator role to user: {UserName}", 
                            systemAdminConfig.UserName);
                    }
                    else
                    {
                        logger.LogWarning("Failed to assign SystemAdministrator role to user {UserName}: {Errors}",
                            systemAdminConfig.UserName, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    logger.LogWarning("Failed to create system administrator user: {Errors}",
                        string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }

                logger.LogInformation("Role and system administrator seeding completed!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{ExceptionType}] Error occurred while seeding roles and system administrator. Exception: {ExceptionMessage}", 
                    ex.GetType().Name, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Seeds test users, friendships, posts, and groups into the database for development and testing.
        /// Creates 20 test users with varied profile data, establishes friendships between them,
        /// generates sample posts, creates community groups, and assigns group memberships.
        /// </summary>
        /// <param name="userManager">The UserManager for creating and managing Identity users.</param>
        /// <param name="dbContext">Optional DbContext for seeding related data (posts, friendships, groups). If null, only users are seeded.</param>
        /// <param name="logger">Logger for recording seeding progress and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Rethrows any exception that occurs during user seeding after logging.</exception>
        public static async Task SeedTestUsersAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext? dbContext, ILogger logger)
        {
            try
            {
                // Check if we already have test users
                var existingUser = await userManager.FindByEmailAsync("testuser1@example.com");
                if (existingUser != null)
                {
                    logger.LogInformation("Test users already exist. Skipping user seeding.");

                    // Still seed groups and posts if DbContext is provided
                    if (dbContext != null)
                    {
                        await SeedPostsAndFriendshipsAsync(dbContext, logger);
                        await SeedGroupsAndGroupPostsAsync(dbContext, logger);
                    }
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
                await SeedGroupsAndGroupPostsAsync(dbContext, logger);
            }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{ExceptionType}] Error occurred while seeding test users. Exception: {ExceptionMessage}", ex.GetType().Name, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Seeds posts and friendship relationships for test users.
        /// Creates friendships between users and generates sample posts with varied content and timestamps.
        /// Each user receives 5-10 posts with random like and comment counts.
        /// </summary>
        /// <param name="dbContext">The database context for accessing and modifying data.</param>
        /// <param name="logger">Logger for recording seeding progress and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <summary>
        /// Seeds community groups, group memberships, and group posts for test data.
        /// Creates 10 diverse groups with varying settings, assigns members (including admins and moderators),
        /// and generates sample group posts. Each group receives 5-15 members and 10-20 posts.
        /// </summary>
        /// <param name="dbContext">The database context for accessing and modifying data.</param>
        /// <param name="logger">Logger for recording seeding progress and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task SeedGroupsAndGroupPostsAsync(ApplicationDbContext dbContext, ILogger logger)
        {
            try
            {
                // Check if groups already exist
                var existingGroups = await dbContext.Groups.AnyAsync();
                if (existingGroups)
                {
                    logger.LogInformation("Groups already exist. Skipping group seeding.");
                    return;
                }

                // Get all seeded users
                var users = await dbContext.Users.ToListAsync();
                if (users.Count == 0)
                {
                    logger.LogWarning("No users found for seeding groups.");
                    return;
                }

                logger.LogInformation("Seeding groups...");

                var random = Random.Shared;
                var groups = new List<Group>();

                // Define sample groups
                var groupData = new[]
                {
                    new { 
                        Name = "Tech Enthusiasts", 
                        Description = "A community for technology lovers to discuss the latest trends, gadgets, and innovations in the tech world.",
                        IsPublic = true,
                        RequiresApproval = false,
                        CreatorIndex = 0
                    },
                    new { 
                        Name = "Book Club", 
                        Description = "Share your favorite reads, discuss literature, and discover new books with fellow bookworms.",
                        IsPublic = true,
                        RequiresApproval = true,
                        CreatorIndex = 1
                    },
                    new { 
                        Name = "Fitness & Wellness", 
                        Description = "Motivate each other, share workout tips, healthy recipes, and wellness advice.",
                        IsPublic = true,
                        RequiresApproval = false,
                        CreatorIndex = 2
                    },
                    new { 
                        Name = "Photography Collective", 
                        Description = "Share your photos, get feedback, learn techniques, and appreciate visual art together.",
                        IsPublic = true,
                        RequiresApproval = false,
                        CreatorIndex = 3
                    },
                    new { 
                        Name = "Cooking & Recipes", 
                        Description = "Exchange recipes, cooking tips, and culinary adventures. From beginners to master chefs!",
                        IsPublic = true,
                        RequiresApproval = false,
                        CreatorIndex = 4
                    },
                    new { 
                        Name = "Gaming Community", 
                        Description = "Connect with fellow gamers, discuss games, share tips, and organize gaming sessions.",
                        IsPublic = true,
                        RequiresApproval = false,
                        CreatorIndex = 5
                    },
                    new { 
                        Name = "Travel Stories", 
                        Description = "Share your travel experiences, get destination recommendations, and inspire wanderlust.",
                        IsPublic = true,
                        RequiresApproval = true,
                        CreatorIndex = 6
                    },
                    new { 
                        Name = "Pet Lovers United", 
                        Description = "Show off your pets, share care tips, and connect with other animal lovers.",
                        IsPublic = true,
                        RequiresApproval = false,
                        CreatorIndex = 7
                    },
                    new { 
                        Name = "DIY & Crafts", 
                        Description = "Share your creative projects, get inspiration, and learn new crafting techniques.",
                        IsPublic = true,
                        RequiresApproval = false,
                        CreatorIndex = 8
                    },
                    new { 
                        Name = "Music Appreciation", 
                        Description = "Discover new music, discuss artists and albums, and share what you're listening to.",
                        IsPublic = true,
                        RequiresApproval = false,
                        CreatorIndex = 9
                    }
                };

                // Create groups
                foreach (var groupInfo in groupData)
                {
                    var creator = users[groupInfo.CreatorIndex];
                    var daysAgo = random.Next(30, 180); // Groups created 30-180 days ago

                    var group = new Group
                    {
                        CreatorId = creator.Id,
                        Name = groupInfo.Name,
                        Description = groupInfo.Description,
                        IsPublic = groupInfo.IsPublic,
                        IsHidden = false,
                        RequiresJoinApproval = groupInfo.RequiresApproval,
                        CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
                        LastActiveAt = DateTime.UtcNow.AddDays(-random.Next(0, 7)),
                        MemberCount = 0 // Will be updated as we add members
                    };
                    groups.Add(group);
                }

                dbContext.Groups.AddRange(groups);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Seeded {GroupCount} groups!", groups.Count);

                logger.LogInformation("Adding group members...");

                // Add creator as admin member and add other members to each group
                var groupUsers = new List<GroupUser>();
                foreach (var group in groups)
                {
                    // Add creator as admin
                    groupUsers.Add(new GroupUser
                    {
                        GroupId = group.Id,
                        UserId = group.CreatorId,
                        JoinedAt = group.CreatedAt,
                        IsAdmin = true,
                        IsModerator = false
                    });

                    // Add 5-15 random members to each group
                    var memberCount = random.Next(5, 16);
                    var potentialMembers = users.Where(u => u.Id != group.CreatorId).OrderBy(x => random.Next()).Take(memberCount);

                    foreach (var member in potentialMembers)
                    {
                        var isModerator = random.Next(0, 10) == 0; // 10% chance to be moderator
                        groupUsers.Add(new GroupUser
                        {
                            GroupId = group.Id,
                            UserId = member.Id,
                            JoinedAt = group.CreatedAt.AddDays(random.Next(1, 30)),
                            IsAdmin = false,
                            IsModerator = isModerator
                        });
                    }

                    // Update member count
                    group.MemberCount = groupUsers.Count(gu => gu.GroupId == group.Id);
                }

                dbContext.GroupUsers.AddRange(groupUsers);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Added {MemberCount} group memberships!", groupUsers.Count);

                logger.LogInformation("Seeding group posts...");

                // Sample group post contents
                var groupPostContents = new[]
                {
                    "Welcome to the group! Looking forward to great discussions.",
                    "What's everyone working on this week?",
                    "Just wanted to share this amazing discovery!",
                    "Does anyone have recommendations for beginners?",
                    "This has been a game-changer for me. Highly recommended!",
                    "Looking for advice from the community.",
                    "Excited to be part of this group!",
                    "Anyone else experiencing this? Let me know!",
                    "Here's a tip that helped me tremendously.",
                    "What do you all think about this?",
                    "Sharing my latest project with the group!",
                    "Quick question for the experts here.",
                    "This made my day! Had to share.",
                    "Looking for collaborators on something exciting.",
                    "Weekly check-in: How's everyone doing?",
                    "Just achieved a major milestone!",
                    "Need some input from the community.",
                    "This is why I love this group!",
                    "Pro tip: This saved me hours of work.",
                    "What's your favorite thing about this topic?",
                    "Beginner question: Where do I start?",
                    "Advanced discussion: Let's dive deep.",
                    "Resources that helped me level up.",
                    "Community event idea - thoughts?",
                    "Celebrating our group's growth!",
                    "Monthly update: Here's what's new.",
                    "Feature request: What would you like to see?",
                    "Success story: Thanks to this community!",
                    "Challenge accepted! Who's joining me?",
                    "Throwback to when we started this group!"
                };

                var groupPosts = new List<GroupPost>();

                // Create 10-20 posts for each group
                foreach (var group in groups)
                {
                    var groupMembers = groupUsers.Where(gu => gu.GroupId == group.Id).ToList();
                    var postCount = random.Next(10, 21);

                    for (int i = 0; i < postCount; i++)
                    {
                        var randomMember = groupMembers[random.Next(groupMembers.Count)];
                        var daysAgo = random.Next(0, 60);
                        var hoursAgo = random.Next(0, 24);

                        var post = new GroupPost
                        {
                            GroupId = group.Id,
                            AuthorId = randomMember.UserId,
                            Content = groupPostContents[random.Next(groupPostContents.Length)],
                            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo).AddHours(-hoursAgo),
                            LikeCount = random.Next(0, 30),
                            CommentCount = random.Next(0, 15)
                        };
                        groupPosts.Add(post);
                    }
                }

                dbContext.GroupPosts.AddRange(groupPosts);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Seeded {PostCount} group posts for {GroupCount} groups!", groupPosts.Count, groups.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{ExceptionType}] Error occurred while seeding groups and group posts. Exception: {ExceptionMessage}", ex.GetType().Name, ex.Message);
                // Don't throw - let the app continue even if group seeding fails
            }
        }
    }
}
