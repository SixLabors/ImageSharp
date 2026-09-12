// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Represents the kind of frame encoding.
/// </summary>
internal enum JxlFrameEncoding : byte
{
    /// <summary>
    /// Use VarDCT
    /// </summary>
    VarDct,

    /// <summary>
    /// Use Modular encoding
    /// </summary>
    Modular
}
