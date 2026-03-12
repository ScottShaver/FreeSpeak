namespace FreeSpeakWeb.Services
{
    public enum AlertType
    {
        Success,
        Info,
        Warning,
        Error
    }

    public class AlertMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Message { get; set; } = string.Empty;
        public AlertType Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AlertService
    {
        private readonly List<AlertMessage> _alerts = new();
        private readonly object _lock = new();

        public event Action? OnChange;

        /// <summary>
        /// Get all current alerts
        /// </summary>
        public List<AlertMessage> GetAlerts()
        {
            lock (_lock)
            {
                return _alerts.ToList();
            }
        }

        /// <summary>
        /// Show a success alert
        /// </summary>
        public void ShowSuccess(string message)
        {
            ShowAlert(message, AlertType.Success);
        }

        /// <summary>
        /// Show an info alert
        /// </summary>
        public void ShowInfo(string message)
        {
            ShowAlert(message, AlertType.Info);
        }

        /// <summary>
        /// Show a warning alert
        /// </summary>
        public void ShowWarning(string message)
        {
            ShowAlert(message, AlertType.Warning);
        }

        /// <summary>
        /// Show an error alert
        /// </summary>
        public void ShowError(string message)
        {
            ShowAlert(message, AlertType.Error);
        }

        /// <summary>
        /// Show an alert with a specific type
        /// </summary>
        public void ShowAlert(string message, AlertType type)
        {
            var alert = new AlertMessage
            {
                Message = message,
                Type = type,
                CreatedAt = DateTime.UtcNow
            };

            lock (_lock)
            {
                _alerts.Add(alert);
            }

            OnChange?.Invoke();

            // Auto-remove after 4 seconds
            Task.Run(async () =>
            {
                await Task.Delay(4000);
                RemoveAlert(alert.Id);
            });
        }

        /// <summary>
        /// Remove a specific alert
        /// </summary>
        public void RemoveAlert(string alertId)
        {
            lock (_lock)
            {
                var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
                if (alert != null)
                {
                    _alerts.Remove(alert);
                    OnChange?.Invoke();
                }
            }
        }

        /// <summary>
        /// Clear all alerts
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                _alerts.Clear();
            }

            OnChange?.Invoke();
        }
    }
}
