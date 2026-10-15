namespace FreeSpeakWeb.Services.Settings
{
    /// <summary>
    /// The kind of value a system setting field holds.
    /// </summary>
    public enum SettingFieldType
    {
        /// <summary>Free text.</summary>
        Text,

        /// <summary>Whole number.</summary>
        Integer,

        /// <summary>True or false.</summary>
        Boolean,

        /// <summary>Write-only secret that is encrypted at rest.</summary>
        Secret,

        /// <summary>A standard log level name.</summary>
        LogLevel
    }

    /// <summary>
    /// Extra validation applied to a text field.
    /// </summary>
    public enum SettingTextRule
    {
        /// <summary>No extra rule.</summary>
        None,

        /// <summary>Must be a valid email address.</summary>
        Email,

        /// <summary>Must be a non-empty host name or address.</summary>
        Host
    }

    /// <summary>
    /// Declarative description of one editable system setting.
    /// </summary>
    /// <param name="Key">The configuration key path (for example <c>VirusScan:ClamAvPort</c>).</param>
    /// <param name="Type">The value type.</param>
    /// <param name="LabelKey">The localization key of the label, or the literal label when <paramref name="LabelIsLiteral"/> is true.</param>
    /// <param name="Default">The value used when neither configuration source provides one.</param>
    /// <param name="Min">Minimum for integer fields.</param>
    /// <param name="Max">Maximum for integer fields.</param>
    /// <param name="Required">Whether a value must be present.</param>
    /// <param name="Invert">Whether the shown boolean is the inverse of the stored one.</param>
    /// <param name="HelpKey">Optional localization key of the help text.</param>
    /// <param name="Section">Optional localization key of a sub-heading shown above the field.</param>
    /// <param name="Rule">Extra text validation.</param>
    /// <param name="LabelIsLiteral">Whether <paramref name="LabelKey"/> is shown as is.</param>
    public sealed record SettingFieldDefinition(
        string Key,
        SettingFieldType Type,
        string LabelKey,
        string Default = "",
        long Min = 0,
        long Max = int.MaxValue,
        bool Required = false,
        bool Invert = false,
        string? HelpKey = null,
        string? Section = null,
        SettingTextRule Rule = SettingTextRule.None,
        bool LabelIsLiteral = false)
    {
        /// <summary>
        /// Gets a value indicating whether the field is a secret.
        /// </summary>
        public bool IsSecret => Type == SettingFieldType.Secret;
    }

    /// <summary>
    /// Declarative description of a group of system settings shown on one page.
    /// </summary>
    /// <param name="Id">The URL-safe group identifier.</param>
    /// <param name="NameKey">The localization key of the group name.</param>
    /// <param name="DescriptionKey">The localization key of the group description.</param>
    /// <param name="RestartRequired">Whether changes only apply after an application restart.</param>
    /// <param name="Fields">The fields of the group.</param>
    public sealed record SettingGroupDefinition(
        string Id,
        string NameKey,
        string DescriptionKey,
        bool RestartRequired,
        IReadOnlyList<SettingFieldDefinition> Fields);
}
