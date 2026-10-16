using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IUserModerationRepository"/>.
    /// </summary>
    public class UserModerationRepository : IUserModerationRepository
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserModerationRepository"/> class.
        /// </summary>
        /// <param name="contextFactory">Factory for creating database contexts.</param>
        public UserModerationRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ModerationUserSummary>> SearchUsersAsync(string term, int take, CancellationToken cancellationToken = default)
        {
            var pattern = "%" + term.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await context.Users.AsNoTracking()
                .Where(u => EF.Functions.ILike(u.UserName!, pattern)
                    || EF.Functions.ILike(u.FirstName + " " + u.LastName, pattern))
                .OrderBy(u => u.UserName)
                .Take(take)
                .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName })
                .ToListAsync(cancellationToken);
            return rows.Select(u => new ModerationUserSummary(u.Id, u.UserName ?? string.Empty, (u.FirstName + " " + u.LastName).Trim())).ToList();
        }

        /// <inheritdoc/>
        public async Task<ModerationUserSummary?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var user = await context.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName })
                .FirstOrDefaultAsync(cancellationToken);
            return user == null
                ? null
                : new ModerationUserSummary(user.Id, user.UserName ?? string.Empty, (user.FirstName + " " + user.LastName).Trim());
        }

        /// <inheritdoc/>
        public async Task<UserWarning> AddWarningAsync(UserWarning warning, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            context.UserWarnings.Add(warning);
            await context.SaveChangesAsync(cancellationToken);
            return warning;
        }

        /// <inheritdoc/>
        public async Task<UserSuspension> AddSuspensionAsync(UserSuspension suspension, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            context.UserSuspensions.Add(suspension);
            await context.SaveChangesAsync(cancellationToken);
            return suspension;
        }

        /// <inheritdoc/>
        public async Task<UserSuspension?> TryAddSuspensionAsync(UserSuspension suspension, DateTime nowUtc, int? ignoreSuspensionId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            // Serializes concurrent suspensions of the same user until this transaction ends.
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({suspension.UserId}, 0))", cancellationToken);

            var conflict = await context.UserSuspensions.AnyAsync(s =>
                s.UserId == suspension.UserId && s.LiftedAt == null && (s.EndsAt == null || s.EndsAt > nowUtc)
                && (ignoreSuspensionId == null || s.Id != ignoreSuspensionId), cancellationToken);
            if (conflict)
            {
                return null;
            }

            context.UserSuspensions.Add(suspension);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return suspension;
        }

        /// <inheritdoc/>
        public async Task DeleteSuspensionAsync(int suspensionId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await context.UserSuspensions.Where(s => s.Id == suspensionId).ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<UserSuspension?> GetSuspensionAsync(int suspensionId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.UserSuspensions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == suspensionId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> MarkSuspensionLiftedAsync(int suspensionId, string liftedByUserId, DateTime liftedAtUtc, string? note, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var updated = await context.UserSuspensions
                .Where(s => s.Id == suspensionId && s.LiftedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.LiftedAt, liftedAtUtc)
                    .SetProperty(x => x.LiftedByUserId, liftedByUserId)
                    .SetProperty(x => x.LiftNote, note), cancellationToken);
            return updated > 0;
        }

        /// <inheritdoc/>
        public async Task<UserSuspension?> GetActiveSuspensionAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.UserSuspensions.AsNoTracking()
                .Where(s => s.UserId == userId && s.LiftedAt == null && (s.EndsAt == null || s.EndsAt > nowUtc))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<UserWarningItem>> GetWarningsAsync(string userId, int take, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await context.UserWarnings.AsNoTracking()
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedAt)
                .Take(take)
                .Select(w => new { w.Id, w.Reason, w.RelatedReportId, w.CreatedAt, w.AcknowledgedAt, w.IssuedByUserId, w.IssuedBy.FirstName, w.IssuedBy.LastName })
                .ToListAsync(cancellationToken);
            return rows.Select(w => new UserWarningItem(w.Id, w.Reason, w.RelatedReportId, w.CreatedAt, w.AcknowledgedAt, w.IssuedByUserId, (w.FirstName + " " + w.LastName).Trim())).ToList();
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<UserSuspensionItem>> GetSuspensionsAsync(string userId, int take, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await context.UserSuspensions.AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(take)
                .Select(s => new { s.Id, s.Reason, s.RelatedReportId, s.CreatedAt, s.EndsAt, s.LiftedAt, s.LiftNote, s.SuspendedByUserId, s.SuspendedBy.FirstName, s.SuspendedBy.LastName })
                .ToListAsync(cancellationToken);
            return rows.Select(s => new UserSuspensionItem(s.Id, s.Reason, s.RelatedReportId, s.CreatedAt, s.EndsAt, s.LiftedAt, s.LiftNote, s.SuspendedByUserId, (s.FirstName + " " + s.LastName).Trim(), false, false)).ToList();
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<UserReportItem>> GetReportsAgainstUserAsync(string userId, int take, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.FeedContentReports.AsNoTracking()
                .Where(r => r.ContentAuthorId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(take)
                .Select(r => new UserReportItem(r.Id, r.Reason, r.Status, r.CreatedAt))
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> AcknowledgeWarningAsync(int warningId, string userId, DateTime acknowledgedAtUtc, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var exists = await context.UserWarnings.AnyAsync(w => w.Id == warningId && w.UserId == userId, cancellationToken);
            if (!exists)
            {
                return false;
            }

            await context.UserWarnings
                .Where(w => w.Id == warningId && w.UserId == userId && w.AcknowledgedAt == null)
                .ExecuteUpdateAsync(w => w.SetProperty(x => x.AcknowledgedAt, acknowledgedAtUtc), cancellationToken);
            return true;
        }
    }
}
