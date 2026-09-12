// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO;

/// <summary>
/// Specifies the type of box content.
/// </summary>
internal enum JxlBoxCodingMode : byte
{
    /// <summary>
    /// Compress using Brotli codec.
    /// </summary>
    Brotli,

    /// <summary>
    /// No compression (raw contents).
    /// </summary>
    Uncompressed
}
