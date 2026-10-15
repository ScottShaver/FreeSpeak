namespace FreeSpeakWeb.Services.Settings
{
    /// <summary>
    /// The declarative catalog of editable system setting groups.
    /// </summary>
    public static class SettingsCatalog
    {
        /// <summary>The standard log levels accepted for the Logging group.</summary>
        public static readonly IReadOnlyList<string> LogLevels =
            new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None" };

        /// <summary>The fixed logging categories that can be configured.</summary>
        public static readonly IReadOnlyList<string> LogCategories = new[]
        {
            "Default",
            "Microsoft",
            "Microsoft.AspNetCore",
            "Microsoft.Hosting.Lifetime",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.EntityFrameworkCore.Database.Command",
            "System"
        };

        /// <summary>The identifier of the Smtp group.</summary>
        public const string SmtpGroupId = "smtp";

        /// <summary>The identifier of the VirusScan group.</summary>
        public const string VirusScanGroupId = "virusscan";

        /// <summary>The identifier of the RateLimiting group.</summary>
        public const string RateLimitingGroupId = "ratelimiting";

        /// <summary>
        /// Gets all editable groups in display order.
        /// </summary>
        public static IReadOnlyList<SettingGroupDefinition> Groups { get; } = BuildGroups();

        /// <summary>
        /// Finds a group by identifier (case-insensitive).
        /// </summary>
        /// <param name="id">The group identifier.</param>
        /// <returns>The group, or null when unknown.</returns>
        public static SettingGroupDefinition? Find(string? id) =>
            Groups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));

        private static SettingFieldDefinition Int(string key, string label, string def, long min, long max, string? section = null, string? help = null) =>
            new(key, SettingFieldType.Integer, label, def, min, max, Required: true, HelpKey: help, Section: section);

        private static SettingFieldDefinition Bool(string key, string label, string def, string? help = null, bool invert = false) =>
            new(key, SettingFieldType.Boolean, label, def, HelpKey: help, Invert: invert);

        private static IEnumerable<SettingFieldDefinition> Policy(string name, string requests, string window, string queue)
        {
            var section = "S_" + name;
            yield return Int($"RateLimiting:{name}:MaxRequests", "L_MaxRequests", requests, name == "Login" ? 3 : 1, 1_000_000, section);
            yield return Int($"RateLimiting:{name}:WindowMinutes", "L_WindowMinutes", window, 1, 1440);
            yield return Int($"RateLimiting:{name}:QueueLimit", "L_QueueLimit", queue, 0, 100_000);
        }

        private static IReadOnlyList<SettingGroupDefinition> BuildGroups()
        {
            var rate = new List<SettingFieldDefinition> { Bool("RateLimiting:UseDistributed", "L_UseDistributed", "false", "H_UseDistributed") };
            rate.AddRange(Policy("Global", "500", "1", "20"));
            rate.AddRange(Policy("FileDownload", "100", "1", "10"));
            rate.AddRange(Policy("Login", "10", "5", "0"));
            rate.AddRange(Policy("Api", "200", "1", "5"));

            var logging = LogCategories
                .Select(c => new SettingFieldDefinition($"Logging:LogLevel:{c}", SettingFieldType.LogLevel, c, "Information", LabelIsLiteral: true))
                .ToList();

            return new[]
            {
                new SettingGroupDefinition("site", "G_Site", "GD_Site", false, new[]
                {
                    new SettingFieldDefinition("SiteName", SettingFieldType.Text, "L_SiteName", "FreeSpeak", Required: true),
                    Int("MaxFeedPostCommentDepth", "L_MaxCommentDepth", "4", 1, 20),
                    Int("MaxFeedPostDirectCommentCount", "L_MaxDirectComments", "1000", 1, 100_000),
                    Bool("AllowOpenGroupCreation", "L_AllowOpenGroupCreation", "true"),
                    Bool("AllowPostAttachments", "L_AllowPostAttachments", "true")
                }),
                new SettingGroupDefinition(SmtpGroupId, "G_Smtp", "GD_Smtp", false, new[]
                {
                    Bool("UseIdentityNoOpEmailSender", "L_Enabled", "true", "H_SmtpEnabled", invert: true),
                    new SettingFieldDefinition("Smtp:Server", SettingFieldType.Text, "L_Host", Required: true, Rule: SettingTextRule.Host),
                    Int("Smtp:Port", "L_Port", "587", 1, 65535),
                    new SettingFieldDefinition("Smtp:SenderName", SettingFieldType.Text, "L_SenderName", Required: true),
                    new SettingFieldDefinition("Smtp:SenderEmail", SettingFieldType.Text, "L_SenderEmail", Required: true, Rule: SettingTextRule.Email),
                    Bool("Smtp:AuthenticationRequired", "L_AuthenticationRequired", "true"),
                    new SettingFieldDefinition("Smtp:Account", SettingFieldType.Text, "L_Account"),
                    new SettingFieldDefinition("Smtp:Password", SettingFieldType.Secret, "L_Password", HelpKey: "H_Secret"),
                    Bool("Smtp:UseSsl", "L_UseSsl", "true")
                }),
                new SettingGroupDefinition(VirusScanGroupId, "G_VirusScan", "GD_VirusScan", false, new[]
                {
                    Bool("VirusScan:Enabled", "L_Enabled", "false"),
                    new SettingFieldDefinition("VirusScan:ClamAvHost", SettingFieldType.Text, "L_Host", "localhost", Required: true, Rule: SettingTextRule.Host),
                    Int("VirusScan:ClamAvPort", "L_Port", "3310", 1, 65535),
                    new SettingFieldDefinition("VirusScan:MaxStreamSizeBytes", SettingFieldType.Integer, "L_MaxStreamSizeBytes", "104857600", 1, long.MaxValue, Required: true),
                    Bool("VirusScan:FailOpen", "L_FailOpen", "false", "H_FailOpen"),
                    Int("VirusScan:TimeoutSeconds", "L_TimeoutSeconds", "60", 1, 3600)
                }),
                new SettingGroupDefinition(RateLimitingGroupId, "G_RateLimiting", "GD_RateLimiting", true, rate),
                new SettingGroupDefinition("profiling", "G_Profiling", "GD_Profiling", true, new[]
                {
                    Bool("Profiling:Enabled", "L_Enabled", "false"),
                    Bool("Profiling:ShowControls", "L_ShowControls", "true"),
                    Int("Profiling:MaxResults", "L_MaxResults", "100", 1, 10_000),
                    Bool("Profiling:ProfileDatabase", "L_ProfileDatabase", "true"),
                    Bool("Profiling:AllowOutsideDevelopment", "L_AllowOutsideDevelopment", "false", "H_AllowOutsideDevelopment")
                }),
                new SettingGroupDefinition("caching", "G_Caching", "GD_Caching", true, new[]
                {
                    Bool("Caching:UseRedis", "L_UseRedis", "false"),
                    new SettingFieldDefinition("Caching:RedisConnectionString", SettingFieldType.Secret, "L_RedisConnectionString", HelpKey: "H_Secret")
                }),
                new SettingGroupDefinition("logging", "G_Logging", "GD_Logging", false, logging)
            };
        }
    }
}
