// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

internal static class Vector4Factory
{
    public static Vector4[] CreateVectors(int length = 2048, int min = 0, int max = 1)
    {
        Random rnd = new(42);
        return GenerateRandomVectorArray(rnd, length, min, max);
    }

    private static Vector4[] GenerateRandomVectorArray(Random rnd, int length, float minVal, float maxVal)
    {
        Vector4[] values = new Vector4[length];

        for (int i = 0; i < length; i++)
        {
            ref Vector4 v = ref values[i];
            v.X = GetRandomFloat(rnd, minVal, maxVal);
            v.Y = GetRandomFloat(rnd, minVal, maxVal);
            v.Z = GetRandomFloat(rnd, minVal, maxVal);
            v.W = GetRandomFloat(rnd, minVal, maxVal);
        }

        return values;
    }

    private static float GetRandomFloat(Random rnd, float minVal, float maxVal)
        => ((float)rnd.NextDouble() * (maxVal - minVal)) + minVal;
}
