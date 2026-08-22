// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Quantization mode.
/// </summary>
internal enum JxlQuantMode : byte
{
    Library,
    Id,
    Dct2,
    Dct4,
    Dct4x8,
    Afv,
    Dct,
    Raw
}
