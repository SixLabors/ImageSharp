// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal static partial class ZigZag
{
    /// <summary>
    /// Gets span of zig-zag ordering indices.
    /// </summary>
    /// <remarks>
    /// When reading corrupted data, the Huffman decoders could attempt
    /// to reference an entry beyond the end of this array (if the decoded
    /// zero run length reaches past the end of the block).  To prevent
    /// wild stores without adding an inner-loop test, we put some extra
    /// "63"s after the real entries.  This will cause the extra coefficient
    /// to be stored in location 63 of the block, not somewhere random.
    /// The worst case would be a run-length of 15, which means we need 16
    /// fake entries.
    /// </remarks>
    public static ReadOnlySpan<byte> ZigZagOrder =>
    [
        0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,

        // Extra entries for safety in decoder
        63, 63, 63, 63, 63, 63, 63, 63,
        63, 63, 63, 63, 63, 63, 63, 63
    ];

    /// <summary>
    /// Gets span of zig-zag with fused transpose step ordering indices.
    /// </summary>
    /// <remarks>
    /// When reading corrupted data, the Huffman decoders could attempt
    /// to reference an entry beyond the end of this array (if the decoded
    /// zero run length reaches past the end of the block).  To prevent
    /// wild stores without adding an inner-loop test, we put some extra
    /// "63"s after the real entries.  This will cause the extra coefficient
    /// to be stored in location 63 of the block, not somewhere random.
    /// The worst case would be a run-length of 15, which means we need 16
    /// fake entries.
    /// </remarks>
    public static ReadOnlySpan<byte> TransposingOrder =>
    [
        0,  8,  1,  2,  9,  16, 24, 17,
        10, 3,  4,  11, 18, 25, 32, 40,
        33, 26, 19, 12, 5,  6,  13, 20,
        27, 34, 41, 48, 56, 49, 42, 35,
        28, 21, 14, 7,  15, 22, 29, 36,
        43, 50, 57, 58, 51, 44, 37, 30,
        23, 31, 38, 45, 52, 59, 60, 53,
        46, 39, 47, 54, 61, 62, 55, 63,

        // Extra entries for safety in decoder
        63, 63, 63, 63, 63, 63, 63, 63,
        63, 63, 63, 63, 63, 63, 63, 63
    ];
}
