// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal enum Av1BlockSize : byte
{
    // See sction 6.10.4 of the Av1 Specification.

    /// <summary>A block of samples, 4 samples wide and 4 samples high.</summary>
    Block4x4 = 0,

    /// <summary>A block of samples, 4 samples wide and 8 samples high.</summary>
    Block4x8 = 1,

    /// <summary>A block of samples, 8 samples wide and 4 samples high.</summary>
    Block8x4 = 2,

    /// <summary>A block of samples, 8 samples wide and 8 samples high.</summary>
    Block8x8 = 3,

    /// <summary>A block of samples, 8 samples wide and 16 samples high.</summary>
    Block8x16 = 4,

    /// <summary>A block of samples, 16 samples wide and 8 samples high.</summary>
    Block16x8 = 5,

    /// <summary>A block of samples, 16 samples wide and 16 samples high.</summary>
    Block16x16 = 6,

    /// <summary>A block of samples, 16 samples wide and 32 samples high.</summary>
    Block16x32 = 7,

    /// <summary>A block of samples, 32 samples wide and 16 samples high.</summary>
    Block32x16 = 8,

    /// <summary>A block of samples, 32 samples wide and 32 samples high.</summary>
    Block32x32 = 9,

    /// <summary>A block of samples, 32 samples wide and 64 samples high.</summary>
    Block32x64 = 10,

    /// <summary>A block of samples, 64 samples wide and 32 samples high.</summary>
    Block64x32 = 11,

    /// <summary>A block of samples, 64 samples wide and 64 samples high.</summary>
    Block64x64 = 12,

    /// <summary>A block of samples, 64 samples wide and 128 samples high.</summary>
    Block64x128 = 13,

    /// <summary>A block of samples, 128 samples wide and 64 samples high.</summary>
    Block128x64 = 14,

    /// <summary>A block of samples, 128 samples wide and 128 samples high.</summary>
    Block128x128 = 15,

    /// <summary>A block of samples, 4 samples wide and 16 samples high.</summary>
    Block4x16 = 16,

    /// <summary>A block of samples, 16 samples wide and 4 samples high.</summary>
    Block16x4 = 17,

    /// <summary>A block of samples, 8 samples wide and 32 samples high.</summary>
    Block8x32 = 18,

    /// <summary>A block of samples, 32 samples wide and 8 samples high.</summary>
    Block32x8 = 19,

    /// <summary>A block of samples, 16 samples wide and 64 samples high.</summary>
    Block16x64 = 20,

    /// <summary>A block of samples, 64 samples wide and 16 samples high.</summary>
    Block64x16 = 21,
    AllSizes = 22,
    SizeS = Block4x16,
    Invalid = 255,
    Largest = SizeS - 1,
}
