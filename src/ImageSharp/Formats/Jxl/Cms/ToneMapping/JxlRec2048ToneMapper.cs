// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Cms.TransferFunctions;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms.ToneMapping;

internal readonly struct JxlRec2048ToneMapper
{
    private readonly float redY;
    private readonly float greenY;
    private readonly float blueY;

    private readonly float pqMasteringMin;
    private readonly float pqMasteringMax;
    private readonly float pqMasteringRange;
    private readonly float inversePqMasteringRange;

    private readonly float minLum;
    private readonly float maxLum;
    private readonly float ks;
    private readonly float inverseOneMinusKs;
    private readonly float normalizer;
    private readonly float inverseTargetPeak;

    private readonly InlineArray2<float> sourceRange;
    private readonly InlineArray2<float> targetRange;

    public JxlRec2048ToneMapper(InlineArray2<float> sourceRange, InlineArray2<float> targetRange, Vector3 primariesLuminances)
    {
        this.redY = primariesLuminances.X;
        this.greenY = primariesLuminances.Y;
        this.blueY = primariesLuminances.Z;

        this.pqMasteringMin = InverseEotf(sourceRange[0]);
        this.pqMasteringMax = InverseEotf(sourceRange[1]);
        this.pqMasteringRange = this.pqMasteringMax - this.pqMasteringMin;
        this.inversePqMasteringRange = 1.0f / this.pqMasteringRange;

        this.minLum = (InverseEotf(targetRange[0]) - this.pqMasteringMin) * this.inversePqMasteringRange;
        this.maxLum = (InverseEotf(targetRange[1]) - this.pqMasteringMin) * this.inversePqMasteringRange;
        this.ks = (1.5f * this.maxLum) - 0.5f;

        this.inverseOneMinusKs = 1.0f / MathF.Max(1e-6f, 1.0f - this.ks);
        this.normalizer = sourceRange[1] / targetRange[1];
        this.inverseTargetPeak = 1.0f / targetRange[1];

        this.sourceRange = sourceRange;
        this.targetRange = targetRange;
    }

    private static float InverseEotf(float luminance) => (float)JxlPqTransferFunction.EncodedFromDisplay(1.0f, luminance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float T(float a) => (a - this.ks) * this.inverseOneMinusKs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float P(float b)
    {
        float tb = this.T(b); // tb = T(b)
        float tb2 = tb * tb; // tb²
        float tb3 = tb2 * tb2; // tb³

        return (((2 * tb3) - (3 * tb2) + 1) * this.ks) +
           ((tb3 - (2 * tb2) + tb) * (1 - this.ks)) +
           (((-2 * tb3) + (3 * tb2)) * this.maxLum);
    }

    public void ToneMap(Span<float> rgb)
    {
        float luminance = this.sourceRange[1] * ((this.redY * rgb[0]) + (this.greenY * rgb[1]) + (this.blueY * rgb[2]));
        float normalizedPq = MathF.Min(1.0f, (InverseEotf(luminance) - this.pqMasteringMin) * this.inversePqMasteringRange);
        float e2 = (normalizedPq < this.ks) ? normalizedPq : this.P(normalizedPq);
        float oneMinusE2 = 1 - e2;
        float oneMinusE2_2 = oneMinusE2 * oneMinusE2;
        float oneMinusE2_4 = oneMinusE2_2 * oneMinusE2_2;
        float e3 = (this.minLum * oneMinusE2_4) + e2;
        float e4 = (e3 * this.pqMasteringRange) + this.pqMasteringMin;
        float d4 = (float)JxlPqTransferFunction.DisplayFromEncoded(1.0f, e4);
        float newLuminance = Math.Clamp(d4, 0, this.targetRange[1]);
        float minLuminance = 1e-6f;
        bool useCap = luminance <= minLuminance;
        float ratio = newLuminance / MathF.Max(luminance, minLuminance);
        float cap = newLuminance * this.inverseTargetPeak;
        float multiplier = ratio * this.normalizer;

        if (useCap)
        {
            rgb[0] = cap;
            rgb[1] = cap;
            rgb[2] = cap;
        }
        else
        {
            rgb[0] *= multiplier;
            rgb[1] *= multiplier;
            rgb[2] *= multiplier;
        }
    }
}
