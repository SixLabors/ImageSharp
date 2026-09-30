// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// Utilities specific to noise stages.
/// </summary>
internal static class NoiseStageUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> Clamp0ToMax(Vector<float> x, Vector<float> maxValue)
    {
        Vector<float> clamped = Vector.Min(x, maxValue);
        return Vector.Max(Vector<float>.Zero, clamped);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector<float> NoiseStrength(ref StrengthEvaluationLut eval, Vector<float> x) => Clamp0ToMax(eval.Compute(x), Vector<float>.One);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddNoiseToRgb(
        Vector<float> rndNoiseR,
        Vector<float> rndNoiseG,
        Vector<float> rndNoiseCor,
        Vector<float> noiseStrengthG,
        Vector<float> noiseStrengthR,
        float yToX,
        float yToB,
        Span<float> outX,
        Span<float> outY,
        Span<float> outB)
    {
        Vector<float> rgCorr = Vector.Create(0.9921875f); // 127/128
        Vector<float> rgnCorr = Vector.Create(0.0078125f); // 1/128

        Vector<float> redNoise = noiseStrengthR * Vector.FusedMultiplyAdd(rgnCorr, rndNoiseR, rgCorr * rndNoiseCor);
        Vector<float> greenNoise = noiseStrengthG * Vector.FusedMultiplyAdd(rgnCorr, rndNoiseG, rgCorr * rndNoiseCor);

        Vector<float> vx = Vector.Create<float>(outX);
        Vector<float> vy = Vector.Create<float>(outY);
        Vector<float> vb = Vector.Create<float>(outB);

        Vector<float> rgNoise = redNoise + greenNoise;
        vx = Vector.FusedMultiplyAdd(Vector.Create(yToX), rgNoise, redNoise - greenNoise) + vx;
        vy += rgNoise;
        vb = Vector.FusedMultiplyAdd(Vector.Create(yToB), rgNoise, vb);

        vx.CopyTo(outX);
        vy.CopyTo(outY);
        vb.CopyTo(outB);
    }
}
