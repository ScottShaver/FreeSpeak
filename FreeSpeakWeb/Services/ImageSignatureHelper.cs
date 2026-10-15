namespace FreeSpeakWeb.Services
{
    /// <summary>
    /// Lightweight magic-byte sniffing for the image formats accepted by post uploads.
    /// Used for early client-side feedback; server-side validation remains authoritative.
    /// </summary>
    public static class ImageSignatureHelper
    {
        /// <summary>
        /// Determines whether the leading bytes of a file match a supported image format (JPEG, PNG, GIF or WebP).
        /// </summary>
        /// <param name="header">The first bytes of the file (at least 12 for WebP detection).</param>
        /// <returns>True if the bytes look like a supported image; otherwise false.</returns>
        public static bool IsSupportedImage(ReadOnlySpan<byte> header)
        {
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                return true;
            }

            if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            {
                return true;
            }

            if (header.Length >= 6 && (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8)))
            {
                return true;
            }

            return header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8);
        }
    }
}
