// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Specifies the kind of bit depth.
/// </summary>
internal enum JxlBitDepthType : byte
{
    /// <summary>
    /// Default setting, where the encoder expects the
    /// input pixels to use the full range of the pixel format
    /// data type (e.g. for ushort, the input range is 0..65535
    /// and the value 65535 is mapped to 1.0 when converting
    /// to float), and the decoder uses the full range to output
    /// pixels.
    /// </summary>
    FromPixelFormat,

    /// <summary>
    /// When selected, the encoder expects the input pixels
    /// to be in the range defined by the bits per sample value of the
    /// basic info (e.g., for 12-bit images using ushort data types
    /// the range is 0..4095 and the 4095 value is mapped to 1.0 when
    /// converting to float), and the decoder outputs pixels in
    /// this range.
    /// </summary>
    FromCodeStream,

    /// <summary>
    /// Specifies custom ranges for pixel outputs.
    /// </summary>
    Custom = 2,
}
