// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// Adaptive quantization encoder
/// </summary>
internal static class JxlAdaptiveQuantization
{
    // Scaling differences between JPEG XL and Butteraugli
    private const float SGMul = 226.77216153508914f;
    private const float SGMul2 = 1f / 73.377132366608819f;

    // Includes correlation factor for std::log -> log2
    private const float SGRetMul = SGMul2 * 18.6580932135f * JxlMath.InverseLog2E;
    private const float SGVOffset = 7.7825991679894591f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ComputeMaskForAcStrategyUse(float outputValue)
    {
        const float multiplier = 1f;
        const float offset = 0.001f;

        return multiplier / (outputValue + offset);
    }

    public static float RatioOfDerivativesOfCubicRootToSimpleGamma(float v, bool invert = false)
    {
        float epsilon = 1e-2f;
        v = Math.Max(0, v); // cannot be < 0
        const float numMul = SGRetMul * 3 * SGMul;
        float voffset = (SGVOffset * JxlMath.InverseLog2E) + epsilon;
        const float denMul = JxlMath.InverseLog2E * SGMul;

        float v2 = v * v;
        float num = (numMul * v2) + epsilon;
        float den = ((denMul * v) * v2) + voffset;

        return invert ? num / den : den / num;
    }
}
