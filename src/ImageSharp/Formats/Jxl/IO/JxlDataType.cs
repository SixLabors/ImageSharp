// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO;

/// <summary>
/// Specifies which data type to use for sample values
/// per channel per pixel.
/// </summary>
internal enum JxlDataType : byte
{
    /// <summary>
    /// Use float
    /// </summary>
    Single = 0,

    /// <summary>
    /// Use byte. May clip wide color gamut data.
    /// </summary>
    Byte = 2,

    /// <summary>
    /// Use ushort. May clip wide color gamut data.
    /// </summary>
    UInt16 = 3,

    /// <summary>
    /// Use 16-bit IEEE 754 half-precision floating-point values.
    /// </summary>
    Half = 5
}
