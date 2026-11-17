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
}
