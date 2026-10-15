using Microsoft.AspNetCore.DataProtection;
using Npgsql;

namespace FreeSpeakWeb.Configuration
{
    /// <summary>
    /// A raw row read from the SystemSettings table.
    /// </summary>
    /// <param name="Key">Configuration key path.</param>
    /// <param name="Value">Stored value (ciphertext when <paramref name="IsSecret"/> is true).</param>
    /// <param name="IsSecret">Whether the value is encrypted.</param>
    public sealed record SystemSettingRow(string Key, string Value, bool IsSecret);

    /// <summary>
    /// Reads rows for the <see cref="DatabaseConfigurationProvider"/>. Abstracted so the provider can be tested without a database.
    /// </summary>
    public interface ISystemSettingRowReader
    {
        /// <summary>
        /// Reads every row in the SystemSettings table.
        /// </summary>
        /// <returns>All rows.</returns>
        IReadOnlyList<SystemSettingRow> ReadAll();
    }

    /// <summary>
    /// Reads SystemSettings directly via Npgsql (no DbContext), because configuration is built before dependency injection exists.
    /// </summary>
    public sealed class NpgsqlSystemSettingRowReader : ISystemSettingRowReader
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="NpgsqlSystemSettingRowReader"/> class.
        /// </summary>
        /// <param name="connectionString">The PostgreSQL connection string (DefaultConnection).</param>
        public NpgsqlSystemSettingRowReader(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <inheritdoc/>
        public IReadOnlyList<SystemSettingRow> ReadAll()
        {
            var rows = new List<SystemSettingRow>();
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            using var command = new NpgsqlCommand("SELECT \"Key\", \"Value\", \"IsSecret\" FROM \"SystemSettings\"", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new SystemSettingRow(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)));
            }

            return rows;
        }
    }

    /// <summary>
    /// Allows the settings service to force the database configuration provider to reload immediately.
    /// </summary>
    public interface ISettingsConfigurationReloader
    {
        /// <summary>
        /// Re-reads the database rows and raises a configuration change notification if anything changed.
        /// </summary>
        void Reload();
    }

    /// <summary>
    /// Configuration source that layers rows from the SystemSettings table over the file-based configuration.
    /// </summary>
    public sealed class DatabaseConfigurationSource : IConfigurationSource, ISettingsConfigurationReloader
    {
        /// <summary>
        /// Default interval at which other instances pick up changes.
        /// </summary>
        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or sets the reader used to load rows.
        /// </summary>
        public ISystemSettingRowReader RowReader { get; set; } = default!;

        /// <summary>
        /// Gets or sets the function that decrypts secret values. When null, secret rows are ignored.
        /// </summary>
        public Func<string, string>? Unprotect { get; set; }

        /// <summary>
        /// Gets or sets the poll interval. Use <see cref="Timeout.InfiniteTimeSpan"/> to disable polling.
        /// </summary>
        public TimeSpan PollInterval { get; set; } = DefaultPollInterval;

        /// <summary>
        /// Gets the provider created by <see cref="Build"/>.
        /// </summary>
        public DatabaseConfigurationProvider? Provider { get; private set; }

        /// <inheritdoc/>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            Provider = new DatabaseConfigurationProvider(RowReader, Unprotect, PollInterval);
            return Provider;
        }

        /// <inheritdoc/>
        public void Reload() => Provider?.Reload();
    }

    /// <summary>
    /// Configuration provider backed by the SystemSettings table. Rows override earlier (file) sources.
    /// Tolerates a missing table or unreachable database by exposing no overrides.
    /// </summary>
    public sealed class DatabaseConfigurationProvider : ConfigurationProvider, IDisposable
    {
        private readonly ISystemSettingRowReader _reader;
        private readonly Func<string, string>? _unprotect;
        private readonly Timer? _timer;
        private readonly object _gate = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseConfigurationProvider"/> class.
        /// </summary>
        /// <param name="reader">Row reader.</param>
        /// <param name="unprotect">Decrypts secret values; null ignores secret rows.</param>
        /// <param name="pollInterval">Poll interval, or <see cref="Timeout.InfiniteTimeSpan"/> for none.</param>
        public DatabaseConfigurationProvider(ISystemSettingRowReader reader, Func<string, string>? unprotect, TimeSpan pollInterval)
        {
            _reader = reader;
            _unprotect = unprotect;
            if (pollInterval != Timeout.InfiniteTimeSpan && pollInterval > TimeSpan.Zero)
            {
                _timer = new Timer(_ => Reload(), null, pollInterval, pollInterval);
            }
        }

        /// <inheritdoc/>
        public override void Load()
        {
            var data = TryRead();
            Data = data ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Re-reads the table and raises a reload notification when the effective overrides changed.
        /// A failed read keeps the previous values.
        /// </summary>
        public void Reload()
        {
            lock (_gate)
            {
                var data = TryRead();
                if (data == null || SameContent(Data, data))
                {
                    return;
                }

                Data = data;
                OnReload();
            }
        }

        /// <inheritdoc/>
        public void Dispose() => _timer?.Dispose();

        /// <summary>
        /// Reads and decrypts rows into a case-insensitive dictionary.
        /// </summary>
        /// <returns>The dictionary, or null when the table is missing or the database is unreachable.</returns>
        private Dictionary<string, string?>? TryRead()
        {
            try
            {
                var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in _reader.ReadAll())
                {
                    if (row.IsSecret)
                    {
                        if (_unprotect == null)
                        {
                            continue;
                        }

                        try
                        {
                            data[row.Key] = _unprotect(row.Value);
                        }
                        catch (Exception)
                        {
                            // Undecryptable secret (e.g. lost key ring): fall back to the file value.
                        }
                    }
                    else
                    {
                        data[row.Key] = row.Value;
                    }
                }

                return data;
            }
            catch (Exception)
            {
                // Table not migrated yet or database unreachable: behave as if there are no overrides.
                return null;
            }
        }

        /// <summary>
        /// Compares two configuration dictionaries by content.
        /// </summary>
        private static bool SameContent(IDictionary<string, string?> a, IDictionary<string, string?> b)
        {
            return a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);
        }
    }

    /// <summary>
    /// Creates the Data Protection provider used for secret settings, shared by the pre-DI configuration
    /// provider and the settings service so both use the same keys.
    /// </summary>
    public static class SystemSettingsProtection
    {
        /// <summary>
        /// Data Protection purpose string for secret system settings.
        /// </summary>
        public const string Purpose = "FreeSpeak.SystemSettings.Secret";

        /// <summary>
        /// Creates a standalone provider equivalent to the application's default Data Protection configuration
        /// (same application discriminator and default key ring location).
        /// </summary>
        /// <param name="applicationDiscriminator">The application discriminator; ASP.NET Core uses the content root path.</param>
        /// <returns>A Data Protection provider.</returns>
        public static IDataProtectionProvider CreateStandaloneProvider(string applicationDiscriminator)
        {
            return DataProtectionProvider.Create(applicationDiscriminator);
        }
    }
}
