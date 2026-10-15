using System.Globalization;
using System.Net.Mail;

namespace FreeSpeakWeb.Services.Settings
{
    /// <summary>
    /// Server-side validation and normalization of submitted setting values.
    /// </summary>
    public static class SettingsValidator
    {
        /// <summary>
        /// Normalizes a submitted value for storage (trims, canonicalizes booleans, integers and log levels).
        /// </summary>
        /// <param name="field">The field definition.</param>
        /// <param name="raw">The submitted value.</param>
        /// <returns>The normalized displayed value; unparsable values are returned trimmed.</returns>
        public static string Normalize(SettingFieldDefinition field, string? raw)
        {
            var value = (raw ?? string.Empty).Trim();
            switch (field.Type)
            {
                case SettingFieldType.Boolean:
                    return bool.TryParse(value, out var b) ? (b ? "true" : "false") : value;
                case SettingFieldType.Integer:
                    return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                        ? n.ToString(CultureInfo.InvariantCulture)
                        : value;
                case SettingFieldType.LogLevel:
                    return SettingsCatalog.LogLevels.FirstOrDefault(l => string.Equals(l, value, StringComparison.OrdinalIgnoreCase)) ?? value;
                default:
                    return value;
            }
        }

        /// <summary>
        /// Validates the submitted values of a group.
        /// </summary>
        /// <param name="group">The group definition.</param>
        /// <param name="values">Submitted displayed values keyed by configuration key (missing keys are not validated).</param>
        /// <param name="secretIsSet">Tells whether a secret key already has a configured value.</param>
        /// <returns>The validation errors; empty when valid.</returns>
        public static IReadOnlyList<SettingValidationError> Validate(
            SettingGroupDefinition group,
            IReadOnlyDictionary<string, string?> values,
            Func<string, bool> secretIsSet)
        {
            var errors = new List<SettingValidationError>();

            foreach (var key in values.Keys)
            {
                if (group.Fields.All(f => f.Key != key))
                {
                    errors.Add(new SettingValidationError(key, "E_UnknownKey"));
                }
            }

            foreach (var field in group.Fields)
            {
                if (!values.TryGetValue(field.Key, out var raw))
                {
                    continue;
                }

                var value = Normalize(field, raw);
                switch (field.Type)
                {
                    case SettingFieldType.Secret:
                        break;
                    case SettingFieldType.Boolean:
                        if (value != "true" && value != "false")
                        {
                            errors.Add(new SettingValidationError(field.Key, "E_Boolean"));
                        }
                        break;
                    case SettingFieldType.Integer:
                        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                        {
                            errors.Add(new SettingValidationError(field.Key, "E_Integer"));
                        }
                        else if (n < field.Min || n > field.Max)
                        {
                            errors.Add(new SettingValidationError(field.Key, "E_Range", field.Min, field.Max));
                        }
                        break;
                    case SettingFieldType.LogLevel:
                        if (!SettingsCatalog.LogLevels.Contains(value))
                        {
                            errors.Add(new SettingValidationError(field.Key, "E_LogLevel"));
                        }
                        break;
                    default:
                        if (field.Required && value.Length == 0)
                        {
                            errors.Add(new SettingValidationError(field.Key, "E_Required"));
                        }
                        else if (value.Length > 0 && field.Rule == SettingTextRule.Email && !IsEmail(value))
                        {
                            errors.Add(new SettingValidationError(field.Key, "E_Email"));
                        }
                        else if (value.Length > 0 && field.Rule == SettingTextRule.Host && !IsHost(value))
                        {
                            errors.Add(new SettingValidationError(field.Key, "E_Host"));
                        }
                        else if (value.Length > 500)
                        {
                            errors.Add(new SettingValidationError(field.Key, "E_TooLong", 500));
                        }
                        break;
                }
            }

            ValidateCrossField(group, values, secretIsSet, errors);
            return errors;
        }

        private static void ValidateCrossField(
            SettingGroupDefinition group,
            IReadOnlyDictionary<string, string?> values,
            Func<string, bool> secretIsSet,
            List<SettingValidationError> errors)
        {
            string Get(string key) => values.TryGetValue(key, out var v) ? (v ?? string.Empty).Trim() : string.Empty;

            if (group.Id == SettingsCatalog.SmtpGroupId
                && values.ContainsKey("Smtp:AuthenticationRequired")
                && Get("Smtp:AuthenticationRequired").Equals("true", StringComparison.OrdinalIgnoreCase)
                && Get("Smtp:Account").Length == 0)
            {
                errors.Add(new SettingValidationError("Smtp:Account", "E_AccountRequired"));
            }

            if (group.Id == "caching"
                && Get("Caching:UseRedis").Equals("true", StringComparison.OrdinalIgnoreCase)
                && Get("Caching:RedisConnectionString").Length == 0
                && !secretIsSet("Caching:RedisConnectionString"))
            {
                errors.Add(new SettingValidationError("Caching:RedisConnectionString", "E_RedisRequired"));
            }
        }

        private static bool IsEmail(string value)
        {
            try
            {
                var address = new MailAddress(value);
                return address.Address == value;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether a value looks like a plain host name or IP address.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <returns>True if valid.</returns>
        public static bool IsHost(string value) =>
            value.Length is > 0 and <= 253
            && Uri.CheckHostName(value) != UriHostNameType.Unknown;
    }
}
