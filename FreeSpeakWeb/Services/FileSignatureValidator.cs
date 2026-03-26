using FreeSpeakWeb.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Validates file signatures (magic bytes) to ensure file content matches declared file type.
/// Prevents attacks where malicious files are disguised with incorrect extensions.
/// </summary>
public class FileSignatureValidator : IFileSignatureValidator
{
    private readonly ILogger<FileSignatureValidator> _logger;

    /// <summary>
    /// File signature definitions mapping magic bytes to MIME types.
    /// </summary>
    private static readonly List<FileSignature> _fileSignatures =
    [
        // JPEG - FFD8FF (with variations for different JPEG types)
        new FileSignature([0xFF, 0xD8, 0xFF, 0xE0], "image/jpeg", [".jpg", ".jpeg"]),
        new FileSignature([0xFF, 0xD8, 0xFF, 0xE1], "image/jpeg", [".jpg", ".jpeg"]),
        new FileSignature([0xFF, 0xD8, 0xFF, 0xE2], "image/jpeg", [".jpg", ".jpeg"]),
        new FileSignature([0xFF, 0xD8, 0xFF, 0xE3], "image/jpeg", [".jpg", ".jpeg"]),
        new FileSignature([0xFF, 0xD8, 0xFF, 0xE8], "image/jpeg", [".jpg", ".jpeg"]),
        new FileSignature([0xFF, 0xD8, 0xFF, 0xDB], "image/jpeg", [".jpg", ".jpeg"]),

        // PNG - 89504E47 0D0A1A0A
        new FileSignature([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png", [".png"]),

        // GIF - GIF87a or GIF89a
        new FileSignature([0x47, 0x49, 0x46, 0x38, 0x37, 0x61], "image/gif", [".gif"]),
        new FileSignature([0x47, 0x49, 0x46, 0x38, 0x39, 0x61], "image/gif", [".gif"]),

        // WebP - RIFF....WEBP
        new FileSignature([0x52, 0x49, 0x46, 0x46], "image/webp", [".webp"], 8, [0x57, 0x45, 0x42, 0x50]),

        // BMP - BM
        new FileSignature([0x42, 0x4D], "image/bmp", [".bmp"]),

        // TIFF - Little Endian (II) or Big Endian (MM)
        new FileSignature([0x49, 0x49, 0x2A, 0x00], "image/tiff", [".tiff", ".tif"]),
        new FileSignature([0x4D, 0x4D, 0x00, 0x2A], "image/tiff", [".tiff", ".tif"]),

        // ICO - Icon format
        new FileSignature([0x00, 0x00, 0x01, 0x00], "image/x-icon", [".ico"]),

        // HEIC/HEIF - ftyp followed by heic, heix, hevc, or mif1
        new FileSignature([0x00, 0x00, 0x00], "image/heic", [".heic", ".heif"], 4, [0x66, 0x74, 0x79, 0x70]),

        // AVIF - ftyp followed by avif
        new FileSignature([0x00, 0x00, 0x00], "image/avif", [".avif"], 4, [0x66, 0x74, 0x79, 0x70]),

        // MP4/M4V - ftyp box (various brands)
        new FileSignature([0x00, 0x00, 0x00], "video/mp4", [".mp4", ".m4v"], 4, [0x66, 0x74, 0x79, 0x70]),

        // MOV - ftyp qt or moov
        new FileSignature([0x00, 0x00, 0x00], "video/quicktime", [".mov"], 4, [0x66, 0x74, 0x79, 0x70]),
        new FileSignature([0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70, 0x71, 0x74], "video/quicktime", [".mov"]),

        // WebM - EBML header (1A 45 DF A3)
        new FileSignature([0x1A, 0x45, 0xDF, 0xA3], "video/webm", [".webm"]),

        // MKV - EBML header (same as WebM, differentiated by extension)
        new FileSignature([0x1A, 0x45, 0xDF, 0xA3], "video/x-matroska", [".mkv"]),

        // AVI - RIFF....AVI
        new FileSignature([0x52, 0x49, 0x46, 0x46], "video/x-msvideo", [".avi"], 8, [0x41, 0x56, 0x49, 0x20]),

        // WMV/ASF - Windows Media
        new FileSignature([0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11], "video/x-ms-wmv", [".wmv", ".asf"]),

        // FLV - Flash Video
        new FileSignature([0x46, 0x4C, 0x56, 0x01], "video/x-flv", [".flv"]),

        // 3GP/3G2 - 3GPP formats
        new FileSignature([0x00, 0x00, 0x00], "video/3gpp", [".3gp", ".3g2"], 4, [0x66, 0x74, 0x79, 0x70]),

        // MPEG - Video
        new FileSignature([0x00, 0x00, 0x01, 0xBA], "video/mpeg", [".mpg", ".mpeg"]),
        new FileSignature([0x00, 0x00, 0x01, 0xB3], "video/mpeg", [".mpg", ".mpeg"]),

        // PDF - %PDF
        new FileSignature([0x25, 0x50, 0x44, 0x46], "application/pdf", [".pdf"]),

        // Microsoft Office (modern OOXML formats) - PK header with specific content
        new FileSignature([0x50, 0x4B, 0x03, 0x04], "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [".docx", ".xlsx", ".pptx"]),

        // Microsoft Office (legacy formats)
        new FileSignature([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1], "application/msword", [".doc", ".xls", ".ppt"]),

        // ZIP Archive
        new FileSignature([0x50, 0x4B, 0x03, 0x04], "application/zip", [".zip"]),
        new FileSignature([0x50, 0x4B, 0x05, 0x06], "application/zip", [".zip"]),
        new FileSignature([0x50, 0x4B, 0x07, 0x08], "application/zip", [".zip"]),

        // RAR Archive
        new FileSignature([0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00], "application/x-rar-compressed", [".rar"]),
        new FileSignature([0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00], "application/x-rar-compressed", [".rar"]),

        // 7-Zip Archive
        new FileSignature([0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C], "application/x-7z-compressed", [".7z"]),

        // TAR Archive
        new FileSignature([0x75, 0x73, 0x74, 0x61, 0x72], "application/x-tar", [".tar"], 257),

        // GZIP
        new FileSignature([0x1F, 0x8B], "application/gzip", [".gz", ".tgz"]),

        // Plain text / CSV (no magic bytes, will be validated by extension only)
        // Note: Text files don't have magic bytes, so we'll handle them specially
    ];

    /// <summary>
    /// Allowed image extensions for posts and group files.
    /// </summary>
    private static readonly HashSet<string> _allowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif", ".ico", ".heic", ".heif", ".avif"
    };

    /// <summary>
    /// Allowed video extensions for posts and group files.
    /// </summary>
    private static readonly HashSet<string> _allowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".webm", ".mkv", ".avi", ".wmv", ".flv", ".3gp", ".3g2", ".mpg", ".mpeg"
    };

    /// <summary>
    /// Allowed document extensions for group files only.
    /// </summary>
    private static readonly HashSet<string> _allowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".rtf", ".odt", ".ods", ".odp"
    };

