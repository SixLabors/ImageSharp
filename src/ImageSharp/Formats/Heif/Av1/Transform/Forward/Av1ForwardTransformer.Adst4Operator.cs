// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines the four-point forward ADST operator.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Implements the four-point forward asymmetric discrete sine transform.
    /// </summary>
    internal readonly struct Adst4Operator : IAv1ForwardTransform1dOperator
    {
        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<int> buffer0,
            ref Av1TransformVector<int> buffer1,
            int cosBit)
        {
            _ = buffer0;
            _ = buffer1;

            ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<int>.CreateRounding(cosBit);
            int input0 = Load<int>(ref values, inputStride, 0);
            int input1 = Load<int>(ref values, inputStride, 1);
            int input2 = Load<int>(ref values, inputStride, 2);
            int input3 = Load<int>(ref values, inputStride, 3);
            int input01 = Av1ForwardTransformArithmetic<int>.Add(input0, input1);

            // Packed lanes form input0 + input1 before widening, matching Highway's observable saturating arithmetic.
            int output0 = Av1ForwardTransformArithmetic<int>.MultiplyAddRound(
                sinpi[1], input0, sinpi[2], input1, sinpi[3], input2, sinpi[4], input3, cosBit, in rounding);

            int output1 = Av1ForwardTransformArithmetic<int>.MultiplyAddRound(
                sinpi[3], input01, -sinpi[3], input3, 0, input0, 0, input0, cosBit, in rounding);

            int output2 = Av1ForwardTransformArithmetic<int>.MultiplyAddRound(
                sinpi[4], input0, -sinpi[1], input1, -sinpi[3], input2, sinpi[2], input3, cosBit, in rounding);

            // This expression preserves Highway's widened w2 - w0 + 3 * v5 sequence with one rounding point.
            int output3 = Av1ForwardTransformArithmetic<int>.MultiplyAddRound(
                sinpi[4] - sinpi[1],
                input0,
                -sinpi[1] - sinpi[2],
                input1,
                sinpi[3],
                input2,
                sinpi[2] - sinpi[4],
                input3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<short> buffer0,
            ref Av1TransformVector<short> buffer1,
            int cosBit)
        {
            _ = buffer0;
            _ = buffer1;

            ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<short>.CreateRounding(cosBit);
            short input0 = Load<short>(ref values, inputStride, 0);
            short input1 = Load<short>(ref values, inputStride, 1);
            short input2 = Load<short>(ref values, inputStride, 2);
            short input3 = Load<short>(ref values, inputStride, 3);
            short input01 = Av1ForwardTransformArithmetic<short>.Add(input0, input1);

            // Packed lanes form input0 + input1 before widening, matching Highway's observable saturating arithmetic.
            short output0 = Av1ForwardTransformArithmetic<short>.MultiplyAddRound(
                sinpi[1], input0, sinpi[2], input1, sinpi[3], input2, sinpi[4], input3, cosBit, in rounding);

            short output1 = Av1ForwardTransformArithmetic<short>.MultiplyAddRound(
                sinpi[3], input01, -sinpi[3], input3, 0, input0, 0, input0, cosBit, in rounding);

            short output2 = Av1ForwardTransformArithmetic<short>.MultiplyAddRound(
                sinpi[4], input0, -sinpi[1], input1, -sinpi[3], input2, sinpi[2], input3, cosBit, in rounding);

            // This expression preserves Highway's widened w2 - w0 + 3 * v5 sequence with one rounding point.
            short output3 = Av1ForwardTransformArithmetic<short>.MultiplyAddRound(
                sinpi[4] - sinpi[1],
                input0,
                -sinpi[1] - sinpi[2],
                input1,
                sinpi[3],
                input2,
                sinpi[2] - sinpi[4],
                input3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector128<short>> buffer0,
            ref Av1TransformVector<Vector128<short>> buffer1,
            int cosBit)
        {
            _ = buffer0;
            _ = buffer1;

            ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector128<short>>.CreateRounding(cosBit);
            Vector128<short> input0 = Load<Vector128<short>>(ref values, inputStride, 0);
            Vector128<short> input1 = Load<Vector128<short>>(ref values, inputStride, 1);
            Vector128<short> input2 = Load<Vector128<short>>(ref values, inputStride, 2);
            Vector128<short> input3 = Load<Vector128<short>>(ref values, inputStride, 3);
            Vector128<short> input01 = Av1ForwardTransformArithmetic<Vector128<short>>.Add(input0, input1);

            // Packed lanes form input0 + input1 before widening, matching Highway's observable saturating arithmetic.
            Vector128<short> output0 = Av1ForwardTransformArithmetic<Vector128<short>>.MultiplyAddRound(
                sinpi[1], input0, sinpi[2], input1, sinpi[3], input2, sinpi[4], input3, cosBit, in rounding);

            Vector128<short> output1 = Av1ForwardTransformArithmetic<Vector128<short>>.MultiplyAddRound(
                sinpi[3], input01, -sinpi[3], input3, 0, input0, 0, input0, cosBit, in rounding);

            Vector128<short> output2 = Av1ForwardTransformArithmetic<Vector128<short>>.MultiplyAddRound(
                sinpi[4], input0, -sinpi[1], input1, -sinpi[3], input2, sinpi[2], input3, cosBit, in rounding);

            // This expression preserves Highway's widened w2 - w0 + 3 * v5 sequence with one rounding point.
            Vector128<short> output3 = Av1ForwardTransformArithmetic<Vector128<short>>.MultiplyAddRound(
                sinpi[4] - sinpi[1],
                input0,
                -sinpi[1] - sinpi[2],
                input1,
                sinpi[3],
                input2,
                sinpi[2] - sinpi[4],
                input3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector256<short>> buffer0,
            ref Av1TransformVector<Vector256<short>> buffer1,
            int cosBit)
        {
            _ = buffer0;
            _ = buffer1;

            ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector256<short>>.CreateRounding(cosBit);
            Vector256<short> input0 = Load<Vector256<short>>(ref values, inputStride, 0);
            Vector256<short> input1 = Load<Vector256<short>>(ref values, inputStride, 1);
            Vector256<short> input2 = Load<Vector256<short>>(ref values, inputStride, 2);
            Vector256<short> input3 = Load<Vector256<short>>(ref values, inputStride, 3);
            Vector256<short> input01 = Av1ForwardTransformArithmetic<Vector256<short>>.Add(input0, input1);

            // Packed lanes form input0 + input1 before widening, matching Highway's observable saturating arithmetic.
            Vector256<short> output0 = Av1ForwardTransformArithmetic<Vector256<short>>.MultiplyAddRound(
                sinpi[1], input0, sinpi[2], input1, sinpi[3], input2, sinpi[4], input3, cosBit, in rounding);

            Vector256<short> output1 = Av1ForwardTransformArithmetic<Vector256<short>>.MultiplyAddRound(
                sinpi[3], input01, -sinpi[3], input3, 0, input0, 0, input0, cosBit, in rounding);

            Vector256<short> output2 = Av1ForwardTransformArithmetic<Vector256<short>>.MultiplyAddRound(
                sinpi[4], input0, -sinpi[1], input1, -sinpi[3], input2, sinpi[2], input3, cosBit, in rounding);

            // This expression preserves Highway's widened w2 - w0 + 3 * v5 sequence with one rounding point.
            Vector256<short> output3 = Av1ForwardTransformArithmetic<Vector256<short>>.MultiplyAddRound(
                sinpi[4] - sinpi[1],
                input0,
                -sinpi[1] - sinpi[2],
                input1,
                sinpi[3],
                input2,
                sinpi[2] - sinpi[4],
                input3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector512<short>> buffer0,
            ref Av1TransformVector<Vector512<short>> buffer1,
            int cosBit)
        {
            _ = buffer0;
            _ = buffer1;

            ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector512<short>>.CreateRounding(cosBit);
            Vector512<short> input0 = Load<Vector512<short>>(ref values, inputStride, 0);
            Vector512<short> input1 = Load<Vector512<short>>(ref values, inputStride, 1);
            Vector512<short> input2 = Load<Vector512<short>>(ref values, inputStride, 2);
            Vector512<short> input3 = Load<Vector512<short>>(ref values, inputStride, 3);
            Vector512<short> input01 = Av1ForwardTransformArithmetic<Vector512<short>>.Add(input0, input1);

            // Packed lanes form input0 + input1 before widening, matching Highway's observable saturating arithmetic.
            Vector512<short> output0 = Av1ForwardTransformArithmetic<Vector512<short>>.MultiplyAddRound(
                sinpi[1], input0, sinpi[2], input1, sinpi[3], input2, sinpi[4], input3, cosBit, in rounding);

            Vector512<short> output1 = Av1ForwardTransformArithmetic<Vector512<short>>.MultiplyAddRound(
                sinpi[3], input01, -sinpi[3], input3, 0, input0, 0, input0, cosBit, in rounding);

            Vector512<short> output2 = Av1ForwardTransformArithmetic<Vector512<short>>.MultiplyAddRound(
                sinpi[4], input0, -sinpi[1], input1, -sinpi[3], input2, sinpi[2], input3, cosBit, in rounding);

            // This expression preserves Highway's widened w2 - w0 + 3 * v5 sequence with one rounding point.
            Vector512<short> output3 = Av1ForwardTransformArithmetic<Vector512<short>>.MultiplyAddRound(
                sinpi[4] - sinpi[1],
                input0,
                -sinpi[1] - sinpi[2],
                input1,
                sinpi[3],
                input2,
                sinpi[2] - sinpi[4],
                input3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector128<int>> buffer0,
            ref Av1TransformVector<Vector128<int>> buffer1,
            int cosBit)
        {
            _ = buffer0;
            _ = buffer1;

            ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector128<int>>.CreateRounding(cosBit);
            Vector128<int> input0 = Load<Vector128<int>>(ref values, inputStride, 0);
            Vector128<int> input1 = Load<Vector128<int>>(ref values, inputStride, 1);
            Vector128<int> input2 = Load<Vector128<int>>(ref values, inputStride, 2);
            Vector128<int> input3 = Load<Vector128<int>>(ref values, inputStride, 3);
            Vector128<int> input01 = Av1ForwardTransformArithmetic<Vector128<int>>.Add(input0, input1);

            // Packed lanes form input0 + input1 before widening, matching Highway's observable saturating arithmetic.
            Vector128<int> output0 = Av1ForwardTransformArithmetic<Vector128<int>>.MultiplyAddRound(
                sinpi[1], input0, sinpi[2], input1, sinpi[3], input2, sinpi[4], input3, cosBit, in rounding);

            Vector128<int> output1 = Av1ForwardTransformArithmetic<Vector128<int>>.MultiplyAddRound(
                sinpi[3], input01, -sinpi[3], input3, 0, input0, 0, input0, cosBit, in rounding);

            Vector128<int> output2 = Av1ForwardTransformArithmetic<Vector128<int>>.MultiplyAddRound(
                sinpi[4], input0, -sinpi[1], input1, -sinpi[3], input2, sinpi[2], input3, cosBit, in rounding);

            // This expression preserves Highway's widened w2 - w0 + 3 * v5 sequence with one rounding point.
            Vector128<int> output3 = Av1ForwardTransformArithmetic<Vector128<int>>.MultiplyAddRound(
                sinpi[4] - sinpi[1],
                input0,
                -sinpi[1] - sinpi[2],
                input1,
                sinpi[3],
                input2,
                sinpi[2] - sinpi[4],
                input3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector256<int>> buffer0,
            ref Av1TransformVector<Vector256<int>> buffer1,
            int cosBit)
        {
            _ = buffer0;
            _ = buffer1;

            ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector256<int>>.CreateRounding(cosBit);
            Vector256<int> input0 = Load<Vector256<int>>(ref values, inputStride, 0);
            Vector256<int> input1 = Load<Vector256<int>>(ref values, inputStride, 1);
            Vector256<int> input2 = Load<Vector256<int>>(ref values, inputStride, 2);
            Vector256<int> input3 = Load<Vector256<int>>(ref values, inputStride, 3);
            Vector256<int> input01 = Av1ForwardTransformArithmetic<Vector256<int>>.Add(input0, input1);

            // Packed lanes form input0 + input1 before widening, matching Highway's observable saturating arithmetic.
            Vector256<int> output0 = Av1ForwardTransformArithmetic<Vector256<int>>.MultiplyAddRound(
                sinpi[1], input0, sinpi[2], input1, sinpi[3], input2, sinpi[4], input3, cosBit, in rounding);

            Vector256<int> output1 = Av1ForwardTransformArithmetic<Vector256<int>>.MultiplyAddRound(
                sinpi[3], input01, -sinpi[3], input3, 0, input0, 0, input0, cosBit, in rounding);

            Vector256<int> output2 = Av1ForwardTransformArithmetic<Vector256<int>>.MultiplyAddRound(
                sinpi[4], input0, -sinpi[1], input1, -sinpi[3], input2, sinpi[2], input3, cosBit, in rounding);

            // This expression preserves Highway's widened w2 - w0 + 3 * v5 sequence with one rounding point.
            Vector256<int> output3 = Av1ForwardTransformArithmetic<Vector256<int>>.MultiplyAddRound(
                sinpi[4] - sinpi[1],
                input0,
                -sinpi[1] - sinpi[2],
                input1,
                sinpi[3],
                input2,
                sinpi[2] - sinpi[4],
                input3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
        }

        /// <inheritdoc/>
        public static void Transform(
            ref byte values,
            nint inputStride,
            nint outputStride,
            ref Av1TransformVector<Vector512<int>> buffer0,
            ref Av1TransformVector<Vector512<int>> buffer1,
            int cosBit)
        {
            _ = buffer0;
            _ = buffer1;

            ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector512<int>>.CreateRounding(cosBit);
            Vector512<int> input0 = Load<Vector512<int>>(ref values, inputStride, 0);
            Vector512<int> input1 = Load<Vector512<int>>(ref values, inputStride, 1);
            Vector512<int> input2 = Load<Vector512<int>>(ref values, inputStride, 2);
            Vector512<int> input3 = Load<Vector512<int>>(ref values, inputStride, 3);
            Vector512<int> input01 = Av1ForwardTransformArithmetic<Vector512<int>>.Add(input0, input1);

            // Packed lanes form input0 + input1 before widening, matching Highway's observable saturating arithmetic.
            Vector512<int> output0 = Av1ForwardTransformArithmetic<Vector512<int>>.MultiplyAddRound(
                sinpi[1], input0, sinpi[2], input1, sinpi[3], input2, sinpi[4], input3, cosBit, in rounding);

            Vector512<int> output1 = Av1ForwardTransformArithmetic<Vector512<int>>.MultiplyAddRound(
                sinpi[3], input01, -sinpi[3], input3, 0, input0, 0, input0, cosBit, in rounding);

            Vector512<int> output2 = Av1ForwardTransformArithmetic<Vector512<int>>.MultiplyAddRound(
                sinpi[4], input0, -sinpi[1], input1, -sinpi[3], input2, sinpi[2], input3, cosBit, in rounding);

            // This expression preserves Highway's widened w2 - w0 + 3 * v5 sequence with one rounding point.
            Vector512<int> output3 = Av1ForwardTransformArithmetic<Vector512<int>>.MultiplyAddRound(
                sinpi[4] - sinpi[1],
                input0,
                -sinpi[1] - sinpi[2],
                input1,
                sinpi[3],
                input2,
                sinpi[2] - sinpi[4],
                input3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
        }
    }
}
