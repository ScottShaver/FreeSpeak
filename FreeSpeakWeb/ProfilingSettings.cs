namespace FreeSpeakWeb
{
    /// <summary>
    /// Configuration settings for MiniProfiler performance profiling.
    /// Controls whether profiling is enabled and configures profiling behavior.
    /// </summary>
    public class ProfilingSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether MiniProfiler profiling is enabled.
        /// When false, profiling calls are no-ops to minimize performance overhead.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to show profiling controls in the UI.
        /// Only applicable when Enabled is true.
        /// </summary>
        public bool ShowControls { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of profiling results to store.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Gets or sets a value indicating whether to profile database queries.
        /// Requires MiniProfiler.EntityFrameworkCore package.
        /// </summary>
        public bool ProfileDatabase { get; set; } = true;
    }
}
