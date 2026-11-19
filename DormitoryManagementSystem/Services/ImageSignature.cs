namespace DormitoryManagementSystem.Services;

internal static class ImageSignature
{
    internal static (string Extension, string ContentType)? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return (".jpg", "image/jpeg");
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return (".png", "image/png");
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");
        return null;
    }
}
