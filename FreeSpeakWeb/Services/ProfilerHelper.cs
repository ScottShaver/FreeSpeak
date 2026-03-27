using Microsoft.Extensions.Options;
using StackExchange.Profiling;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Helper class that wraps MiniProfiler API calls with configuration checks.
    /// If profiling is disabled in settings, all calls become no-ops for minimal overhead.
    /// </summary>
    public class ProfilerHelper
    {
        private readonly bool _profilingEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfilerHelper"/> class.
        /// </summary>
        /// <param name="profilingSettings">Configuration settings for profiling behavior.</param>
        public ProfilerHelper(IOptions<ProfilingSettings> profilingSettings)
        {
            _profilingEnabled = profilingSettings.Value.Enabled;
        }

        /// <summary>
        /// Starts a profiling step with the specified name.
        /// Returns a disposable timing object that should be used with 'using' statement.
        /// </summary>
        /// <param name="name">The name of the profiling step.</param>
        /// <returns>A disposable timing object, or null if profiling is disabled.</returns>
        public IDisposable? Step(string name)
        {
            if (!_profilingEnabled)
                return null;

            return MiniProfiler.Current?.Step(name);
        }

        /// <summary>
        /// Starts a custom timing with the specified category and command.
        /// Useful for profiling database queries or external service calls.
        /// </summary>
        /// <param name="category">The category of the timing (e.g., "sql", "redis").</param>
        /// <param name="command">The command being executed.</param>
        /// <param name="executeType">Optional execution type (e.g., "SELECT", "INSERT").</param>
        /// <returns>A disposable custom timing object, or null if profiling is disabled.</returns>
        public CustomTiming? CustomTiming(string category, string command, string? executeType = null)
        {
            if (!_profilingEnabled)
                return null;

            return MiniProfiler.Current?.CustomTiming(category, command, executeType);
        }

        /// <summary>
        /// Adds a custom data key-value pair to the current profiler.
        /// Useful for adding contextual information to profiling sessions.
        /// </summary>
        /// <param name="key">The key for the custom data.</param>
        /// <param name="value">The value for the custom data.</param>
        public void AddCustomData(string key, string value)
        {
            if (!_profilingEnabled)
                return;

            MiniProfiler.Current?.AddCustomLink(key, value);
        }

        /// <summary>
        /// Gets a value indicating whether profiling is currently enabled.
        /// </summary>
        public bool IsEnabled => _profilingEnabled;
    }
}
