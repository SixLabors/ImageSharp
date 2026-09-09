// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines the four-point forward DCT operator.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Implements the four-point forward discrete cosine transform.
    /// </summary>
    internal readonly struct Dct4Operator : IAv1ForwardTransform1dOperator
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
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<int>.CreateRounding(cosBit);

            int input0 = Load<int>(ref values, inputStride, 0);
            int input1 = Load<int>(ref values, inputStride, 1);
            int input2 = Load<int>(ref values, inputStride, 2);
            int input3 = Load<int>(ref values, inputStride, 3);

            // The paired stage keeps the axes in their native lane representation. Packed short lanes therefore retain
            // Highway's saturating add/subtract behavior before the widening butterfly multiplication.
            Av1ForwardTransformArithmetic<int>.AddSubtract(input0, input3, out buffer0[0], out buffer0[3]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(input1, input2, out buffer0[1], out buffer0[2]);
            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out int output0,
                out int output2,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out int output1,
                out int output3,
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
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<short>.CreateRounding(cosBit);

            short input0 = Load<short>(ref values, inputStride, 0);
            short input1 = Load<short>(ref values, inputStride, 1);
            short input2 = Load<short>(ref values, inputStride, 2);
            short input3 = Load<short>(ref values, inputStride, 3);

            // The paired stage keeps the axes in their native lane representation. Packed short lanes therefore retain
            // Highway's saturating add/subtract behavior before the widening butterfly multiplication.
            Av1ForwardTransformArithmetic<short>.AddSubtract(input0, input3, out buffer0[0], out buffer0[3]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(input1, input2, out buffer0[1], out buffer0[2]);
            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out short output0,
                out short output2,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out short output1,
                out short output3,
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
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector128<short>>.CreateRounding(cosBit);

            Vector128<short> input0 = Load<Vector128<short>>(ref values, inputStride, 0);
            Vector128<short> input1 = Load<Vector128<short>>(ref values, inputStride, 1);
            Vector128<short> input2 = Load<Vector128<short>>(ref values, inputStride, 2);
            Vector128<short> input3 = Load<Vector128<short>>(ref values, inputStride, 3);

            // The paired stage keeps the axes in their native lane representation. Packed short lanes therefore retain
            // Highway's saturating add/subtract behavior before the widening butterfly multiplication.
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(input0, input3, out buffer0[0], out buffer0[3]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(input1, input2, out buffer0[1], out buffer0[2]);
            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector128<short> output0,
                out Vector128<short> output2,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector128<short> output1,
                out Vector128<short> output3,
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
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector256<short>>.CreateRounding(cosBit);

            Vector256<short> input0 = Load<Vector256<short>>(ref values, inputStride, 0);
            Vector256<short> input1 = Load<Vector256<short>>(ref values, inputStride, 1);
            Vector256<short> input2 = Load<Vector256<short>>(ref values, inputStride, 2);
            Vector256<short> input3 = Load<Vector256<short>>(ref values, inputStride, 3);

            // The paired stage keeps the axes in their native lane representation. Packed short lanes therefore retain
            // Highway's saturating add/subtract behavior before the widening butterfly multiplication.
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(input0, input3, out buffer0[0], out buffer0[3]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(input1, input2, out buffer0[1], out buffer0[2]);
            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector256<short> output0,
                out Vector256<short> output2,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector256<short> output1,
                out Vector256<short> output3,
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
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector512<short>>.CreateRounding(cosBit);

            Vector512<short> input0 = Load<Vector512<short>>(ref values, inputStride, 0);
            Vector512<short> input1 = Load<Vector512<short>>(ref values, inputStride, 1);
            Vector512<short> input2 = Load<Vector512<short>>(ref values, inputStride, 2);
            Vector512<short> input3 = Load<Vector512<short>>(ref values, inputStride, 3);

            // The paired stage keeps the axes in their native lane representation. Packed short lanes therefore retain
            // Highway's saturating add/subtract behavior before the widening butterfly multiplication.
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(input0, input3, out buffer0[0], out buffer0[3]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(input1, input2, out buffer0[1], out buffer0[2]);
            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector512<short> output0,
                out Vector512<short> output2,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector512<short> output1,
                out Vector512<short> output3,
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
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector128<int>>.CreateRounding(cosBit);

            Vector128<int> input0 = Load<Vector128<int>>(ref values, inputStride, 0);
            Vector128<int> input1 = Load<Vector128<int>>(ref values, inputStride, 1);
            Vector128<int> input2 = Load<Vector128<int>>(ref values, inputStride, 2);
            Vector128<int> input3 = Load<Vector128<int>>(ref values, inputStride, 3);

            // The paired stage keeps the axes in their native lane representation. Packed short lanes therefore retain
            // Highway's saturating add/subtract behavior before the widening butterfly multiplication.
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(input0, input3, out buffer0[0], out buffer0[3]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(input1, input2, out buffer0[1], out buffer0[2]);
            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector128<int> output0,
                out Vector128<int> output2,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector128<int> output1,
                out Vector128<int> output3,
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
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector256<int>>.CreateRounding(cosBit);

            Vector256<int> input0 = Load<Vector256<int>>(ref values, inputStride, 0);
            Vector256<int> input1 = Load<Vector256<int>>(ref values, inputStride, 1);
            Vector256<int> input2 = Load<Vector256<int>>(ref values, inputStride, 2);
            Vector256<int> input3 = Load<Vector256<int>>(ref values, inputStride, 3);

            // The paired stage keeps the axes in their native lane representation. Packed short lanes therefore retain
            // Highway's saturating add/subtract behavior before the widening butterfly multiplication.
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(input0, input3, out buffer0[0], out buffer0[3]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(input1, input2, out buffer0[1], out buffer0[2]);
            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector256<int> output0,
                out Vector256<int> output2,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector256<int> output1,
                out Vector256<int> output3,
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
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Av1TransformRounding rounding = Av1ForwardTransformArithmetic<Vector512<int>>.CreateRounding(cosBit);

            Vector512<int> input0 = Load<Vector512<int>>(ref values, inputStride, 0);
            Vector512<int> input1 = Load<Vector512<int>>(ref values, inputStride, 1);
            Vector512<int> input2 = Load<Vector512<int>>(ref values, inputStride, 2);
            Vector512<int> input3 = Load<Vector512<int>>(ref values, inputStride, 3);

            // The paired stage keeps the axes in their native lane representation. Packed short lanes therefore retain
            // Highway's saturating add/subtract behavior before the widening butterfly multiplication.
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(input0, input3, out buffer0[0], out buffer0[3]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(input1, input2, out buffer0[1], out buffer0[2]);
            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer0[0],
                buffer0[1],
                out Vector512<int> output0,
                out Vector512<int> output2,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer0[3],
                buffer0[2],
                out Vector512<int> output1,
                out Vector512<int> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
        }
    }
}
