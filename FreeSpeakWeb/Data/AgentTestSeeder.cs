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
                        new("agent.sysadmin2", "agent.sysadmin2@agenttest.invalid", "Sasha", "Sysadmin", "SystemAdministrator"),
        new("agent.sysmod", "agent.sysmod@agenttest.invalid", "Sydney", "Sysmod", "SystemModerator"),
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
                EnableFileUploads = true,
                RequiresFileApproval = true,
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

            await SeedExtendedDataAsync(dbContext, users, openGroup, privateGroup, publicPost, baseTime);

            logger.LogInformation("Agent test seeding completed ({Count} personas).", Personas.Length);
        }

        /// <summary>
        /// Appends the extended QA data after the original seed so existing IDs never shift:
        /// pending join requests and posts, a pending friend request, notifications, a deep comment
        /// thread, a third group with a non-creator admin, a group file record and extra member posts.
        /// </summary>
        /// <param name="dbContext">The database context used for related data.</param>
        /// <param name="users">All seeded personas keyed by username.</param>
        /// <param name="openGroup">The already saved open group (id 1).</param>
        /// <param name="privateGroup">The already saved private group (id 2).</param>
        /// <param name="publicPost">The already saved public feed post by agent.member.</param>
        /// <param name="baseTime">Fixed anchor time for deterministic timestamps.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task SeedExtendedDataAsync(
            ApplicationDbContext dbContext,
            Dictionary<string, ApplicationUser> users,
            Group openGroup,
            Group privateGroup,
            Post publicPost,
            DateTime baseTime)
        {
            var owner = users["agent.owner"];
            var moderator = users["agent.moderator"];
            var member = users["agent.member"];
            var newUser = users["agent.newuser"];
            var friend = users["agent.friend"];
            var stranger = users["agent.stranger"];

            // 1. Pending join requests to the private group.
            dbContext.GroupJoinRequests.AddRange(
                new GroupJoinRequest { GroupId = privateGroup.Id, UserId = newUser.Id, RequestedAt = baseTime.AddMinutes(30) },
                new GroupJoinRequest { GroupId = privateGroup.Id, UserId = stranger.Id, RequestedAt = baseTime.AddMinutes(31) });

            // 2. agent.friend is an approved private group member who has posts awaiting approval.
            dbContext.GroupUsers.Add(new GroupUser { GroupId = privateGroup.Id, UserId = friend.Id, JoinedAt = baseTime.AddMinutes(25), LastActiveAt = baseTime.AddMinutes(25) });
            privateGroup.MemberCount = 3;
            var pendingPost1 = new GroupPost { GroupId = privateGroup.Id, AuthorId = friend.Id, Content = "Agent test: pending post 1 awaiting approval (approve me).", CreatedAt = baseTime.AddMinutes(32), Status = PostStatus.Pending };
            var pendingPost2 = new GroupPost { GroupId = privateGroup.Id, AuthorId = friend.Id, Content = "Agent test: pending post 2 awaiting approval (decline me).", CreatedAt = baseTime.AddMinutes(33), Status = PostStatus.Pending };
            dbContext.GroupPosts.AddRange(pendingPost1, pendingPost2);

            // 3. Pending friend request from stranger to member.
            var pendingFriendship = new Friendship { RequesterId = stranger.Id, AddresseeId = member.Id, Status = FriendshipStatus.Pending, RequestedAt = baseTime.AddMinutes(34) };
            dbContext.Friendships.Add(pendingFriendship);

            // 6. Group 3: owner is creator, agent.moderator is the only other admin; plus a second system admin.
            var adminGroup = new Group
            {
                Name = "Agent Test Admin Group",
                Description = "Creator plus one non-creator admin, for last-admin and self-demotion tests.",
                CreatorId = owner.Id,
                IsPublic = true,
                IsActive = true,
                CreatedAt = baseTime.AddMinutes(35),
                LastActiveAt = baseTime.AddMinutes(35),
                MemberCount = 3,
            };
            dbContext.Groups.Add(adminGroup);

            // 8. Extra feed posts by member (public, friends-only, only-me).
            dbContext.Posts.AddRange(
                new Post { AuthorId = member.Id, Content = "Agent test: extra public post by member.", AudienceType = AudienceType.Public, CreatedAt = baseTime.AddMinutes(40) },
                new Post { AuthorId = member.Id, Content = "Agent test: extra friends-only post by member.", AudienceType = AudienceType.FriendsOnly, CreatedAt = baseTime.AddMinutes(41) },
                new Post { AuthorId = member.Id, Content = "Agent test: extra only-me post by member.", AudienceType = AudienceType.MeOnly, CreatedAt = baseTime.AddMinutes(42) });
            var threadPost = new Post { AuthorId = member.Id, Content = "Agent test: post with a deeply nested comment thread.", AudienceType = AudienceType.Public, CreatedAt = baseTime.AddMinutes(43) };
            dbContext.Posts.Add(threadPost);
            await dbContext.SaveChangesAsync();

            dbContext.GroupUsers.AddRange(
                new GroupUser { GroupId = adminGroup.Id, UserId = owner.Id, IsAdmin = true, JoinedAt = baseTime.AddMinutes(35), LastActiveAt = baseTime.AddMinutes(35) },
                new GroupUser { GroupId = adminGroup.Id, UserId = moderator.Id, IsAdmin = true, JoinedAt = baseTime.AddMinutes(35), LastActiveAt = baseTime.AddMinutes(35) },
                new GroupUser { GroupId = adminGroup.Id, UserId = member.Id, JoinedAt = baseTime.AddMinutes(35), LastActiveAt = baseTime.AddMinutes(35) });

            // 7. Group file records only; no binary is written, so listing works but downloads do not.
            dbContext.GroupFiles.AddRange(
                new GroupFile
                {
                    GroupId = openGroup.Id,
                    UploaderId = member.Id,
                    OriginalFileName = "agent-test-approved.txt",
                    StoredFileName = "agent-test-approved.txt",
                    Description = "Seeded approved file record (no file on disk).",
                    FileSize = 1024,
                    ContentType = "text/plain",
                    UploadedAt = baseTime.AddMinutes(45),
                    Status = GroupFileStatus.Approved,
                    ReviewedById = owner.Id,
                    ReviewedAt = baseTime.AddMinutes(46),
                    VirusScanCompleted = true,
                    VirusScanPassed = true,
                    VirusScanCompletedAt = baseTime.AddMinutes(45),
                },
                new GroupFile
                {
                    GroupId = openGroup.Id,
                    UploaderId = member.Id,
                    OriginalFileName = "agent-test-pending.txt",
                    StoredFileName = "agent-test-pending.txt",
                    Description = "Seeded pending file record (no file on disk).",
                    FileSize = 2048,
                    ContentType = "text/plain",
                    UploadedAt = baseTime.AddMinutes(47),
                    Status = GroupFileStatus.Pending,
                });

            // 5. Comment chain: top-level comment plus 4 nested replies (depth 0..4). MaxFeedPostCommentDepth is 4.
            Comment? parent = null;
            for (var depth = 0; depth <= 4; depth++)
            {
                var comment = new Comment
                {
                    PostId = threadPost.Id,
                    AuthorId = depth % 2 == 0 ? friend.Id : owner.Id,
                    Content = $"Agent test: comment depth {depth}.",
                    CreatedAt = baseTime.AddMinutes(50 + depth),
                    ParentCommentId = parent?.Id,
                };
                dbContext.Comments.Add(comment);
                await dbContext.SaveChangesAsync();
                parent = comment;
            }

            threadPost.CommentCount = 5;

            // 4. Notifications. ExpiresAt is null: cleanup only deletes rows with ExpiresAt < now, so these are never purged.
            dbContext.UserNotifications.AddRange(
                new UserNotification
                {
                    UserId = member.Id,
                    Type = NotificationType.FriendRequest,
                    TemplateKey = "FriendRequest",
                    Data = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        NavigationUrl = "/friends?tab=requests",
                        FriendshipId = pendingFriendship.Id,
                        RequesterId = stranger.Id,
                        RequesterName = "Stella Stranger",
                    }),
                    IsRead = false,
                    CreatedAt = baseTime.AddMinutes(34),
                    ExpiresAt = null,
                },
                new UserNotification { UserId = member.Id, Type = NotificationType.System, Message = "Agent test: unread system notification for member.", IsRead = false, CreatedAt = baseTime.AddMinutes(60), ExpiresAt = null },
                new UserNotification { UserId = member.Id, Type = NotificationType.System, Message = "Agent test: read system notification for member.", IsRead = true, CreatedAt = baseTime.AddMinutes(61), ExpiresAt = null },
                new UserNotification { UserId = owner.Id, Type = NotificationType.System, Message = "Agent test: unread system notification for owner.", IsRead = false, CreatedAt = baseTime.AddMinutes(62), ExpiresAt = null },
                new UserNotification { UserId = owner.Id, Type = NotificationType.System, Message = "Agent test: second unread notification for owner.", IsRead = false, CreatedAt = baseTime.AddMinutes(63), ExpiresAt = null },
                new UserNotification { UserId = owner.Id, Type = NotificationType.System, Message = "Agent test: read system notification for owner.", IsRead = true, CreatedAt = baseTime.AddMinutes(64), ExpiresAt = null });

            await dbContext.SaveChangesAsync();

            // 5. Feed content reports for the system moderator dashboard. Times are relative to now so
            // that the stale/fresh split (Moderation:StaleReportDays, default 3) holds on every seed run.
            var now = DateTime.UtcNow;
            var spamPost = new Post { AuthorId = stranger.Id, Content = "Agent test: spammy post by stranger.", AudienceType = AudienceType.Public, CreatedAt = baseTime.AddMinutes(70) };
            var dubiousPost = new Post { AuthorId = friend.Id, Content = "Agent test: dubious claim post by friend.", AudienceType = AudienceType.Public, CreatedAt = baseTime.AddMinutes(71) };
            dbContext.Posts.AddRange(spamPost, dubiousPost);
            await dbContext.SaveChangesAsync();

            var strangerComment = await dbContext.Comments.FirstAsync(c => c.PostId == publicPost.Id && c.AuthorId == stranger.Id);
            dbContext.FeedContentReports.AddRange(
                new FeedContentReport { PostId = publicPost.Id, ContentAuthorId = publicPost.AuthorId, ReporterId = friend.Id, Reason = ReportReason.Spam, Description = "Agent test: stale report (5 days old).", CreatedAt = now.AddDays(-5) },
                new FeedContentReport { PostId = publicPost.Id, ContentAuthorId = publicPost.AuthorId, ReporterId = owner.Id, Reason = ReportReason.HarassmentOrBullying, Description = "Agent test: second report on the same post (4 days old).", CreatedAt = now.AddDays(-4) },
                new FeedContentReport { PostId = dubiousPost.Id, ContentAuthorId = friend.Id, ReporterId = stranger.Id, Reason = ReportReason.Misinformation, Description = "Agent test: stale report (10 days old).", CreatedAt = now.AddDays(-10) },
                new FeedContentReport { PostId = spamPost.Id, ContentAuthorId = stranger.Id, ReporterId = member.Id, Reason = ReportReason.Spam, Description = "Agent test: fresh report (2 hours old).", CreatedAt = now.AddHours(-2) },
                new FeedContentReport { CommentId = strangerComment.Id, ContentAuthorId = stranger.Id, ReporterId = friend.Id, Reason = ReportReason.HateSpeech, Description = "Agent test: fresh comment report (1 hour old).", CreatedAt = now.AddHours(-1) });
            await dbContext.SaveChangesAsync();

            // 8. Group content reports across groups 1-3 for the system moderator Group Reports tab.
            var gOpenPost = new GroupPost { GroupId = openGroup.Id, AuthorId = member.Id, Content = "Agent test: reported open-group post (stale).", CreatedAt = baseTime.AddMinutes(80), Status = PostStatus.Posted };
            var gPrivatePost = new GroupPost { GroupId = privateGroup.Id, AuthorId = friend.Id, Content = "Agent test: reported private-group post (fresh).", CreatedAt = baseTime.AddMinutes(81), Status = PostStatus.Posted };
            var gAdminPost = new GroupPost { GroupId = adminGroup.Id, AuthorId = owner.Id, Content = "Agent test: reported admin-group post (stale).", CreatedAt = baseTime.AddMinutes(82), Status = PostStatus.Posted };
            var gOpenCommentPost = new GroupPost { GroupId = openGroup.Id, AuthorId = owner.Id, Content = "Agent test: open-group post with a reported comment.", CreatedAt = baseTime.AddMinutes(83), Status = PostStatus.Posted };
            dbContext.GroupPosts.AddRange(gOpenPost, gPrivatePost, gAdminPost, gOpenCommentPost);
            await dbContext.SaveChangesAsync();

            var gComment = new GroupPostComment { PostId = gOpenCommentPost.Id, AuthorId = member.Id, Content = "Agent test: reported group comment.", CreatedAt = baseTime.AddMinutes(84) };
            dbContext.GroupPostComments.Add(gComment);
            gOpenCommentPost.CommentCount = 1;
            await dbContext.SaveChangesAsync();

            dbContext.GroupContentReports.AddRange(
                new GroupContentReport { GroupId = openGroup.Id, PostId = gOpenPost.Id, ReporterId = owner.Id, Reason = ReportReason.Spam, Description = "Agent test: group report, stale (6 days old).", CreatedAt = now.AddDays(-6) },
                new GroupContentReport { GroupId = adminGroup.Id, PostId = gAdminPost.Id, ReporterId = moderator.Id, Reason = ReportReason.HarassmentOrBullying, Description = "Agent test: group report in group 3, stale (4 days old).", CreatedAt = now.AddDays(-4) },
                new GroupContentReport { GroupId = privateGroup.Id, PostId = gPrivatePost.Id, ReporterId = owner.Id, Reason = ReportReason.Misinformation, Description = "Agent test: group report in private group, fresh (3 hours old).", CreatedAt = now.AddHours(-3) },
                new GroupContentReport { GroupId = openGroup.Id, CommentId = gComment.Id, ReporterId = owner.Id, Reason = ReportReason.HateSpeech, Description = "Agent test: group comment report, fresh (1 hour old).", CreatedAt = now.AddHours(-1) });
            await dbContext.SaveChangesAsync();
        }
    }
}
