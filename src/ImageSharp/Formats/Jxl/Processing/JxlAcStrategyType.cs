// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal enum JxlAcStrategyType : ushort
{
    // Regular block size DCT
    DCT = 0,

    // Encode pixels without transforming
    IDENTITY = 1,

    // Use 2-by-2 DCT
    DCT2X2 = 2,

    // Use 4-by-4 DCT
    DCT4X4 = 3,

    // Use 16-by-16 DCT
    DCT16X16 = 4,

    // Use 32-by-32 DCT
    DCT32X32 = 5,

    // Use 16-by-8 DCT
    DCT16X8 = 6,

    // Use 8-by-16 DCT
    DCT8X16 = 7,

    // Use 32-by-8 DCT
    DCT32X8 = 8,

    // Use 8-by-32 DCT
    DCT8X32 = 9,

    // Use 32-by-16 DCT
    DCT32X16 = 10,

    // Use 16-by-32 DCT
    DCT16X32 = 11,

    // 4x8 and 8x4 DCT
    DCT4X8 = 12,
    DCT8X4 = 13,

    // Corner-DCT.
    AFV0 = 14,

    AFV1 = 15,
    AFV2 = 16,
    AFV3 = 17,

    // Larger DCTs
    DCT64X64 = 18,
    DCT64X32 = 19,
    DCT32X64 = 20,

    // No transforms smaller than 64x64 are allowed below.
    DCT128X128 = 21,
    DCT128X64 = 22,
    DCT64X128 = 23,
    DCT256X256 = 24,
    DCT256X128 = 25,
    DCT128X256 = 26
}
