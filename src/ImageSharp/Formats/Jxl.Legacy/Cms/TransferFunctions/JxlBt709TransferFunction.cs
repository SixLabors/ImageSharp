// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Processing;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms.TransferFunctions;

/// <summary>
/// ITU-R BT.709 transfer function
/// </summary>
internal static class JxlBt709TransferFunction
{
    // Encoded From Display constants
    private const float Threshold = 0.018f;
    private const float MulLow = 4.5f;
    private const float MulHi = 1.099f;
    private const float PowHi = 0.45f;
    private const float Sub = -0.099f;

    // Display From Encoded constants
    private const float InverseThreshold = 0.081f;
    private const float InverseMulLow = 1f / 4.5f;
    private const float InverseMulHi = 1f / 1.099f;
    private const float InversePowHi = 1f / 0.45f;
    private const float InverseAdd = 0.099f * InverseMulHi;

    public static float EncodedFromDisplay(float d)
    {
        if (d < Threshold)
        {
            return MulLow * d;
        }

        return (MulHi * MathF.Pow(d, PowHi)) + Sub;
    }

    public static Vector<float> EncodedFromDisplay(Vector<float> x)
    {
        Vector<float> low = Vector.Create(MulLow) * x;
        Vector<float> high = (Vector.Create(MulHi) * JxlSimdUtils.FastPowf(x, Vector.Create(PowHi))) + Vector.Create(Sub);
        return Vector.ConditionalSelect(
            Vector.LessThanOrEqual(x, Vector.Create(Threshold)),
            low,
            high);
    }

    public static Vector<float> DisplayFromEncoded(Vector<float> x)
    {
        Vector<float> low = Vector.Create(InverseMulLow) * x;
        Vector<float> high = JxlSimdUtils.FastPowf((x * Vector.Create(InverseMulHi)) + Vector.Create(InverseAdd), Vector.Create(InversePowHi));
        return Vector.ConditionalSelect(
            Vector.LessThan(x, Vector.Create(InverseThreshold)),
            low,
            high);
    }
}
