// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal static class Av1BlockSizeExtensions
{
    private static readonly int[] SizeWide = { 1, 1, 2, 2, 2, 4, 4, 4, 8, 8, 8, 16, 16, 16, 32, 32, 1, 4, 2, 8, 4, 16 };
    private static readonly int[] SizeHigh = { 1, 2, 1, 2, 4, 2, 4, 8, 4, 8, 16, 8, 16, 32, 16, 32, 4, 1, 8, 2, 16, 4 };

    public static int Get4x4WideCount(this Av1BlockSize blockSize) => SizeWide[(int)blockSize];

    public static int Get4x4HighCount(this Av1BlockSize blockSize) => SizeHigh[(int)blockSize];
}
