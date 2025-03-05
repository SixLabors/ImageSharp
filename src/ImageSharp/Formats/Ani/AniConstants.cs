// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

internal static class AniConstants
{
    /// <summary>
    /// The list of mime types that equate to an ani.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = ["application/x-navi-animation"];

    /// <summary>
    /// The list of file extensions that equate to an ani.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = ["ani"];

    /// <summary>
    /// Gets the header bytes identifying an ani.
    /// </summary>
    public static ReadOnlySpan<byte> AniFormTypeFourCc => "ACON"u8;

    /// <summary>
    /// Gets the header bytes identifying an ani.
    /// </summary>
    public const uint AniFourCc = 0x41_43_4F_4E;

    public static class ChunkFourCcs
    {
        public static ReadOnlySpan<byte> AniHeader => "anih"u8;

        public static ReadOnlySpan<byte> Seq => "seq "u8;

        public static ReadOnlySpan<byte> Rate => "rate"u8;

        public static ReadOnlySpan<byte> Icon => "icon"u8;
    }
}
