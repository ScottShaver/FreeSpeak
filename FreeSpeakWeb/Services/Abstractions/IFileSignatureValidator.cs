namespace FreeSpeakWeb.Services.Abstractions;

/// <summary>
/// Interface for validating file signatures (magic bytes) to ensure file content matches declared file type.
/// Prevents attacks where malicious files are disguised with incorrect extensions.
/// </summary>
public interface IFileSignatureValidator
{
    /// <summary>
    /// Validates that the file content matches the expected file type based on magic bytes.
    /// </summary>
    /// <param name="fileBytes">The raw bytes of the file to validate.</param>
    /// <param name="fileName">The declared filename including extension.</param>
    /// <returns>A tuple containing success status and error message if validation failed.</returns>
    (bool IsValid, string? ErrorMessage) ValidateFileSignature(byte[] fileBytes, string fileName);

    /// <summary>
    /// Validates that the file content matches the expected file type based on magic bytes.
    /// </summary>
    /// <param name="fileBytes">The raw bytes of the file to validate.</param>
    /// <param name="fileName">The declared filename including extension.</param>
    /// <returns>A tuple containing success status and error message if validation failed.</returns>
    (bool IsValid, string? ErrorMessage) ValidateFileSignature(ReadOnlySpan<byte> fileBytes, string fileName);

    /// <summary>
    /// Validates that the file content matches the expected file type for group file uploads.
    /// Allows images, videos, documents, and archives but blocks executables and scripts.
    /// </summary>
    /// <param name="fileBytes">The raw bytes of the file to validate.</param>
    /// <param name="fileName">The declared filename including extension.</param>
    /// <returns>A tuple containing success status and error message if validation failed.</returns>
    (bool IsValid, string? ErrorMessage) ValidateGroupFileSignature(byte[] fileBytes, string fileName);

    /// <summary>
    /// Validates that the file content matches the expected file type for group file uploads.
    /// Allows images, videos, documents, and archives but blocks executables and scripts.
    /// </summary>
    /// <param name="fileBytes">The raw bytes of the file to validate.</param>
    /// <param name="fileName">The declared filename including extension.</param>
    /// <returns>A tuple containing success status and error message if validation failed.</returns>
    (bool IsValid, string? ErrorMessage) ValidateGroupFileSignature(ReadOnlySpan<byte> fileBytes, string fileName);

    /// <summary>
    /// Detects the actual file type based on magic bytes.
    /// </summary>
    /// <param name="fileBytes">The raw bytes of the file to analyze.</param>
    /// <returns>The detected MIME type, or null if the file type is unknown.</returns>
    string? DetectMimeType(byte[] fileBytes);

    /// <summary>
    /// Detects the actual file type based on magic bytes.
    /// </summary>
    /// <param name="fileBytes">The raw bytes of the file to analyze.</param>
    /// <returns>The detected MIME type, or null if the file type is unknown.</returns>
    string? DetectMimeType(ReadOnlySpan<byte> fileBytes);

    /// <summary>
    /// Checks if the file extension is in the allowed list for uploads.
    /// </summary>
    /// <param name="fileName">The filename to check.</param>
    /// <returns>True if the extension is allowed, false otherwise.</returns>
    bool IsAllowedExtension(string fileName);

    /// <summary>
    /// Gets the expected MIME type for a given file extension.
    /// </summary>
    /// <param name="extension">The file extension (with or without leading dot).</param>
    /// <returns>The expected MIME type, or null if the extension is not recognized.</returns>
    string? GetMimeTypeForExtension(string extension);
}
