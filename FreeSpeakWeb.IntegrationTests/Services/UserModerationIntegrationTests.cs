using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.IntegrationTests.Infrastructure;
using FreeSpeakWeb.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FreeSpeakWeb.IntegrationTests.Services
{
    /// <summary>
    /// Integration tests for user warning and suspension persistence against PostgreSQL.
    /// </summary>
    public class UserModerationIntegrationTests : IntegrationTestBase
    {
        private UserModerationRepository CreateRepository() => new(CreateDbContextFactory());

        private static ApplicationUser NewUser(string id) => new()
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpper(),
            Email = $"{id}@example.com",
            NormalizedEmail = $"{id}@example.com".ToUpper(),
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = id,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        private async Task<(string Target, string Moderator)> SeedUsersAsync()
        {
            var target = "target-" + Guid.NewGuid().ToString("N");
            var moderator = "mod-" + Guid.NewGuid().ToString("N");
            await using var context = CreateDbContext();
            context.Users.AddRange(NewUser(target), NewUser(moderator));
            await context.SaveChangesAsync();
            return (target, moderator);
        }

        /// <summary>A stored warning is returned in the user history and can be acknowledged once.</summary>
        [Fact]
        public async Task Warning_IsStoredListedAndAcknowledgedOnce()
        {
            var (target, moderator) = await SeedUsersAsync();
            var repo = CreateRepository();

            var warning = await repo.AddWarningAsync(new UserWarning { UserId = target, IssuedByUserId = moderator, Reason = "be nice" });
            var list = await repo.GetWarningsAsync(target, 10);
            var first = await repo.AcknowledgeWarningAsync(warning.Id, target, DateTime.UtcNow);
            var second = await repo.AcknowledgeWarningAsync(warning.Id, target, DateTime.UtcNow);

            list.Should().ContainSingle(w => w.Id == warning.Id && w.Reason == "be nice");
            first.Should().BeTrue();
            second.Should().BeFalse();
        }

        /// <summary>A timed suspension is active until it ends and is no longer active afterwards.</summary>
        [Fact]
        public async Task ActiveSuspension_ExpiresAtEndsAt()
        {
            var (target, moderator) = await SeedUsersAsync();
            var repo = CreateRepository();
            var now = DateTime.UtcNow;
            await repo.AddSuspensionAsync(new UserSuspension { UserId = target, SuspendedByUserId = moderator, Reason = "spam", EndsAt = now.AddDays(1) });

            (await repo.GetActiveSuspensionAsync(target, now)).Should().NotBeNull();
            (await repo.GetActiveSuspensionAsync(target, now.AddDays(2))).Should().BeNull();
        }

        /// <summary>A permanent suspension (no end date) stays active.</summary>
        [Fact]
        public async Task ActiveSuspension_Permanent_NeverExpires()
        {
            var (target, moderator) = await SeedUsersAsync();
            var repo = CreateRepository();
            await repo.AddSuspensionAsync(new UserSuspension { UserId = target, SuspendedByUserId = moderator, Reason = "abuse", EndsAt = null });

            var active = await repo.GetActiveSuspensionAsync(target, DateTime.UtcNow.AddYears(50));

            active.Should().NotBeNull();
            active!.EndsAt.Should().BeNull();
        }

        /// <summary>Lifting records who lifted and when, ends the active state and cannot be repeated.</summary>
        [Fact]
        public async Task Lift_EndsSuspensionOnce()
        {
            var (target, moderator) = await SeedUsersAsync();
            var repo = CreateRepository();
            var suspension = await repo.AddSuspensionAsync(new UserSuspension { UserId = target, SuspendedByUserId = moderator, Reason = "spam", EndsAt = null });

            var first = await repo.MarkSuspensionLiftedAsync(suspension.Id, moderator, DateTime.UtcNow, "resolved");
            var second = await repo.MarkSuspensionLiftedAsync(suspension.Id, moderator, DateTime.UtcNow, "again");

            first.Should().BeTrue();
            second.Should().BeFalse();
            (await repo.GetActiveSuspensionAsync(target, DateTime.UtcNow)).Should().BeNull();
            var stored = await repo.GetSuspensionAsync(suspension.Id);
            stored!.LiftedByUserId.Should().Be(moderator);
            stored.LiftNote.Should().Be("resolved");
        }

        /// <summary>Simultaneous suspensions of the same user cannot both succeed.</summary>
        [Fact]
        public async Task TryAddSuspensionAsync_ConcurrentCalls_OnlyOneSucceeds()
        {
            var (target, moderator) = await SeedUsersAsync();
            var repo = CreateRepository();
            var now = DateTime.UtcNow;

            var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => repo.TryAddSuspensionAsync(
                new UserSuspension { UserId = target, SuspendedByUserId = moderator, Reason = "spam", CreatedAt = now, EndsAt = now.AddDays(1) },
                now, null))).ToArray();
            var results = await Task.WhenAll(tasks);

            results.Count(r => r != null).Should().Be(1);
            await using var verify = CreateDbContext();
            verify.UserSuspensions.Count(s => s.UserId == target).Should().Be(1);
        }

        /// <summary>A stale active record can be ignored so the user can be suspended again.</summary>
        [Fact]
        public async Task TryAddSuspensionAsync_IgnoresStaleRecord()
        {
            var (target, moderator) = await SeedUsersAsync();
            var repo = CreateRepository();
            var now = DateTime.UtcNow;
            var stale = await repo.AddSuspensionAsync(new UserSuspension { UserId = target, SuspendedByUserId = moderator, Reason = "old", CreatedAt = now, EndsAt = now.AddDays(1) });

            (await repo.TryAddSuspensionAsync(new UserSuspension { UserId = target, SuspendedByUserId = moderator, Reason = "n", CreatedAt = now }, now, null)).Should().BeNull();
            (await repo.TryAddSuspensionAsync(new UserSuspension { UserId = target, SuspendedByUserId = moderator, Reason = "n", CreatedAt = now }, now, stale.Id)).Should().NotBeNull();
        }

        /// <summary>User search matches by user name and ignores very short terms' wildcards safely.</summary>
        [Fact]
        public async Task SearchUsersAsync_FindsByUserName()
        {
            var (target, _) = await SeedUsersAsync();
            var repo = CreateRepository();

            var results = await repo.SearchUsersAsync(target[..12], 10);

            results.Should().Contain(u => u.UserId == target);
        }

        /// <summary>Deleting a user removes their warnings and suspensions.</summary>
        [Fact]
        public async Task DeletingUser_CascadesWarningsAndSuspensions()
        {
            var (target, moderator) = await SeedUsersAsync();
            var repo = CreateRepository();
            await repo.AddWarningAsync(new UserWarning { UserId = target, IssuedByUserId = moderator, Reason = "w" });
            await repo.AddSuspensionAsync(new UserSuspension { UserId = target, SuspendedByUserId = moderator, Reason = "s", EndsAt = null });

            await using (var context = CreateDbContext())
            {
                context.Users.Remove(await context.Users.SingleAsync(u => u.Id == target));
                await context.SaveChangesAsync();
            }

            await using var verify = CreateDbContext();
            (await verify.Set<UserWarning>().CountAsync(w => w.UserId == target)).Should().Be(0);
            (await verify.Set<UserSuspension>().CountAsync(s => s.UserId == target)).Should().Be(0);
        }
    }
}
