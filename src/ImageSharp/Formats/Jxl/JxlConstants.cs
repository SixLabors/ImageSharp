// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Constants for the JPEG XL (JXL) format.
/// </summary>
internal static class JxlConstants
{
    /// <summary>Gets the JPEG XL codestream signature (FF 0A).</summary>
    public static ReadOnlySpan<byte> CodestreamMagic => new byte[] { 0xFF, 0x0A };

    /// <summary>Gets the JPEG XL container signature.</summary>
    public static ReadOnlySpan<byte> ContainerMagic => new byte[]
        { 0x00, 0x00, 0x00, 0x0C, 0x4A, 0x58, 0x4C, 0x20, 0x0D, 0x0A, 0x87, 0x0A };

    /// <summary>Gets the list of mimetypes that equate to a JXL.</summary>
    public static string[] MimeTypes { get; } = ["image/jxl"];

    /// <summary>Gets the list of file extensions that equate to a JXL.</summary>
    public static string[] FileExtensions { get; } = ["jxl"];
}
