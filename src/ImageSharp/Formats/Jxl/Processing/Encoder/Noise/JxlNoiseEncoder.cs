// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Noise;

/// <summary>
/// Noise functions for encoder.
/// </summary>
internal static class JxlNoiseEncoder
{
    public static float GetScoreSumsOfAbsoluteDifferences(JxlImage3F opsin, int x, int y, int blockSize)
    {
        const int smallBlockSizeX = 3;
        const int smallBlockSizeY = 4;

        int numSAD = (blockSize - smallBlockSizeX) * (blockSize - smallBlockSizeY);
        int counter = 0;
        const int offset = 2;

        float[]? pooled = null;

        Span<float> sad = numSAD <= 128
            ? stackalloc float[128].Slice(0, numSAD)
            : pooled = ArrayPool<float>.Shared.Rent(numSAD);

        for (int yBl = 0; yBl + smallBlockSizeY < blockSize; ++yBl)
        {
            for (int xBl = 0; xBl + smallBlockSizeX < blockSize; ++xBl)
            {
                float sadSum = 0;

                for (int cy = 0; cy < smallBlockSizeY; ++cy)
                {
                    for (int cx = 0; cx < smallBlockSizeX; ++cx)
                    {
                        float wnd = 0.5f * (opsin.PlaneRow(1, y + yBl + cy)[x + xBl + cx] + opsin.PlaneRow(0, y + yBl + cy)[x + xBl + cx]);
                        float center = 0.5f * (opsin.PlaneRow(1, y + offset + cy)[x + offset + cx] + opsin.PlaneRow(0, y + offset + cy)[x + offset + cx]);
                        sadSum += MathF.Abs(center - wnd);
                    }
                }

                sad[counter++] = sadSum;
            }
        }

        int samples = numSAD / 2;

        // As with ROAD (rank order absolute distance), we keep the smallest half of
        // the values in SAD (we use here the more robust patch SAD instead of
        // absolute single-pixel differences).
        sad.Sort();

        float totalSadSum = TensorPrimitives.Sum(sad);

        if (pooled is not null)
        {
            ArrayPool<float>.Shared.Return(pooled);
        }

        return totalSadSum / samples;
    }
}
