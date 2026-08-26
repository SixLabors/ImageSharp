// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the four-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Applies the four-point forward discrete cosine transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Dct4<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
    {
        // Libaom forms both outputs of each mirror pair together. This preserves the saturating Int16 AddSub
        // primitive used by Highway while the Int32 and scalar specializations retain their native arithmetic.
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[0], input[3], out output[0], out output[3]);
        Av1ForwardTransformArithmetic<TValue>.AddSubtract(input[1], input[2], out output[1], out output[2]);

        ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        Av1TransformRounding rounding = Av1ForwardTransformArithmetic<TValue>.CreateRounding(cosBit);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[32], cospi[32], output[0], output[1], out step[0], out step[2], cosBit, in rounding);
        Av1ForwardTransformArithmetic<TValue>.Butterfly(cospi[16], cospi[48], output[3], output[2], out step[1], out step[3], cosBit, in rounding);

        output[0] = step[0];
        output[1] = step[1];
        output[2] = step[2];
        output[3] = step[3];
    }
}
