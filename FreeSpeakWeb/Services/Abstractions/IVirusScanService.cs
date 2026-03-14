namespace FreeSpeakWeb.Services.Abstractions;

/// <summary>
/// Interface for virus/malware scanning of uploaded files.
/// Provides abstraction over antivirus scanning implementations (e.g., ClamAV).
/// </summary>
public interface IVirusScanService
{
    /// <summary>
    /// Scans file bytes for viruses and malware.
    /// </summary>
    /// <param name="fileBytes">The raw bytes of the file to scan.</param>
    /// <param name="fileName">The name of the file being scanned (for logging purposes).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A scan result indicating whether the file is clean, infected, or if an error occurred.</returns>
    Task<VirusScanResult> ScanAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans a file stream for viruses and malware.
    /// </summary>
    /// <param name="fileStream">The stream containing the file data to scan.</param>
    /// <param name="fileName">The name of the file being scanned (for logging purposes).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A scan result indicating whether the file is clean, infected, or if an error occurred.</returns>
    Task<VirusScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the virus scanning service is available and responsive.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the service is available, false otherwise.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a virus scan operation.
/// </summary>
public class VirusScanResult
{
    /// <summary>
    /// Gets whether the file is clean (no threats detected).
    /// </summary>
    public bool IsClean { get; init; }

    /// <summary>
    /// Gets whether virus scanning is enabled and was performed.
    /// </summary>
    public bool ScanPerformed { get; init; }

    /// <summary>
    /// Gets the name of the detected virus/malware, if any.
    /// </summary>
    public string? VirusName { get; init; }

    /// <summary>
    /// Gets the error message if the scan failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets whether the scan completed successfully (regardless of result).
    /// </summary>
    public bool ScanSucceeded { get; init; }

    /// <summary>
    /// Gets the raw result message from the scanner.
    /// </summary>
    public string? RawResult { get; init; }

    /// <summary>
    /// Creates a result indicating a clean file.
    /// </summary>
    /// <param name="rawResult">The raw result from the scanner.</param>
    /// <returns>A clean scan result.</returns>
    public static VirusScanResult Clean(string? rawResult = null) => new()
    {
        IsClean = true,
        ScanPerformed = true,
        ScanSucceeded = true,
        RawResult = rawResult
    };

    /// <summary>
    /// Creates a result indicating an infected file.
    /// </summary>
    /// <param name="virusName">The name of the detected virus.</param>
    /// <param name="rawResult">The raw result from the scanner.</param>
    /// <returns>An infected scan result.</returns>
    public static VirusScanResult Infected(string virusName, string? rawResult = null) => new()
    {
        IsClean = false,
        ScanPerformed = true,
        ScanSucceeded = true,
        VirusName = virusName,
        RawResult = rawResult
    };

    /// <summary>
    /// Creates a result indicating the scan failed with an error.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed scan result.</returns>
    public static VirusScanResult Error(string errorMessage) => new()
    {
        IsClean = false,
        ScanPerformed = true,
        ScanSucceeded = false,
        ErrorMessage = errorMessage
    };

    /// <summary>
    /// Creates a result indicating scanning was skipped (disabled).
    /// </summary>
    /// <returns>A skipped scan result.</returns>
    public static VirusScanResult Skipped() => new()
    {
        IsClean = true,
        ScanPerformed = false,
        ScanSucceeded = true
    };
}
