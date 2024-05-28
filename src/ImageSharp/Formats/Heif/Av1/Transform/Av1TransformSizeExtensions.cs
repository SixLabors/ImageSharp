// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Quantization;

internal static class Av1TransformSizeExtensions
{
    private static readonly int[] Size2d = [
    16, 64, 256, 1024, 4096, 32, 32, 128, 128, 512, 512, 2048, 2048, 64, 64, 256, 256, 1024, 1024];

    public static int GetScale(this Av1TransformSize size)
    {
        int pels = Size2d[(int)size];
        return (pels > 1024) ? 2 : (pels > 256) ? 1 : 0;
    }

    public static int GetWidth(this Av1TransformSize size) => (int)size;

    public static int GetHeight(this Av1TransformSize size) => (int)size;

    public static int GetWidthLog2(this Av1TransformSize size) => (int)size;

    public static int GetHeightLog2(this Av1TransformSize size) => (int)size;
}
