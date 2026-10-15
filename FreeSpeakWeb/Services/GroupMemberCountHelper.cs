using FreeSpeakWeb.Data;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Keeps the denormalized <see cref="Group.MemberCount"/> consistent with the actual membership rows.
    /// Banning removes the membership row, so the number of <see cref="GroupUser"/> rows is the number of approved, non-banned members.
    /// </summary>
    internal static class GroupMemberCountHelper
    {
        /// <summary>
        /// Recomputes and persists a group's member count from its actual membership rows.
        /// Call after the membership change has been saved.
        /// </summary>
        /// <param name="context">The database context to use.</param>
        /// <param name="groupId">The unique identifier of the group.</param>
        /// <returns>The recomputed member count, or null if the group does not exist.</returns>
        public static async Task<int?> RecomputeAsync(ApplicationDbContext context, int groupId)
        {
            var group = await context.Groups.FindAsync(groupId);
            if (group == null)
            {
                return null;
            }

            var count = await context.GroupUsers.CountAsync(gu => gu.GroupId == groupId);
            if (group.MemberCount != count)
            {
                group.MemberCount = count;
                await context.SaveChangesAsync();
            }

            return count;
        }
    }
}
