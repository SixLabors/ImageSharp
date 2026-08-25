// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Provides the HEVC intra-mode values and chroma-format mapping shared by entropy decoding and reconstruction.
/// </summary>
internal static class HevcIntraPredictionMode
{
    /// <summary>
    /// The horizontal angular prediction mode.
    /// </summary>
    public const int Horizontal = 10;

    /// <summary>
    /// The vertical angular prediction mode.
    /// </summary>
    public const int Vertical = 26;

    /// <summary>
    /// Gets the 4:2:2 chroma intra-angle remapping defined by H.265 Table 8-4.
    /// </summary>
    private static ReadOnlySpan<byte> Chroma422AngleMap =>
    [
        0, 1, 2, 2, 2, 2, 3, 5, 7, 8, 10, 12, 13, 15, 17, 18, 19, 20, 21, 22, 23, 23, 24, 24, 25, 25, 26, 27, 27, 28, 28, 29, 29, 30, 31,
    ];

    /// <summary>
    /// Maps a coded chroma intra mode to the angular mode used by a 4:2:2 chroma block.
    /// </summary>
    /// <param name="mode">The effective coded chroma intra mode.</param>
    /// <returns>The prediction angle used by the rectangular chroma block.</returns>
    public static int RemapChroma422(int mode) => Chroma422AngleMap[mode];
}
