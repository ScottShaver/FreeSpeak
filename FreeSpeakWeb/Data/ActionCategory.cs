namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the various categories of actions that can be logged in the audit system.
    /// Used to categorize user activities and system events for audit trail purposes.
    /// </summary>
    public enum ActionCategory
    {
        /// <summary>
        /// Action related to changes in a user's profile information.
        /// Includes updates to bio, display name, location, and other profile fields.
        /// </summary>
        UserProfileChange = 0,

        /// <summary>
        /// Action when a user successfully logs into the system.
        /// Includes timestamp and authentication method information.
        /// </summary>
        UserLogin = 1,

        /// <summary>
        /// Action when a user logs out of the system.
        /// Tracks session end time and logout method.
        /// </summary>
        UserLogout = 2,

        /// <summary>
        /// Action when a user creates a post in a group.
        /// Includes group identifier and post content summary.
        /// </summary>
        UserGroupPost = 3,

        /// <summary>
        /// Action when a user creates a feed post.
        /// Tracks personal or friends-only post creation.
        /// </summary>
        UserPost = 4,

        /// <summary>
        /// Action related to user personal data operations.
        /// Includes data export requests, data deletion, and privacy settings changes.
        /// </summary>
        UserPersonalData = 5,

        /// <summary>
        /// Action related to password changes or resets.
        /// Tracks password security operations for the user account.
        /// </summary>
        UserPassword = 6,

        /// <summary>
        /// Action when user preferences or settings are modified.
        /// Includes theme changes, notification settings, and other user preferences.
        /// </summary>
        UserPreference = 7,

        /// <summary>
        /// Action when a user searches for friends or views friend suggestions.
        /// Tracks friend discovery activities.
        /// </summary>
        UserFriendsFind = 8,

        /// <summary>
        /// Action when a user sends or receives friend requests.
        /// Includes both outgoing and incoming friend request events.
        /// </summary>
        UserFriendsRequest = 9,

        /// <summary>
        /// Action when a user searches for groups or browses group listings.
        /// Tracks group discovery activities.
        /// </summary>
        UserGroupFind = 10,

        /// <summary>
        /// Action when a user joins a group or requests to join a group.
        /// Includes both direct joins and join request submissions.
        /// </summary>
        UserGroupRequests = 11,

        /// <summary>
        /// Action when a user leaves a group.
        /// Tracks group membership termination events.
        /// </summary>
        UserGroupLeave = 12,

        /// <summary>
        /// Action when a user uploads files or media to the system.
        /// Includes profile pictures, post images, and other file uploads.
        /// </summary>
        UserUpload = 13,

        /// <summary>
        /// Action related to user notifications.
        /// Includes notification creation, delivery, and user interaction with notifications.
        /// </summary>
        UserNotification = 14,

        /// <summary>
        /// Action when a group admin creates a new group.
        /// Tracks group creation by administrators with initial settings.
        /// </summary>
        GroupAdminCreateGroup = 15,

        /// <summary>
        /// Action when a group admin edits group settings or information.
        /// Tracks modifications to group name, description, visibility, and other settings.
        /// </summary>
        GroupAdminEditGroup = 16,

        /// <summary>
        /// Action when a group admin bans a user from the group.
        /// Tracks user bans including the reason and banned user details.
        /// </summary>
        GroupAdminBanUser = 17,

        /// <summary>
        /// Action when a group admin closes or deletes a group.
        /// Tracks group closure with reason and final state.
        /// </summary>
        GroupAdminCloseGroup = 18,

        /// <summary>
        /// Action when a group admin approves a user's join request.
        /// Tracks join request approvals with requester details.
        /// </summary>
        GroupAdminApproveJoinRequest = 19,

        /// <summary>
        /// Action when a group admin changes a member's role.
        /// Tracks role changes such as promoting to moderator or demoting from admin.
        /// </summary>
        GroupAdminChangeMemberRole = 20,

        /// <summary>
        /// Action when a group admin denies a user's join request.
        /// Tracks join request denials with requester details and optional reason.
        /// </summary>
        GroupAdminDenyJoinRequest = 21,

        /// <summary>
        /// Action when a group moderator approves a post for publication.
        /// Tracks post approvals in moderated groups.
        /// </summary>
        GroupModeratorApprovePost = 22,

        /// <summary>
        /// Action when a group moderator declines a post.
        /// Tracks post rejections with reason in moderated groups.
        /// </summary>
        GroupModeratorDeclinePost = 23,

        /// <summary>
        /// Action when a group moderator removes an existing post.
        /// Tracks post removals with reason and post details.
        /// </summary>
        GroupModeratorRemovePost = 24,

        /// <summary>
        /// Action when a group moderator removes a comment from a post.
        /// Tracks comment removals with reason and comment details.
        /// </summary>
        GroupModeratorRemovePostComment = 25,

        /// <summary>
        /// Action when a system moderator removes a post across the platform.
        /// Tracks system-level post removals for policy violations.
        /// </summary>
        SystemModeratorRemovePost = 26,

        /// <summary>
        /// Action when a system moderator removes a comment across the platform.
        /// Tracks system-level comment removals for policy violations.
        /// </summary>
        SystemModeratorRemovePostComment = 27,

        /// <summary>
        /// Action when a user agrees to a group's rules to participate.
        /// Tracks user acknowledgment of group rules.
        /// </summary>
        UserGroupAgreeToRules = 28,

        /// <summary>
        /// Action when a user declines a group's rules.
        /// Tracks user declining to accept group rules.
        /// </summary>
        UserGroupDeclineRules = 29
    }
}
