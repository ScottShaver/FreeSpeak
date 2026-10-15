using Microsoft.Extensions.Options;

namespace FreeSpeakWeb.Tests.Helpers
{
    /// <summary>
    /// Minimal <see cref="IOptionsMonitor{TOptions}"/> for tests whose current value can be replaced.
    /// </summary>
    /// <typeparam name="T">The options type.</typeparam>
    public sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly List<Action<T, string?>> _listeners = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TestOptionsMonitor{T}"/> class.
        /// </summary>
        /// <param name="value">The initial value.</param>
        public TestOptionsMonitor(T value)
        {
            CurrentValue = value;
        }

        /// <inheritdoc/>
        public T CurrentValue { get; private set; }

        /// <inheritdoc/>
        public T Get(string? name) => CurrentValue;

        /// <summary>
        /// Replaces the current value and notifies listeners.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void Set(T value)
        {
            CurrentValue = value;
            foreach (var listener in _listeners)
            {
                listener(value, null);
            }
        }

        /// <inheritdoc/>
        public IDisposable? OnChange(Action<T, string?> listener)
        {
            _listeners.Add(listener);
            return null;
        }
    }
}
