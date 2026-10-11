// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ChromaFromLuma;

internal static class JxlChromaFromLumaEncoder
{
    public static JxlImageF InitializeDcStorage(Configuration configuration, int numBlocks)
    {
        JxlImageF dcValues = new(configuration, JxlMath.RoundUpTo(numBlocks, Vector<float>.Count), 4);

        for (int y = 0; y < 4; y++)
        {
            Span<float> row = dcValues.GetRow(y);

            for (int x = dcValues.XSize - Vector<float>.Count; x < dcValues.XSize; x++)
            {
                row[x] = 0;
            }
        }

        return dcValues;
    }
}
