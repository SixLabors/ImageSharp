// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 8-point inverse DCT with at most 1 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees all later inputs are zero. Rotations with one surviving input retain
    /// their original rounding boundary, and all nonzero butterfly outputs retain their stage clamps.
    /// Vector fields identify transform positions; lanes remain independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Dct8Low1Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 1;

        /// <inheritdoc/>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange)
        {
            // Only the DC basis survives. The remaining butterflies copy that value into every output;
            // inverse stage ranges are uniform within an axis, so one clamp covers the repeated merges.
            int cosine = Av1SinusConstants.CosinusPi(cosBit)[32];
            int value = Av1Transform1dMath.Clamp(Av1Math.RoundShift((long)input[0] * cosine, cosBit), stageRange[5]);

            output[..8].Fill(value);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector256<int>> input,
            ref Av1TransformVector<Vector256<int>> output,
            ref Av1TransformVector<Vector256<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
        {
            // Only the DC basis survives. The remaining butterflies copy that value into every output;
            // inverse stage ranges are uniform within an axis, so one clamp covers the repeated merges.
            int cosine = Av1SinusConstants.CosinusPi(cosBit)[32];
            Vector256<int> value = Av1Transform1dMath.Clamp(Av1Transform1dMath.MultiplyRound(input.V0, cosine, cosBit), stageRange[5]);

            output.V0 = value;
            output.V1 = value;
            output.V2 = value;
            output.V3 = value;
            output.V4 = value;
            output.V5 = value;
            output.V6 = value;
            output.V7 = value;
        }

        /// <inheritdoc/>
        public static void Transform(
            ref Av1TransformVector<Vector128<int>> input,
            ref Av1TransformVector<Vector128<int>> output,
            ref Av1TransformVector<Vector128<int>> step,
            int cosBit,
            InlineArray12<byte> stageRange)
        {
            // Only the DC basis survives. The remaining butterflies copy that value into every output;
            // inverse stage ranges are uniform within an axis, so one clamp covers the repeated merges.
            int cosine = Av1SinusConstants.CosinusPi(cosBit)[32];
            Vector128<int> value = Av1Transform1dMath.Clamp(Av1Transform1dMath.MultiplyRound(input.V0, cosine, cosBit), stageRange[5]);

            output.V0 = value;
            output.V1 = value;
            output.V2 = value;
            output.V3 = value;
            output.V4 = value;
            output.V5 = value;
            output.V6 = value;
            output.V7 = value;
        }
    }
}
