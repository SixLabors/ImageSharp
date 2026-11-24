// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Enumerates the compression of the pixel data in a bitmap.
/// </summary>
public enum BmpCompression
{
    /// <summary>
    /// The pixel data is not compressed.
    /// </summary>
    None = 0,

    /// <summary>
    /// The pixel data uses run length encoding. The encoder writes RLE8 for 8-bit and RLE4 for 4-bit palette bitmaps.
    /// Other bitmaps are not compressed.
    /// </summary>
    RunLength = 1,

    /// <summary>
    /// The pixel data uses the one-dimensional coding scheme of ITU-T T.4, with Modified Huffman run codes. An EOL code
    /// comes before the first row and after each row. The encoder writes it for 1-bit palette bitmaps, as OS/2 2.x
    /// bitmaps. The OS/2 2.x header cannot hold an ICC profile, so a bitmap with a profile is not compressed. Other
    /// bitmaps are not compressed.
    /// </summary>
    Huffman1D = 2,
}
