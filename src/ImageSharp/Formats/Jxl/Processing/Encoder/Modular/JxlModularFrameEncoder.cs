// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Modular;

internal class JxlModularFrameEncoder
{
    public static void QuantizeChannel(JxlModularChannel ch, int q)
    {
        if (q == 1)
        {
            // No quantization - q=1 keeps values as-is.
            return;
        }

        JxlImageI plane = ch.Plane;

        for (int y = 0; y < plane.YSize; y++)
        {
            Span<int> row = plane.GetRow(y);

            for (int x = 0; x < plane.XSize; x++)
            {
                if (row[x] < 0)
                {
                    row[x] = -((-row[x] + (q / 2)) / q) * q;
                }
                else
                {
                    row[x] = ((row[x] + (q / 2)) / q) * q;
                }
            }
        }
    }
}
