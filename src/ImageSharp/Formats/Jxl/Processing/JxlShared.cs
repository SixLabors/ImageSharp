// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Shared JPEG XL constants
/// </summary>
internal static class JxlShared
{
    /// <summary>
    /// Maximum number of passes in an image.
    /// </summary>
    public const int MaximumNumberOfPasses = 11;

    /// <summary>
    /// Maximum number of reference frames.
    /// </summary>
    public const int MaximumNumberOfReferenceFrames = 4;

    /// <summary>
    /// Gets the 12-byte signature (a.k.a. magic) for JPEG XL files.
    /// </summary>
    public static ReadOnlySpan<byte> SignatureBox =>
    [
        0x00, 0x00, 0x00, 0x0C,
        (byte)'J', (byte)'X', (byte)'L', (byte)' ',
        0x0D, 0x0A, 0x87, 0x0A
    ];
}
