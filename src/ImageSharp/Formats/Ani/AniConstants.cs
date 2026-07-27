// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Defines constants used by the ANI format.
/// </summary>
internal static class AniConstants
{
    /// <summary>
    /// The number of bytes in the RIFF identifier, size, and form type.
    /// </summary>
    public const int RiffHeaderSize = 12;

    /// <summary>
    /// The number of bytes in a RIFF chunk identifier and size.
    /// </summary>
    public const int ChunkHeaderSize = 8;

    /// <summary>
    /// The number of bytes required to identify an embedded ICO or CUR resource.
    /// </summary>
    public const int IconDirHeaderSize = 6;

    /// <summary>
    /// The list of MIME types that identify ANI data.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = ["application/x-navi-animation"];

    /// <summary>
    /// The list of file extensions that identify ANI data.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = ["ani"];

    /// <summary>
    /// Gets the RIFF container identifier.
    /// </summary>
    public static ReadOnlySpan<byte> RiffFourCc => "RIFF"u8;

    /// <summary>
    /// Gets the ANI RIFF form type.
    /// </summary>
    public static ReadOnlySpan<byte> AniFormTypeFourCc => "ACON"u8;
}
