// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Noise;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Noise;

internal static class JxlPhotonNoise
{
    /// <summary>
    /// Assumes a daylight-like spectrum.
    /// </summary>
    private const float PhotonsPerLxSPerUm2 = 11260;

    /// <summary>
    /// Order of magnitude for cameras in the 2010-2020 decade,
    /// taking the CFA into account.
    /// </summary>
    private const float EffectiveQuantumEfficiency = 0.20f;

    private const float PhotoResponseNonUniformity = 0.005f;

    private const float InputReferredReadNoise = 3;

    private const float SensorAreaUm2 = 36000f * 24000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Square(float x) => x * x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Cube(float x) => x * x * x;

    public static JxlNoiseParameters SimulatePhotonNoise(int xSize, int ySize, float iso)
    {
        float opsinAbsorbanceBiasCbrt = MathF.Cbrt(JxlOpsinConstants.OpsinAbsorbanceBias1);
        float h18 = 10f / iso;
        float pixelAreaUm2 = SensorAreaUm2 / (xSize * ySize);
        float electronsPerPixel18 = EffectiveQuantumEfficiency * PhotonsPerLxSPerUm2 * h18 * pixelAreaUm2;
        JxlNoiseParameters parameters = new();
        Span<float> lookup = parameters.Lookup; // Faster than float[]

        for (int i = 0; i < JxlNoiseParameters.NoisePoints; ++i)
        {
            float scaledIndex = i / (JxlNoiseParameters.NoisePoints - 2f);
            float y = 2 * scaledIndex;
            float linear = MathF.Max(0f, Cube(y - opsinAbsorbanceBiasCbrt) + JxlOpsinConstants.OpsinAbsorbanceBias1);
            float electronsPerPixel = electronsPerPixel18 * (linear / 0.18f);
            float noise = MathF.Sqrt(Square(InputReferredReadNoise) + electronsPerPixel + Square(PhotoResponseNonUniformity * electronsPerPixel));
            float linearNoise = noise * (0.18f / electronsPerPixel18);
            float opsinDerivative = (1f / 3) / Square(MathF.Sqrt(linear - JxlOpsinConstants.OpsinAbsorbanceBias1));
            float opsinNoise = linearNoise * opsinDerivative;

            lookup[i] = Math.Clamp(opsinNoise / (0.22f * MathF.Sqrt(2f) * 1.13f), 0f, JxlNoiseConstants.NoiseLutMax);
        }

        return parameters;
    }
}
