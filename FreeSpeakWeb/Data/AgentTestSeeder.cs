using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Seeds a deterministic set of test personas, groups, posts and comments used for
    /// agentic (AI-driven) end-to-end testing. This seeder is for development/test use only
    /// and refuses to run unless every safety check in <see cref="EnsureSafeToRun"/> passes.
    /// </summary>
    public static class AgentTestSeeder
    {
        /// <summary>
        /// Configuration key that must be set to true to enable agent test seeding.
        /// </summary>
        public const string EnabledConfigKey = "AgentTest:Enabled";

        /// <summary>
        /// Required suffix of the database name; prevents running against a normal dev or production database.
        /// </summary>
        public const string RequiredDatabaseSuffix = "_AgentTest";

        /// <summary>
        /// Dev-only password shared by all agent test personas. Documented in docs/AGENT_TESTING.md.
        /// </summary>
        public const string PersonaPassword = "AgentTest123!";

        private record Persona(string UserName, string Email, string FirstName, string LastName, string? Role = null);

        private static readonly Persona[] Personas =
        {
            new("agent.sysadmin", "agent.sysadmin@agenttest.invalid", "Sam", "Sysadmin", "SystemAdministrator"),
            new("agent.owner", "agent.owner@agenttest.invalid", "Olive", "Owner"),
            new("agent.moderator", "agent.moderator@agenttest.invalid", "Morgan", "Moderator"),
            new("agent.member", "agent.member@agenttest.invalid", "Max", "Member"),
            new("agent.banned", "agent.banned@agenttest.invalid", "Bailey", "Banned"),
            new("agent.newuser", "agent.newuser@agenttest.invalid", "Nina", "Newuser"),
            new("agent.friend", "agent.friend@agenttest.invalid", "Finn", "Friend"),
            new("agent.stranger", "agent.stranger@agenttest.invalid", "Stella", "Stranger"),
        };

        /// <summary>
        /// Verifies that agent test seeding is allowed: Development environment, explicit opt-in
        /// configuration, and a database whose name ends with <see cref="RequiredDatabaseSuffix"/>.
        /// </summary>
        /// <param name="isDevelopment">True when the app runs in the Development environment.</param>
        /// <param name="enabled">True when <see cref="EnabledConfigKey"/> is set.</param>
        /// <param name="databaseName">The name of the connected database.</param>
        /// <exception cref="InvalidOperationException">Thrown when any safety check fails.</exception>
        public static void EnsureSafeToRun(bool isDevelopment, bool enabled, string? databaseName)
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException("Agent test seeding is only allowed in the Development environment.");
            }

            if (!enabled)
            {
                throw new InvalidOperationException($"Agent test seeding requires {EnabledConfigKey}=true.");
            }

            if (string.IsNullOrEmpty(databaseName) ||
                !databaseName.EndsWith(RequiredDatabaseSuffix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Agent test seeding refused: database '{databaseName}' does not end with '{RequiredDatabaseSuffix}'.");
            }
        }

        /// <summary>
        /// Seeds the agent test personas, friendships, groups, memberships, posts and comments.
        /// Idempotent: does nothing if the persona users already exist.
        /// </summary>
        /// <param name="userManager">The UserManager for creating users.</param>
        /// <param name="dbContext">The database context used for related data.</param>
        /// <param name="isDevelopment">True when the app runs in the Development environment.</param>
        /// <param name="enabled">True when agent test seeding was explicitly enabled.</param>
        /// <param name="logger">Logger for progress and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a safety check fails.</exception>
        public static async Task SeedAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext,
            bool isDevelopment,
            bool enabled,
            ILogger logger)
        {
            EnsureSafeToRun(isDevelopment, enabled, dbContext.Database.GetDbConnection().Database);

            if (await userManager.FindByNameAsync(Personas[0].UserName) != null)
            {
                logger.LogInformation("Agent test personas already exist. Skipping.");
                return;
            }

            var users = new Dictionary<string, ApplicationUser>();
            foreach (var p in Personas)
            {
                var user = new ApplicationUser
                {
                    UserName = p.UserName,
                    Email = p.Email,
                    EmailConfirmed = true,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    DateOfBirth = new DateOnly(1990, 1, 1),
                };

                var result = await userManager.CreateAsync(user, PersonaPassword);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create persona {p.UserName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }

                if (p.Role != null)
                {
                    var roleResult = await userManager.AddToRoleAsync(user, p.Role);
                    if (!roleResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Failed to assign role {p.Role} to {p.UserName}: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                    }
                }

                users[p.UserName] = user;
            }

            var owner = users["agent.owner"];
            var moderator = users["agent.moderator"];
            var member = users["agent.member"];
            var banned = users["agent.banned"];
            var friend = users["agent.friend"];
            var stranger = users["agent.stranger"];

            // Fixed base time keeps data deterministic across runs.
            var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            // Friendships: friend is friends with member and owner; stranger has none.
            dbContext.Friendships.AddRange(
                new Friendship { RequesterId = friend.Id, AddresseeId = member.Id, Status = FriendshipStatus.Accepted, RequestedAt = baseTime, RespondedAt = baseTime },
                new Friendship { RequesterId = friend.Id, AddresseeId = owner.Id, Status = FriendshipStatus.Accepted, RequestedAt = baseTime, RespondedAt = baseTime });

            // Feed posts with different audiences.
            var publicPost = new Post { AuthorId = member.Id, Content = "Agent test: public post by member.", AudienceType = AudienceType.Public, CreatedAt = baseTime.AddMinutes(1) };
            var friendsPost = new Post { AuthorId = member.Id, Content = "Agent test: friends-only post by member.", AudienceType = AudienceType.FriendsOnly, CreatedAt = baseTime.AddMinutes(2) };
            var privatePost = new Post { AuthorId = member.Id, Content = "Agent test: private (only me) post by member.", AudienceType = AudienceType.MeOnly, CreatedAt = baseTime.AddMinutes(3) };
            dbContext.Posts.AddRange(publicPost, friendsPost, privatePost);

            // Groups
            var openGroup = new Group
            {
                Name = "Agent Test Open Group",
                Description = "Public group, no approval required.",
                CreatorId = owner.Id,
                IsPublic = true,
                IsActive = true,
                CreatedAt = baseTime,
                LastActiveAt = baseTime,
                MemberCount = 3,
            };
            var privateGroup = new Group
            {
                Name = "Agent Test Private Group",
                Description = "Private group with join approval and post approval.",
                CreatorId = owner.Id,
                IsPublic = false,
                IsActive = true,
                RequiresJoinApproval = true,
                RequiresPostApproval = true,
                CreatedAt = baseTime,
                LastActiveAt = baseTime,
                MemberCount = 2,
            };
            dbContext.Groups.AddRange(openGroup, privateGroup);
            await dbContext.SaveChangesAsync();

            dbContext.GroupUsers.AddRange(
                new GroupUser { GroupId = openGroup.Id, UserId = owner.Id, IsAdmin = true, JoinedAt = baseTime, LastActiveAt = baseTime },
                new GroupUser { GroupId = openGroup.Id, UserId = moderator.Id, IsModerator = true, JoinedAt = baseTime, LastActiveAt = baseTime },
                new GroupUser { GroupId = openGroup.Id, UserId = member.Id, JoinedAt = baseTime, LastActiveAt = baseTime },
                new GroupUser { GroupId = privateGroup.Id, UserId = owner.Id, IsAdmin = true, JoinedAt = baseTime, LastActiveAt = baseTime },
                new GroupUser { GroupId = privateGroup.Id, UserId = moderator.Id, IsModerator = true, JoinedAt = baseTime, LastActiveAt = baseTime });

            dbContext.GroupBannedMembers.Add(new GroupBannedMember { GroupId = openGroup.Id, UserId = banned.Id, BannedAt = baseTime.AddMinutes(5) });

            var groupPost1 = new GroupPost { GroupId = openGroup.Id, AuthorId = owner.Id, Content = "Agent test: welcome to the open group.", CreatedAt = baseTime.AddMinutes(10), Status = PostStatus.Posted };
            var groupPost2 = new GroupPost { GroupId = openGroup.Id, AuthorId = member.Id, Content = "Agent test: a member post in the open group.", CreatedAt = baseTime.AddMinutes(11), Status = PostStatus.Posted };
            var groupPost3 = new GroupPost { GroupId = privateGroup.Id, AuthorId = owner.Id, Content = "Agent test: secret post in the private group.", CreatedAt = baseTime.AddMinutes(12), Status = PostStatus.Posted };
            dbContext.GroupPosts.AddRange(groupPost1, groupPost2, groupPost3);
            await dbContext.SaveChangesAsync();

            dbContext.Comments.Add(new Comment { PostId = publicPost.Id, AuthorId = friend.Id, Content = "Agent test: friend comment on public post.", CreatedAt = baseTime.AddMinutes(20) });
            dbContext.Comments.Add(new Comment { PostId = publicPost.Id, AuthorId = stranger.Id, Content = "Agent test: stranger comment on public post.", CreatedAt = baseTime.AddMinutes(21) });
            dbContext.GroupPostComments.Add(new GroupPostComment { PostId = groupPost2.Id, AuthorId = moderator.Id, Content = "Agent test: moderator comment on member post.", CreatedAt = baseTime.AddMinutes(22) });
            publicPost.CommentCount = 2;
            groupPost2.CommentCount = 1;
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Agent test seeding completed ({Count} personas).", Personas.Length);
        }
    }
}
