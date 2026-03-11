using System.Timers;
using Timer = System.Timers.Timer;

namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Service to manage notification badge counts with automatic polling
    /// </summary>
    public class NotificationBadgeService : IDisposable
    {
        private readonly NotificationService _notificationService;
        private readonly ILogger<NotificationBadgeService> _logger;
        private Timer? _pollingTimer;
        private string? _currentUserId;
        private int _unreadCount;
        private bool _isInitialized;
        
        // Event to notify subscribers when the unread count changes
        public event Action<int>? OnUnreadCountChanged;

        public int UnreadCount => _unreadCount;

        public NotificationBadgeService(
            NotificationService notificationService,
            ILogger<NotificationBadgeService> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Initialize the service for a specific user and start polling
        /// </summary>
        public async Task InitializeAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Cannot initialize NotificationBadgeService with empty userId");
                return;
            }

            // If already initialized for this user, just ensure timer is running
            if (_isInitialized && _currentUserId == userId)
            {
                EnsureTimerRunning();
                return;
            }

            // Stop existing timer if any
            StopPolling();

            _currentUserId = userId;
            _isInitialized = true;

            // Load initial count
            await RefreshUnreadCountAsync();

            // Start polling timer (5 minutes = 300,000 milliseconds)
            StartPolling();

            _logger.LogInformation("NotificationBadgeService initialized for user {UserId}", userId);
        }

        /// <summary>
        /// Refresh the unread count immediately
        /// </summary>
        public async Task RefreshUnreadCountAsync()
        {
            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                return;
            }

            try
            {
                var count = await _notificationService.GetUnreadCountAsync(_currentUserId);
                
                if (count != _unreadCount)
                {
                    _unreadCount = count;
                    OnUnreadCountChanged?.Invoke(_unreadCount);
                    _logger.LogDebug("Unread count updated to {Count} for user {UserId}", count, _currentUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing unread count for user {UserId}", _currentUserId);
            }
        }

        /// <summary>
        /// Reset the polling timer (e.g., when user visits notifications page)
        /// </summary>
        public async Task ResetTimerAsync()
        {
            _logger.LogDebug("Resetting notification polling timer for user {UserId}", _currentUserId);
            
            // Refresh count immediately
            await RefreshUnreadCountAsync();
            
            // Restart the timer
            StopPolling();
            StartPolling();
        }

        /// <summary>
        /// Start the polling timer
        /// </summary>
        private void StartPolling()
        {
            if (_pollingTimer != null)
            {
                return; // Timer already running
            }

            // Create timer with 5-minute interval
            _pollingTimer = new Timer(300000); // 5 minutes in milliseconds
            _pollingTimer.Elapsed += async (sender, e) => await OnTimerElapsed();
            _pollingTimer.AutoReset = true;
            _pollingTimer.Start();

            _logger.LogDebug("Notification polling timer started for user {UserId}", _currentUserId);
        }

        /// <summary>
        /// Stop the polling timer
        /// </summary>
        private void StopPolling()
        {
            if (_pollingTimer != null)
            {
                _pollingTimer.Stop();
                _pollingTimer.Dispose();
                _pollingTimer = null;
                _logger.LogDebug("Notification polling timer stopped");
            }
        }

        /// <summary>
        /// Ensure the timer is running
        /// </summary>
        private void EnsureTimerRunning()
        {
            if (_pollingTimer == null || !_pollingTimer.Enabled)
            {
                StartPolling();
            }
        }

        /// <summary>
        /// Handle timer elapsed event
        /// </summary>
        private async Task OnTimerElapsed()
        {
            await RefreshUnreadCountAsync();
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            StopPolling();
            OnUnreadCountChanged = null;
            _isInitialized = false;
            _currentUserId = null;
            _logger.LogDebug("NotificationBadgeService disposed");
        }
    }
}
