using FreeSpeakWeb.Services.Abstractions;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// <see cref="IGroupContentRemoval"/> implemented on top of <see cref="GroupPostService"/>, whose delete methods
    /// authorize group admins and moderators as well as system moderators and administrators.
    /// </summary>
    public class GroupContentRemoval : IGroupContentRemoval
    {
        private readonly GroupPostService _groupPostService;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupContentRemoval"/> class.
        /// </summary>
        /// <param name="groupPostService">The service that deletes group posts and comments.</param>
        public GroupContentRemoval(GroupPostService groupPostService)
        {
            _groupPostService = groupPostService;
        }

        /// <inheritdoc/>
        public Task<(bool Success, string? ErrorMessage)> RemovePostAsModeratorAsync(int postId, string moderatorId)
            => _groupPostService.DeleteGroupPostAsync(postId, moderatorId, recordModeratorRemoval: false);

        /// <inheritdoc/>
        public Task<(bool Success, string? ErrorMessage)> RemoveCommentAsModeratorAsync(int commentId, string moderatorId)
            => _groupPostService.DeleteCommentAsync(commentId, moderatorId, recordModeratorRemoval: false);
    }
}