    /// <summary>
    /// Allowed archive extensions for group files only.
    /// </summary>
    private static readonly HashSet<string> _allowedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz"
    };

    /// <summary>
    /// Forbidden extensions that should never be allowed (executables and scripts).
    /// </summary>
    private static readonly HashSet<string> _forbiddenExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Executables
        ".exe", ".dll", ".com", ".bat", ".cmd", ".msi", ".scr", ".sys", ".bin",
        // Scripts
        ".ps1", ".psm1", ".psd1", ".ps1xml", ".psc1", ".pssc", ".sh", ".bash", ".zsh",
        ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
        // Application packages
        ".app", ".deb", ".rpm", ".dmg", ".pkg",
        // Other potentially dangerous
        ".jar", ".apk", ".gadget", ".application", ".pif", ".cpl", ".inf", ".reg"
    };

    /// <summary>
    /// Extension to MIME type mapping.
    /// </summary>
    private static readonly Dictionary<string, string> _extensionToMimeType = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".webp", "image/webp" },
        { ".bmp", "image/bmp" },
        { ".tiff", "image/tiff" },
        { ".tif", "image/tiff" },
        { ".ico", "image/x-icon" },
        { ".heic", "image/heic" },
        { ".heif", "image/heic" },
        { ".avif", "image/avif" },
        // Videos
        { ".mp4", "video/mp4" },
        { ".m4v", "video/mp4" },
        { ".mov", "video/quicktime" },
        { ".webm", "video/webm" },
        { ".mkv", "video/x-matroska" },
        { ".avi", "video/x-msvideo" },
        { ".wmv", "video/x-ms-wmv" },
        { ".asf", "video/x-ms-wmv" },
        { ".flv", "video/x-flv" },
        { ".3gp", "video/3gpp" },
        { ".3g2", "video/3gpp" },
        { ".mpg", "video/mpeg" },
        { ".mpeg", "video/mpeg" },
        // Documents
        { ".pdf", "application/pdf" },
        { ".doc", "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ppt", "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        { ".txt", "text/plain" },
        { ".csv", "text/csv" },
        { ".rtf", "application/rtf" },
        { ".odt", "application/vnd.oasis.opendocument.text" },
        { ".ods", "application/vnd.oasis.opendocument.spreadsheet" },
        { ".odp", "application/vnd.oasis.opendocument.presentation" },
        // Archives
        { ".zip", "application/zip" },
        { ".rar", "application/x-rar-compressed" },
        { ".7z", "application/x-7z-compressed" },
        { ".tar", "application/x-tar" },
        { ".gz", "application/gzip" },
        { ".tgz", "application/gzip" },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSignatureValidator"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging validation events.</param>
    public FileSignatureValidator(ILogger<FileSignatureValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public (bool IsValid, string? ErrorMessage) ValidateFileSignature(byte[] fileBytes, string fileName)
    {
        return ValidateFileSignature(fileBytes.AsSpan(), fileName);
    }

    /// <inheritdoc/>
    public (bool IsValid, string? ErrorMessage) ValidateFileSignature(ReadOnlySpan<byte> fileBytes, string fileName)
    {
        return ValidateFileSignatureInternal(fileBytes, fileName, forGroupFiles: false);
    }

    /// <summary>
    /// Validates that the file content matches the expected file type for group file uploads.
    /// Allows images, videos, documents, and archives but blocks executables and scripts.
    /// </summary>
    /// <param name="fileBytes">The raw bytes of the file to validate.</param>
    /// <param name="fileName">The declared filename including extension.</param>
    /// <returns>A tuple containing success status and error message if validation failed.</returns>
    public (bool IsValid, string? ErrorMessage) ValidateGroupFileSignature(byte[] fileBytes, string fileName)
    {
        return ValidateGroupFileSignature(fileBytes.AsSpan(), fileName);
    }

    /// <summary>
    /// Validates that the file content matches the expected file type for group file uploads.
    /// Allows images, videos, documents, and archives but blocks executables and scripts.
    /// </summary>
    /// <param name="fileBytes">The raw bytes of the file to validate.</param>
    /// <param name="fileName">The declared filename including extension.</param>
    /// <returns>A tuple containing success status and error message if validation failed.</returns>
    public (bool IsValid, string? ErrorMessage) ValidateGroupFileSignature(ReadOnlySpan<byte> fileBytes, string fileName)
    {
        return ValidateFileSignatureInternal(fileBytes, fileName, forGroupFiles: true);
    }

    /// <summary>
    /// Internal method to validate file signature with different rules for posts vs group files.
    /// </summary>
    private (bool IsValid, string? ErrorMessage) ValidateFileSignatureInternal(ReadOnlySpan<byte> fileBytes, string fileName, bool forGroupFiles)
    {
        if (fileBytes.Length == 0)
        {
            return (false, "File is empty.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            return (false, "File has no extension.");
        }

        // Check if extension is forbidden (executables/scripts)
        if (_forbiddenExtensions.Contains(extension))
        {
            _logger.LogWarning("File upload rejected: Extension {Extension} is forbidden for file {FileName}", 
                extension, fileName);
            return (false, $"File extension '{extension}' is not allowed for security reasons.");
        }

        // Check if extension is allowed based on context
        bool isAllowed = forGroupFiles 
            ? IsAllowedForGroupFiles(fileName) 
            : IsAllowedExtension(fileName);

        if (!isAllowed)
        {
            _logger.LogWarning("File upload rejected: Extension {Extension} is not allowed for file {FileName}", 
                extension, fileName);
            return (false, $"File extension '{extension}' is not allowed.");
        }

        // Get expected MIME type for the extension
        var expectedMimeType = GetMimeTypeForExtension(extension);
        if (expectedMimeType == null)
        {
            return (false, $"Unknown file extension '{extension}'.");
        }

        // For text files (txt, csv) that don't have magic bytes, skip signature validation
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) || 
            extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Text file validated by extension only: {FileName}", fileName);
            return (true, null);
        }

        // Detect actual MIME type from file content
        var detectedMimeType = DetectMimeType(fileBytes);
        if (detectedMimeType == null)
        {
            // For certain document/archive formats, if we can't detect but extension is allowed, allow it
            // This handles edge cases with complex formats
            if (forGroupFiles && (_allowedDocumentExtensions.Contains(extension) || _allowedArchiveExtensions.Contains(extension)))
            {
                _logger.LogDebug("Document/archive file validated by extension: {FileName}", fileName);
                return (true, null);
            }

            _logger.LogWarning("File upload rejected: Unable to detect file type from magic bytes for file {FileName}", 
                fileName);
            return (false, "Unable to verify file type. The file may be corrupted or in an unsupported format.");
        }

        // Compare expected vs detected (allowing for related types)
        if (!AreMimeTypesCompatible(expectedMimeType, detectedMimeType))
        {
            _logger.LogWarning(
                "File upload rejected: Extension {Extension} claims {ExpectedMime} but content is {DetectedMime} for file {FileName}",
                extension, expectedMimeType, detectedMimeType, fileName);
            return (false, $"File content does not match the declared file type. Expected {expectedMimeType} but detected {detectedMimeType}.");
        }

        _logger.LogDebug("File signature validated: {FileName} ({MimeType})", fileName, detectedMimeType);
        return (true, null);
    }

    /// <inheritdoc/>
    public string? DetectMimeType(byte[] fileBytes)
    {
        return DetectMimeType(fileBytes.AsSpan());
    }

    /// <inheritdoc/>
    public string? DetectMimeType(ReadOnlySpan<byte> fileBytes)
    {
        if (fileBytes.Length < 4)
        {
            return null;
        }

        foreach (var signature in _fileSignatures)
        {
            if (MatchesSignature(fileBytes, signature))
            {
                return signature.MimeType;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public bool IsAllowedExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        return _allowedImageExtensions.Contains(extension) || _allowedVideoExtensions.Contains(extension);
    }

    /// <summary>
    /// Checks if the file extension is allowed for group file uploads (includes documents and archives).
    /// </summary>
    /// <param name="fileName">The filename to check.</param>
    /// <returns>True if the extension is allowed, false otherwise.</returns>
    public bool IsAllowedForGroupFiles(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        // Check if forbidden first
        if (_forbiddenExtensions.Contains(extension))
        {
            return false;
        }

        // Allow images, videos, documents, and archives
        return _allowedImageExtensions.Contains(extension) || 
               _allowedVideoExtensions.Contains(extension) ||
               _allowedDocumentExtensions.Contains(extension) ||
               _allowedArchiveExtensions.Contains(extension);
    }

    /// <inheritdoc/>
    public string? GetMimeTypeForExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        // Ensure extension starts with dot
        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        return _extensionToMimeType.TryGetValue(extension, out var mimeType) ? mimeType : null;
    }

    /// <summary>
    /// Checks if the file bytes match a given signature.
    /// </summary>
    /// <param name="fileBytes">The file bytes to check.</param>
    /// <param name="signature">The signature to match against.</param>
    /// <returns>True if the signature matches, false otherwise.</returns>
    private static bool MatchesSignature(ReadOnlySpan<byte> fileBytes, FileSignature signature)
    {
        // Check primary magic bytes
        if (fileBytes.Length < signature.MagicBytes.Length)
        {
            return false;
        }

        for (int i = 0; i < signature.MagicBytes.Length; i++)
        {
            if (fileBytes[i] != signature.MagicBytes[i])
            {
                return false;
            }
        }

        // Check secondary magic bytes if present (for formats like RIFF/WebP, RIFF/AVI)
        if (signature.SecondaryMagicBytes != null && signature.SecondaryOffset > 0)
        {
            if (fileBytes.Length < signature.SecondaryOffset + signature.SecondaryMagicBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < signature.SecondaryMagicBytes.Length; i++)
            {
                if (fileBytes[signature.SecondaryOffset + i] != signature.SecondaryMagicBytes[i])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Determines if two MIME types are compatible (exact match or related types).
    /// </summary>
    /// <param name="expected">The expected MIME type based on extension.</param>
    /// <param name="detected">The detected MIME type from magic bytes.</param>
    /// <returns>True if the types are compatible, false otherwise.</returns>
    private static bool AreMimeTypesCompatible(string expected, string detected)
    {
        // Exact match
        if (string.Equals(expected, detected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // ISOBMFF container formats (MP4, MOV, HEIC, AVIF, 3GP) all share ftyp boxes
        // These formats can be detected as each other due to similar container structure
        var isobmffTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4", "video/quicktime", "video/3gpp",
            "image/heic", "image/heif", "image/avif"
        };

        if (isobmffTypes.Contains(expected) && isobmffTypes.Contains(detected))
        {
            return true;
        }

        // Allow related video types (ftyp-based formats are hard to distinguish precisely)
        if (expected.StartsWith("video/") && detected.StartsWith("video/"))
        {
            // WebM and MKV share the same EBML header
            if ((expected == "video/webm" || expected == "video/x-matroska") &&
                (detected == "video/webm" || detected == "video/x-matroska"))
            {
                return true;
            }
        }

        // Office formats (OOXML uses ZIP container - PK header)
        // Allow ZIP detection for Office documents
        if ((expected.Contains("openxmlformats") || expected == "application/vnd.ms-excel" || 
             expected == "application/vnd.ms-powerpoint" || expected == "application/msword") &&
            detected == "application/zip")
        {
            return true;
        }

        // Legacy Office formats share the same OLE compound file header
        var legacyOfficeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/msword", "application/vnd.ms-excel", "application/vnd.ms-powerpoint"
        };

        if (legacyOfficeTypes.Contains(expected) && legacyOfficeTypes.Contains(detected))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Represents a file signature (magic bytes) for a specific file type.
    /// </summary>
    private sealed class FileSignature
    {
        /// <summary>
        /// Gets the magic bytes at the start of the file.
        /// </summary>
        public byte[] MagicBytes { get; }

        /// <summary>
        /// Gets the MIME type for this file signature.
        /// </summary>
        public string MimeType { get; }

        /// <summary>
        /// Gets the allowed file extensions for this signature.
        /// </summary>
        public string[] Extensions { get; }

        /// <summary>
        /// Gets the offset for secondary magic bytes (for container formats).
        /// </summary>
        public int SecondaryOffset { get; }

        /// <summary>
        /// Gets the secondary magic bytes (for container formats like RIFF).
        /// </summary>
        public byte[]? SecondaryMagicBytes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSignature"/> class.
        /// </summary>
        /// <param name="magicBytes">The magic bytes at the start of the file.</param>
        /// <param name="mimeType">The MIME type for this signature.</param>
        /// <param name="extensions">The allowed file extensions.</param>
        /// <param name="secondaryOffset">The offset for secondary magic bytes.</param>
        /// <param name="secondaryMagicBytes">The secondary magic bytes.</param>
        public FileSignature(byte[] magicBytes, string mimeType, string[] extensions, 
            int secondaryOffset = 0, byte[]? secondaryMagicBytes = null)
        {
            MagicBytes = magicBytes;
            MimeType = mimeType;
            Extensions = extensions;
            SecondaryOffset = secondaryOffset;
            SecondaryMagicBytes = secondaryMagicBytes;
        }
    }
}
