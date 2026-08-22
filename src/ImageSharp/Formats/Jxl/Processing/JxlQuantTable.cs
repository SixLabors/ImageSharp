// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Specifies which quantization table to use depending on
/// transform &amp; block type.
/// </summary>
internal enum JxlQuantTable : byte
{
    DCT = 0,
    IDENTITY,
    DCT2X2,
    DCT4X4,
    DCT16X16,
    DCT32X32,

    // DCT16X8
    DCT8X16,

    // DCT32X8
    DCT8X32,

    // DCT32X16
    DCT16X32,
    DCT4X8,

    // DCT8X4
    AFV0,

    // AFV1
    // AFV2
    // AFV3
    DCT64X64,

    // DCT64X32,
    DCT32X64,
    DCT128X128,

    // DCT128X64,
    DCT64X128,
    DCT256X256,

    // DCT256X128,
    DCT128X256
}
