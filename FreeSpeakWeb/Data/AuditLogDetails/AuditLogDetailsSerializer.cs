using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeSpeakWeb.Data.AuditLogDetails
{
    /// <summary>
    /// Provides helper methods for serializing audit log detail objects to JSON.
    /// </summary>
    public static class AuditLogDetailsSerializer
    {
        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        /// <summary>
        /// Serializes an audit log detail object to a JSON string.
        /// </summary>
        /// <typeparam name="T">The type of the detail object.</typeparam>
        /// <param name="details">The detail object to serialize.</param>
        /// <returns>A JSON string representation of the detail object.</returns>
        public static string ToJson<T>(T details) where T : class
        {
            return JsonSerializer.Serialize(details, DefaultOptions);
        }

        /// <summary>
        /// Deserializes a JSON string to an audit log detail object.
        /// </summary>
        /// <typeparam name="T">The type of the detail object.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized detail object, or null if deserialization fails.</returns>
        public static T? FromJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json, DefaultOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Serializes an audit log detail object to a formatted (indented) JSON string.
        /// Useful for display purposes.
        /// </summary>
        /// <typeparam name="T">The type of the detail object.</typeparam>
        /// <param name="details">The detail object to serialize.</param>
        /// <returns>A formatted JSON string representation of the detail object.</returns>
        public static string ToFormattedJson<T>(T details) where T : class
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };

            return JsonSerializer.Serialize(details, options);
        }
    }
}
