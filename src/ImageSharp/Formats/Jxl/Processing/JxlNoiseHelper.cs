// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal static class JxlNoiseHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JxlNoiseIndexAndFraction IndexAndFraction(float x)
    {
        const int scaleNumerator = JxlNoiseParameters.NoisePoints - 2;
        const float scale = scaleNumerator / 1.0f;

        float scaledX = MathF.Max(0f, x * scale);
        float floorX = MathF.Floor(scaledX);
        float fractionalX = scaledX - floorX;

        if (scaledX >= scaleNumerator + 1)
        {
            floorX = scaleNumerator;
            fractionalX = 1f;
        }

        return new((int)floorX, fractionalX);
    }
}
