// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Performs transpose on JPEG XL DCT blocks.
/// </summary>
internal static class JxlTranspose
{
    // TODO: SIMD
    public static void Transpose(int r, int c, JxlDctSource from, JxlDctOutput to)
    {
        for (int n = 0; n < r; n++)
        {
            for (int m = 0; m < c; m++)
            {
                to.Write(from.Read(n, m), m, n);
            }
        }
    }
}
