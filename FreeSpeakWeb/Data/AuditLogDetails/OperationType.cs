namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Defines the types of operations that can be logged in audit log entries.
    /// This enumeration provides strongly-typed values that should be converted to strings
    /// when assigning to the OperationType property in audit log detail classes.
    /// </summary>
    public enum OperationTypeEnum
    {
        /// <summary>
        /// Account has been disabled.
        /// </summary>
        Disable,

        /// <summary>
        /// Accept an action or request.
        /// </summary>
        Accept,

        /// <summary>
        /// Add an item or element.
        /// </summary>
        Add,

        /// <summary>
        /// User has been blocked.
        /// </summary>
        Blocked,

        /// <summary>
        /// Multiple items have been deleted in bulk.
        /// </summary>
        BulkDeleted,

        /// <summary>
        /// Multiple items have been marked as read in bulk.
        /// </summary>
        BulkRead,

        /// <summary>
        /// Cancel an action or request.
        /// </summary>
        Cancel,

        /// <summary>
        /// Copy a link to clipboard.
        /// </summary>
        CopyLink,

        /// <summary>
        /// Create a new item or entity.
        /// </summary>
        Create,

        /// <summary>
        /// Download personal data.
        /// </summary>
        Download,

        /// <summary>
        /// Request or action has been declined.
        /// </summary>
        Decline,

        /// <summary>
        /// Delete an item or entity.
        /// </summary>
        Delete,

        /// <summary>
        /// Delete all read items.
        /// </summary>
        DeleteAllRead,

        /// <summary>
        /// Join a group directly without a request.
        /// </summary>
        DirectJoin,

        /// <summary>
        /// Edit an existing item or entity.
        /// </summary>
        Edit,

        /// <summary>
        /// Forgot password request initiated.
        /// </summary>
        ForgotPassword,

        /// <summary>
        /// Join request has been cancelled.
        /// </summary>
        JoinRequestCanceled,

        /// <summary>
        /// Join request has been sent.
        /// </summary>
        JoinRequestSent,

        /// <summary>
        /// Mark all notifications as read.
        /// </summary>
        MarkAllAsRead,

        /// <summary>
        /// Mute notifications for a group post.
        /// </summary>
        MuteGroupPost,

        /// <summary>
        /// Mute notifications for a post.
        /// </summary>
        MutePost,

        /// <summary>
        /// Pin an item to keep it at the top.
        /// </summary>
        Pin,

        /// <summary>
        /// Item has been read.
        /// </summary>
        Read,

        /// <summary>
        /// Reject an action or request.
        /// </summary>
        Reject,

        /// <summary>
        /// Remove an item or element.
        /// </summary>
        Remove,

        /// <summary>
        /// Reply to a comment or post.
        /// </summary>
        Reply,

        /// <summary>
        /// Resend confirmation email.
        /// </summary>
        ResendConfirmation,

        /// <summary>
        /// Request has been cancelled.
        /// </summary>
        RequestCancel,

        /// <summary>
        /// Request has been submitted.
        /// </summary>
        RequestSubmit,

        /// <summary>
        /// Password reset operation.
        /// </summary>
        Reset,

        /// <summary>
        /// Send a message or notification.
        /// </summary>
        Send,

        /// <summary>
        /// Verification email has been sent.
        /// </summary>
        SendVerification,

        /// <summary>
        /// Set a value or preference.
        /// </summary>
        Set,

        /// <summary>
        /// Unmute notifications for a group post.
        /// </summary>
        UnmuteGroupPost,

        /// <summary>
        /// Unmute notifications for a post.
        /// </summary>
        UnmutePost,

        /// <summary>
        /// Unpin an item that was previously pinned.
        /// </summary>
        Unpin,

        /// <summary>
        /// Update an existing item or entity.
        /// </summary>
        Update
    }
}
