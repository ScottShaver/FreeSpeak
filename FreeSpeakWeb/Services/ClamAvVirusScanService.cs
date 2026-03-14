using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nClam;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Virus scanning service implementation using ClamAV via the nClam library.
/// Scans uploaded files for viruses and malware before they are stored.
/// Requires a running ClamAV daemon (clamd) accessible via TCP.
/// </summary>
public class ClamAvVirusScanService : IVirusScanService
{
    private readonly ILogger<ClamAvVirusScanService> _logger;
    private readonly VirusScanSettings _settings;
    private readonly ClamClient? _clamClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClamAvVirusScanService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for recording scan events.</param>
    /// <param name="settings">The virus scan configuration settings.</param>
    public ClamAvVirusScanService(
        ILogger<ClamAvVirusScanService> logger,
        IOptions<VirusScanSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;

        if (_settings.Enabled && !string.IsNullOrEmpty(_settings.ClamAvHost))
        {
            _clamClient = new ClamClient(_settings.ClamAvHost, _settings.ClamAvPort)
            {
                MaxStreamSize = _settings.MaxStreamSizeBytes
            };
            _logger.LogInformation(
                "ClamAV virus scanning enabled. Connecting to {Host}:{Port}",
                _settings.ClamAvHost, _settings.ClamAvPort);
        }
        else
        {
            _logger.LogWarning("Virus scanning is disabled. Files will not be scanned for malware.");
        }
    }

    /// <inheritdoc/>
    public async Task<VirusScanResult> ScanAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || _clamClient == null)
        {
            _logger.LogDebug("Virus scanning skipped for {FileName} - scanning is disabled", fileName);
            return VirusScanResult.Skipped();
        }

        try
        {
            _logger.LogDebug("Scanning file {FileName} ({Size} bytes) for viruses", fileName, fileBytes.Length);

            var scanResult = await _clamClient.SendAndScanFileAsync(fileBytes, cancellationToken);

            return ProcessScanResult(scanResult, fileName);
        }
        catch (Exception ex)
        {
            return HandleScanException(ex, fileName);
        }
    }

    /// <inheritdoc/>
    public async Task<VirusScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || _clamClient == null)
        {
            _logger.LogDebug("Virus scanning skipped for {FileName} - scanning is disabled", fileName);
            return VirusScanResult.Skipped();
        }

        try
        {
            _logger.LogDebug("Scanning file stream {FileName} for viruses", fileName);

            var scanResult = await _clamClient.SendAndScanFileAsync(fileStream, cancellationToken);

            return ProcessScanResult(scanResult, fileName);
        }
        catch (Exception ex)
        {
            return HandleScanException(ex, fileName);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || _clamClient == null)
        {
            return false;
        }

        try
        {
            var pingResult = await _clamClient.PingAsync(cancellationToken);
            return pingResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ClamAV availability check failed");
            return false;
        }
    }

    /// <summary>
    /// Processes the raw scan result from ClamAV and converts it to a VirusScanResult.
    /// </summary>
    /// <param name="scanResult">The raw result from ClamAV.</param>
    /// <param name="fileName">The name of the scanned file.</param>
    /// <returns>A processed virus scan result.</returns>
    private VirusScanResult ProcessScanResult(ClamScanResult scanResult, string fileName)
    {
        var rawResult = scanResult.RawResult;

        switch (scanResult.Result)
        {
            case ClamScanResults.Clean:
                _logger.LogDebug("File {FileName} is clean", fileName);
                return VirusScanResult.Clean(rawResult);

            case ClamScanResults.VirusDetected:
                var virusName = scanResult.InfectedFiles?.FirstOrDefault()?.VirusName ?? "Unknown";
                _logger.LogWarning(
                    "VIRUS DETECTED in file {FileName}: {VirusName}",
                    fileName, virusName);
                return VirusScanResult.Infected(virusName, rawResult);

            case ClamScanResults.Error:
                _logger.LogError(
                    "Virus scan error for file {FileName}: {RawResult}",
                    fileName, rawResult);

                // If configured to fail open, treat errors as clean
                if (_settings.FailOpen)
                {
                    _logger.LogWarning(
                        "FailOpen is enabled - allowing file {FileName} despite scan error",
                        fileName);
                    return VirusScanResult.Clean(rawResult);
                }

                return VirusScanResult.Error($"Scan error: {rawResult}");

            default:
                _logger.LogWarning(
                    "Unknown scan result for file {FileName}: {Result}",
                    fileName, scanResult.Result);

                if (_settings.FailOpen)
                {
                    return VirusScanResult.Clean(rawResult);
                }

                return VirusScanResult.Error($"Unknown result: {scanResult.Result}");
        }
    }

    /// <summary>
    /// Handles exceptions that occur during virus scanning.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="fileName">The name of the file being scanned.</param>
    /// <returns>A virus scan result representing the error state.</returns>
    private VirusScanResult HandleScanException(Exception ex, string fileName)
    {
        _logger.LogError(ex, "Exception during virus scan for file {FileName}", fileName);

        // If configured to fail open, treat connection errors as clean
        if (_settings.FailOpen)
        {
            _logger.LogWarning(
                "FailOpen is enabled - allowing file {FileName} despite scan exception: {Message}",
                fileName, ex.Message);
            return VirusScanResult.Clean($"Exception: {ex.Message}");
        }

        return VirusScanResult.Error($"Scan failed: {ex.Message}");
    }
}

/// <summary>
/// Configuration settings for virus scanning.
/// </summary>
public class VirusScanSettings
{
    /// <summary>
    /// Gets or sets whether virus scanning is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the ClamAV daemon hostname or IP address.
    /// </summary>
    public string ClamAvHost { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the ClamAV daemon port (default: 3310).
    /// </summary>
    public int ClamAvPort { get; set; } = 3310;

    /// <summary>
    /// Gets or sets the maximum stream size in bytes that can be sent to ClamAV.
    /// Default is 100MB to match the application's max video size.
    /// </summary>
    public long MaxStreamSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets whether to allow files through when ClamAV is unavailable or returns an error.
    /// When true (fail open), files are allowed if scanning fails.
    /// When false (fail closed), files are rejected if scanning fails.
    /// Default is false for security.
    /// </summary>
    public bool FailOpen { get; set; } = false;

    /// <summary>
    /// Gets or sets the timeout in seconds for scan operations.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
