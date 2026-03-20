namespace FreeSpeakWeb.Data
{
    /// <summary>
    /// Enum representing different user role types for badge display.
    /// </summary>
    public enum RoleType
    {
        /// <summary>
        /// No special role.
        /// </summary>
        None = 0,

        /// <summary>
        /// Group administrator role.
        /// </summary>
        GroupAdmin = 1,

        /// <summary>
        /// Group moderator role.
        /// </summary>
        GroupModerator = 2,

        /// <summary>
        /// System administrator role.
        /// </summary>
        SystemAdmin = 3,

        /// <summary>
        /// System moderator role.
        /// </summary>
        SystemModerator = 4
    }
}
