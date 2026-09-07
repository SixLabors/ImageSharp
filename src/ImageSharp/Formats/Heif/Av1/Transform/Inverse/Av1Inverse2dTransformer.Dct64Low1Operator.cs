// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Applies the 64-point inverse DCT with at most 1 low-frequency input coefficients.
    /// </summary>
    /// <remarks>
    /// The selected scan bound guarantees all later inputs are zero. Rotations with one surviving input retain
    /// their original rounding boundary, and all nonzero butterfly outputs retain their stage clamps.
    /// Vector fields identify transform positions; lanes remain independent rows or columns throughout.
    /// </remarks>
    internal readonly struct Dct64Low1Operator : IAv1Transform1dOperator
    {
        /// <inheritdoc/>
        public static int InputLength => 1;

        /// <inheritdoc/>
        public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, InlineArray12<byte> stageRange)
        {
            // Only the DC basis survives. The remaining butterflies copy that value into every output;
            // inverse stage ranges are uniform within an axis, so one clamp covers the repeated merges.
            int cosine = Av1SinusConstants.CosinusPi(cosBit)[32];
            int value = Av1Transform1dMath.Clamp(Av1Math.RoundShift((long)input[0] * cosine, cosBit), stageRange[11]);

            output[..64].Fill(value);
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
            Vector256<int> value = Av1Transform1dMath.Clamp(Av1Transform1dMath.MultiplyRound(input.V0, cosine, cosBit), stageRange[11]);

            output.V0 = value;
            output.V1 = value;
            output.V2 = value;
            output.V3 = value;
            output.V4 = value;
            output.V5 = value;
            output.V6 = value;
            output.V7 = value;
            output.V8 = value;
            output.V9 = value;
            output.V10 = value;
            output.V11 = value;
            output.V12 = value;
            output.V13 = value;
            output.V14 = value;
            output.V15 = value;
            output.V16 = value;
            output.V17 = value;
            output.V18 = value;
            output.V19 = value;
            output.V20 = value;
            output.V21 = value;
            output.V22 = value;
            output.V23 = value;
            output.V24 = value;
            output.V25 = value;
            output.V26 = value;
            output.V27 = value;
            output.V28 = value;
            output.V29 = value;
            output.V30 = value;
            output.V31 = value;
            output.V32 = value;
            output.V33 = value;
            output.V34 = value;
            output.V35 = value;
            output.V36 = value;
            output.V37 = value;
            output.V38 = value;
            output.V39 = value;
            output.V40 = value;
            output.V41 = value;
            output.V42 = value;
            output.V43 = value;
            output.V44 = value;
            output.V45 = value;
            output.V46 = value;
            output.V47 = value;
            output.V48 = value;
            output.V49 = value;
            output.V50 = value;
            output.V51 = value;
            output.V52 = value;
            output.V53 = value;
            output.V54 = value;
            output.V55 = value;
            output.V56 = value;
            output.V57 = value;
            output.V58 = value;
            output.V59 = value;
            output.V60 = value;
            output.V61 = value;
            output.V62 = value;
            output.V63 = value;
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
            Vector128<int> value = Av1Transform1dMath.Clamp(Av1Transform1dMath.MultiplyRound(input.V0, cosine, cosBit), stageRange[11]);

            output.V0 = value;
            output.V1 = value;
            output.V2 = value;
            output.V3 = value;
            output.V4 = value;
            output.V5 = value;
            output.V6 = value;
            output.V7 = value;
            output.V8 = value;
            output.V9 = value;
            output.V10 = value;
            output.V11 = value;
            output.V12 = value;
            output.V13 = value;
            output.V14 = value;
            output.V15 = value;
            output.V16 = value;
            output.V17 = value;
            output.V18 = value;
            output.V19 = value;
            output.V20 = value;
            output.V21 = value;
            output.V22 = value;
            output.V23 = value;
            output.V24 = value;
            output.V25 = value;
            output.V26 = value;
            output.V27 = value;
            output.V28 = value;
            output.V29 = value;
            output.V30 = value;
            output.V31 = value;
            output.V32 = value;
            output.V33 = value;
            output.V34 = value;
            output.V35 = value;
            output.V36 = value;
            output.V37 = value;
            output.V38 = value;
            output.V39 = value;
            output.V40 = value;
            output.V41 = value;
            output.V42 = value;
            output.V43 = value;
            output.V44 = value;
            output.V45 = value;
            output.V46 = value;
            output.V47 = value;
            output.V48 = value;
            output.V49 = value;
            output.V50 = value;
            output.V51 = value;
            output.V52 = value;
            output.V53 = value;
            output.V54 = value;
            output.V55 = value;
            output.V56 = value;
            output.V57 = value;
            output.V58 = value;
            output.V59 = value;
            output.V60 = value;
            output.V61 = value;
            output.V62 = value;
            output.V63 = value;
        }
    }
}
