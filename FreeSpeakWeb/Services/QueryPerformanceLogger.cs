using System.Diagnostics;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Monitors and logs database query performance to identify slow queries and performance bottlenecks.
    /// Provides detailed timing information and automatic warnings for queries exceeding thresholds.
    /// </summary>
    public class QueryPerformanceLogger
    {
        private readonly ILogger<QueryPerformanceLogger> _logger;
        private const int SlowQueryThresholdMs = 1000; // Warn if query takes > 1 second
        private const int VerySlowQueryThresholdMs = 3000; // Error if query takes > 3 seconds

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryPerformanceLogger"/> class.
        /// </summary>
        /// <param name="logger">Logger for recording query performance metrics.</param>
        public QueryPerformanceLogger(ILogger<QueryPerformanceLogger> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Measures the execution time of a query and logs performance metrics.
        /// Automatically logs warnings for slow queries and errors for very slow queries.
        /// </summary>
        /// <typeparam name="T">The return type of the query.</typeparam>
        /// <param name="query">The async query function to execute and measure.</param>
        /// <param name="queryName">A descriptive name for the query (e.g., "GetFeedPosts").</param>
        /// <param name="parameters">Optional dictionary of query parameters for logging context.</param>
        /// <returns>The result of the query execution.</returns>
        public async Task<T> MeasureQueryAsync<T>(
            Func<Task<T>> query,
            string queryName,
            Dictionary<string, object>? parameters = null)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await query();
                stopwatch.Stop();

                LogQueryPerformance(queryName, stopwatch.ElapsedMilliseconds, parameters, null);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogQueryPerformance(queryName, stopwatch.ElapsedMilliseconds, parameters, ex);
                throw;
            }
        }

        /// <summary>
        /// Measures the execution time of a query without a return value.
        /// </summary>
        /// <param name="query">The async query action to execute and measure.</param>
        /// <param name="queryName">A descriptive name for the query.</param>
        /// <param name="parameters">Optional dictionary of query parameters for logging context.</param>
        public async Task MeasureQueryAsync(
            Func<Task> query,
            string queryName,
            Dictionary<string, object>? parameters = null)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await query();
                stopwatch.Stop();

                LogQueryPerformance(queryName, stopwatch.ElapsedMilliseconds, parameters, null);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogQueryPerformance(queryName, stopwatch.ElapsedMilliseconds, parameters, ex);
                throw;
            }
        }

        /// <summary>
        /// Logs query performance with appropriate log level based on execution time.
        /// </summary>
        private void LogQueryPerformance(
            string queryName,
            long elapsedMilliseconds,
            Dictionary<string, object>? parameters,
            Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogError(exception,
                    "Query failed: {QueryName} after {ElapsedMs}ms. Parameters: {@Parameters}",
                    queryName,
                    elapsedMilliseconds,
                    parameters);
            }
            else if (elapsedMilliseconds > VerySlowQueryThresholdMs)
            {
                _logger.LogError(
                    "Very slow query detected: {QueryName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms). Parameters: {@Parameters}",
                    queryName,
                    elapsedMilliseconds,
                    VerySlowQueryThresholdMs,
                    parameters);
            }
            else if (elapsedMilliseconds > SlowQueryThresholdMs)
            {
                _logger.LogWarning(
                    "Slow query detected: {QueryName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms). Parameters: {@Parameters}",
                    queryName,
                    elapsedMilliseconds,
                    SlowQueryThresholdMs,
                    parameters);
            }
            else
            {
                _logger.LogDebug(
                    "Query completed: {QueryName} in {ElapsedMs}ms. Parameters: {@Parameters}",
                    queryName,
                    elapsedMilliseconds,
                    parameters);
            }
        }

        /// <summary>
        /// Creates a scoped performance measurement context for tracking multiple related queries.
        /// Useful for measuring complex operations involving multiple database calls.
        /// </summary>
        /// <param name="operationName">The name of the overall operation being measured.</param>
        /// <returns>A disposable scope that logs the total operation time when disposed.</returns>
        public QueryPerformanceScope CreateScope(string operationName)
        {
            return new QueryPerformanceScope(_logger, operationName);
        }
    }

    /// <summary>
    /// Represents a performance measurement scope for tracking the duration of complex operations.
    /// Automatically logs the total operation time when disposed.
    /// </summary>
    public class QueryPerformanceScope : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryPerformanceScope"/> class.
        /// </summary>
        /// <param name="logger">Logger for recording operation performance.</param>
        /// <param name="operationName">The name of the operation being measured.</param>
        internal QueryPerformanceScope(ILogger logger, string operationName)
        {
            _logger = logger;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();

            _logger.LogDebug("Started operation: {OperationName}", operationName);
        }

        /// <summary>
        /// Logs an intermediate checkpoint within the operation.
        /// </summary>
        /// <param name="checkpointName">The name of the checkpoint.</param>
        public void LogCheckpoint(string checkpointName)
        {
            _logger.LogDebug(
                "Operation {OperationName} checkpoint {CheckpointName} at {ElapsedMs}ms",
                _operationName,
                checkpointName,
                _stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Disposes the scope and logs the total operation duration.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _stopwatch.Stop();
            _logger.LogInformation(
                "Completed operation: {OperationName} in {ElapsedMs}ms",
                _operationName,
                _stopwatch.ElapsedMilliseconds);

            _disposed = true;
        }
    }
}
