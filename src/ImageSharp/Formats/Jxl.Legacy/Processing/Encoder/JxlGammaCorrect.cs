// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal static class JxlGammaCorrect
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SRgb8ToLinearDirect(double srgb)
    {
        if (srgb <= 0.0)
        {
            return 0.0;
        }

        if (srgb <= 0.04045)
        {
            return srgb / 12.92;
        }

        if (srgb >= 1.0)
        {
            return 1.0;
        }

        return Math.Pow((srgb + 0.055) / 1.055, 2.4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double LinearToSRgb8Direct(double linear)
    {
        if (linear <= 0.0)
        {
            return 0.0;
        }

        if (linear >= 1.0)
        {
            return 1.0;
        }

        if (linear <= 0.0031308)
        {
            return linear * 12.92;
        }

        return (Math.Pow(linear, 1.0 / 2.4) * 1.055) - 0.055;
    }
}
