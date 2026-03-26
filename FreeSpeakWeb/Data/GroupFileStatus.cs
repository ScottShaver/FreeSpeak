namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Defines the approval status of a group file upload.
    /// Used to control file visibility when groups require file approval before sharing.
    /// </summary>
    public enum GroupFileStatus
    {
        /// <summary>
        /// The file is pending moderator or administrator approval.
        /// File is not visible to regular group members until approved.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// The file has been approved and is visible to all group members.
        /// Members can view file details and download the file.
        /// </summary>
        Approved = 1,

        /// <summary>
        /// The file was declined by a moderator or administrator.
        /// The file should be removed from storage after being declined.
        /// </summary>
        Declined = 2
    }
}
