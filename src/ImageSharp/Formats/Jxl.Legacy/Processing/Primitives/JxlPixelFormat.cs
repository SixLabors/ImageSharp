// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Data type for the sample values per channel per pixel
/// for the output buffer for pixels.
/// </summary>
internal struct JxlPixelFormat
{
    /// <summary>
    /// Gets or sets the amount of channels available in a pixel buffer.
    /// </summary>
    public int Channels { get; set; }

    /// <summary>
    /// Gets or sets the data type of each channel.
    /// </summary>
    public JxlDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets a value that denotes whether multi-byte data types are represented in
    /// big-endian or little-endian format. Applies to ushort
    /// and float data types.
    /// </summary>
    public ByteOrder Endianness { get; set; }

    /// <summary>
    /// Gets or sets the alignment of scanlines to a multiple of
    /// align bytes.
    /// </summary>
    public int Align { get; set; }
}
