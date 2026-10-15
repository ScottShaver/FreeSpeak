using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Configuration
{
    /// <summary>
    /// <see cref="IOptions{TOptions}"/> adapter that returns the current value from <see cref="IOptionsMonitor{TOptions}"/>,
    /// so existing consumers see reloaded settings without being changed individually.
    /// </summary>
    /// <typeparam name="T">The options type.</typeparam>
    public sealed class LiveOptions<T> : IOptions<T> where T : class
    {
        private readonly IOptionsMonitor<T> _monitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="LiveOptions{T}"/> class.
        /// </summary>
        /// <param name="monitor">The options monitor supplying current values.</param>
        public LiveOptions(IOptionsMonitor<T> monitor)
        {
            _monitor = monitor;
        }

        /// <inheritdoc/>
        public T Value => _monitor.CurrentValue;
    }
}
