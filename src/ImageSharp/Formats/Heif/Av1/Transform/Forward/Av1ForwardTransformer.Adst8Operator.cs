// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines the eight-point forward ADST operator.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Implements the eight-point forward asymmetric discrete sine transform.
    /// </summary>
    internal readonly struct Adst8Operator : IAv1ForwardTransform1dOperator
    {
        /// <summary>
        /// Gets the fixed coefficient permutation.
        /// </summary>
        private static ReadOnlySpan<byte> OutputOrder => [1, 6, 3, 4, 5, 2, 7, 0];

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

            // Stage 1 applies the ADST permutation and signs while the source block is still read-only.
            buffer0[0] = Load<int>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 7));
            buffer0[2] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 3));
            buffer0[3] = Load<int>(ref values, inputStride, 4);
            buffer0[4] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 1));
            buffer0[5] = Load<int>(ref values, inputStride, 6);
            buffer0[6] = Load<int>(ref values, inputStride, 2);
            buffer0[7] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 5));

            // Stage 2 rotates the second pair in each four-value group while copying the already aligned pairs.
            buffer1[0] = buffer0[0];
            buffer1[1] = buffer0[1];
            Butterfly(cospi[32], cospi[32], buffer0[2], buffer0[3], ref buffer1, 2, 3, cosBit, in rounding);
            buffer1[4] = buffer0[4];
            buffer1[5] = buffer0[5];
            Butterfly(cospi[32], cospi[32], buffer0[6], buffer0[7], ref buffer1, 6, 7, cosBit, in rounding);

            // Stage 3 combines the rotated and copied pairs into two independent four-value groups.
            for (int group = 0; group < 8; group += 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    Av1ForwardTransformArithmetic<int>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 2],
                        out buffer0[group + i],
                        out buffer0[group + i + 2]);
                }
            }

            // Stage 4 rotates the upper group by pi/8 while the completed lower group passes through unchanged.
            for (int i = 0; i < 4; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[4] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[16], buffer0[4], cospi[48], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[48], buffer0[4], -cospi[16], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<int>.HalfButterfly(-cospi[48], buffer0[6], cospi[16], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[16], buffer0[6], cospi[48], buffer0[7], cosBit, in rounding);

            // Stage 5 creates the four final butterfly pairs spanning the two groups.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[i], buffer1[i + 4], out buffer0[i], out buffer0[i + 4]);
            }

            // Stage 6 applies the remaining odd-angle rotations.
            buffer1[0] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[4], buffer0[0], cospi[60], buffer0[1], cosBit, in rounding);
            buffer1[1] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[60], buffer0[0], -cospi[4], buffer0[1], cosBit, in rounding);
            buffer1[2] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[20], buffer0[2], cospi[44], buffer0[3], cosBit, in rounding);
            buffer1[3] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[44], buffer0[2], -cospi[20], buffer0[3], cosBit, in rounding);
            buffer1[4] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[36], buffer0[4], cospi[28], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[28], buffer0[4], -cospi[36], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[52], buffer0[6], cospi[12], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[12], buffer0[6], -cospi[52], buffer0[7], cosBit, in rounding);

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 7 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 8; i++)
            {
                Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
            }
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

            // Stage 1 applies the ADST permutation and signs while the source block is still read-only.
            buffer0[0] = Load<short>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 7));
            buffer0[2] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 3));
            buffer0[3] = Load<short>(ref values, inputStride, 4);
            buffer0[4] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 1));
            buffer0[5] = Load<short>(ref values, inputStride, 6);
            buffer0[6] = Load<short>(ref values, inputStride, 2);
            buffer0[7] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 5));

            // Stage 2 rotates the second pair in each four-value group while copying the already aligned pairs.
            buffer1[0] = buffer0[0];
            buffer1[1] = buffer0[1];
            Butterfly(cospi[32], cospi[32], buffer0[2], buffer0[3], ref buffer1, 2, 3, cosBit, in rounding);
            buffer1[4] = buffer0[4];
            buffer1[5] = buffer0[5];
            Butterfly(cospi[32], cospi[32], buffer0[6], buffer0[7], ref buffer1, 6, 7, cosBit, in rounding);

            // Stage 3 combines the rotated and copied pairs into two independent four-value groups.
            for (int group = 0; group < 8; group += 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    Av1ForwardTransformArithmetic<short>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 2],
                        out buffer0[group + i],
                        out buffer0[group + i + 2]);
                }
            }

            // Stage 4 rotates the upper group by pi/8 while the completed lower group passes through unchanged.
            for (int i = 0; i < 4; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[4] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[16], buffer0[4], cospi[48], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[48], buffer0[4], -cospi[16], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<short>.HalfButterfly(-cospi[48], buffer0[6], cospi[16], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[16], buffer0[6], cospi[48], buffer0[7], cosBit, in rounding);

            // Stage 5 creates the four final butterfly pairs spanning the two groups.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[i], buffer1[i + 4], out buffer0[i], out buffer0[i + 4]);
            }

            // Stage 6 applies the remaining odd-angle rotations.
            buffer1[0] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[4], buffer0[0], cospi[60], buffer0[1], cosBit, in rounding);
            buffer1[1] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[60], buffer0[0], -cospi[4], buffer0[1], cosBit, in rounding);
            buffer1[2] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[20], buffer0[2], cospi[44], buffer0[3], cosBit, in rounding);
            buffer1[3] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[44], buffer0[2], -cospi[20], buffer0[3], cosBit, in rounding);
            buffer1[4] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[36], buffer0[4], cospi[28], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[28], buffer0[4], -cospi[36], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[52], buffer0[6], cospi[12], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[12], buffer0[6], -cospi[52], buffer0[7], cosBit, in rounding);

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 7 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 8; i++)
            {
                Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
            }
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

            // Stage 1 applies the ADST permutation and signs while the source block is still read-only.
            buffer0[0] = Load<Vector128<short>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 7));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 3));
            buffer0[3] = Load<Vector128<short>>(ref values, inputStride, 4);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 1));
            buffer0[5] = Load<Vector128<short>>(ref values, inputStride, 6);
            buffer0[6] = Load<Vector128<short>>(ref values, inputStride, 2);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 5));

            // Stage 2 rotates the second pair in each four-value group while copying the already aligned pairs.
            buffer1[0] = buffer0[0];
            buffer1[1] = buffer0[1];
            Butterfly(cospi[32], cospi[32], buffer0[2], buffer0[3], ref buffer1, 2, 3, cosBit, in rounding);
            buffer1[4] = buffer0[4];
            buffer1[5] = buffer0[5];
            Butterfly(cospi[32], cospi[32], buffer0[6], buffer0[7], ref buffer1, 6, 7, cosBit, in rounding);

            // Stage 3 combines the rotated and copied pairs into two independent four-value groups.
            for (int group = 0; group < 8; group += 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 2],
                        out buffer0[group + i],
                        out buffer0[group + i + 2]);
                }
            }

            // Stage 4 rotates the upper group by pi/8 while the completed lower group passes through unchanged.
            for (int i = 0; i < 4; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[4] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[16], buffer0[4], cospi[48], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[48], buffer0[4], -cospi[16], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(-cospi[48], buffer0[6], cospi[16], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[16], buffer0[6], cospi[48], buffer0[7], cosBit, in rounding);

            // Stage 5 creates the four final butterfly pairs spanning the two groups.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[i], buffer1[i + 4], out buffer0[i], out buffer0[i + 4]);
            }

            // Stage 6 applies the remaining odd-angle rotations.
            buffer1[0] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[4], buffer0[0], cospi[60], buffer0[1], cosBit, in rounding);
            buffer1[1] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[60], buffer0[0], -cospi[4], buffer0[1], cosBit, in rounding);
            buffer1[2] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[20], buffer0[2], cospi[44], buffer0[3], cosBit, in rounding);
            buffer1[3] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[44], buffer0[2], -cospi[20], buffer0[3], cosBit, in rounding);
            buffer1[4] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[36], buffer0[4], cospi[28], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[28], buffer0[4], -cospi[36], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[52], buffer0[6], cospi[12], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[12], buffer0[6], -cospi[52], buffer0[7], cosBit, in rounding);

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 7 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 8; i++)
            {
                Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
            }
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

            // Stage 1 applies the ADST permutation and signs while the source block is still read-only.
            buffer0[0] = Load<Vector256<short>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 7));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 3));
            buffer0[3] = Load<Vector256<short>>(ref values, inputStride, 4);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 1));
            buffer0[5] = Load<Vector256<short>>(ref values, inputStride, 6);
            buffer0[6] = Load<Vector256<short>>(ref values, inputStride, 2);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 5));

            // Stage 2 rotates the second pair in each four-value group while copying the already aligned pairs.
            buffer1[0] = buffer0[0];
            buffer1[1] = buffer0[1];
            Butterfly(cospi[32], cospi[32], buffer0[2], buffer0[3], ref buffer1, 2, 3, cosBit, in rounding);
            buffer1[4] = buffer0[4];
            buffer1[5] = buffer0[5];
            Butterfly(cospi[32], cospi[32], buffer0[6], buffer0[7], ref buffer1, 6, 7, cosBit, in rounding);

            // Stage 3 combines the rotated and copied pairs into two independent four-value groups.
            for (int group = 0; group < 8; group += 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 2],
                        out buffer0[group + i],
                        out buffer0[group + i + 2]);
                }
            }

            // Stage 4 rotates the upper group by pi/8 while the completed lower group passes through unchanged.
            for (int i = 0; i < 4; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[4] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[16], buffer0[4], cospi[48], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[48], buffer0[4], -cospi[16], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(-cospi[48], buffer0[6], cospi[16], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[16], buffer0[6], cospi[48], buffer0[7], cosBit, in rounding);

            // Stage 5 creates the four final butterfly pairs spanning the two groups.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[i], buffer1[i + 4], out buffer0[i], out buffer0[i + 4]);
            }

            // Stage 6 applies the remaining odd-angle rotations.
            buffer1[0] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[4], buffer0[0], cospi[60], buffer0[1], cosBit, in rounding);
            buffer1[1] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[60], buffer0[0], -cospi[4], buffer0[1], cosBit, in rounding);
            buffer1[2] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[20], buffer0[2], cospi[44], buffer0[3], cosBit, in rounding);
            buffer1[3] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[44], buffer0[2], -cospi[20], buffer0[3], cosBit, in rounding);
            buffer1[4] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[36], buffer0[4], cospi[28], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[28], buffer0[4], -cospi[36], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[52], buffer0[6], cospi[12], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[12], buffer0[6], -cospi[52], buffer0[7], cosBit, in rounding);

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 7 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 8; i++)
            {
                Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
            }
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

            // Stage 1 applies the ADST permutation and signs while the source block is still read-only.
            buffer0[0] = Load<Vector512<short>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 7));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 3));
            buffer0[3] = Load<Vector512<short>>(ref values, inputStride, 4);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 1));
            buffer0[5] = Load<Vector512<short>>(ref values, inputStride, 6);
            buffer0[6] = Load<Vector512<short>>(ref values, inputStride, 2);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 5));

            // Stage 2 rotates the second pair in each four-value group while copying the already aligned pairs.
            buffer1[0] = buffer0[0];
            buffer1[1] = buffer0[1];
            Butterfly(cospi[32], cospi[32], buffer0[2], buffer0[3], ref buffer1, 2, 3, cosBit, in rounding);
            buffer1[4] = buffer0[4];
            buffer1[5] = buffer0[5];
            Butterfly(cospi[32], cospi[32], buffer0[6], buffer0[7], ref buffer1, 6, 7, cosBit, in rounding);

            // Stage 3 combines the rotated and copied pairs into two independent four-value groups.
            for (int group = 0; group < 8; group += 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 2],
                        out buffer0[group + i],
                        out buffer0[group + i + 2]);
                }
            }

            // Stage 4 rotates the upper group by pi/8 while the completed lower group passes through unchanged.
            for (int i = 0; i < 4; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[4] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[16], buffer0[4], cospi[48], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[48], buffer0[4], -cospi[16], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(-cospi[48], buffer0[6], cospi[16], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[16], buffer0[6], cospi[48], buffer0[7], cosBit, in rounding);

            // Stage 5 creates the four final butterfly pairs spanning the two groups.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[i], buffer1[i + 4], out buffer0[i], out buffer0[i + 4]);
            }

            // Stage 6 applies the remaining odd-angle rotations.
            buffer1[0] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[4], buffer0[0], cospi[60], buffer0[1], cosBit, in rounding);
            buffer1[1] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[60], buffer0[0], -cospi[4], buffer0[1], cosBit, in rounding);
            buffer1[2] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[20], buffer0[2], cospi[44], buffer0[3], cosBit, in rounding);
            buffer1[3] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[44], buffer0[2], -cospi[20], buffer0[3], cosBit, in rounding);
            buffer1[4] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[36], buffer0[4], cospi[28], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[28], buffer0[4], -cospi[36], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[52], buffer0[6], cospi[12], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[12], buffer0[6], -cospi[52], buffer0[7], cosBit, in rounding);

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 7 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 8; i++)
            {
                Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
            }
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

            // Stage 1 applies the ADST permutation and signs while the source block is still read-only.
            buffer0[0] = Load<Vector128<int>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 7));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 3));
            buffer0[3] = Load<Vector128<int>>(ref values, inputStride, 4);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 1));
            buffer0[5] = Load<Vector128<int>>(ref values, inputStride, 6);
            buffer0[6] = Load<Vector128<int>>(ref values, inputStride, 2);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 5));

            // Stage 2 rotates the second pair in each four-value group while copying the already aligned pairs.
            buffer1[0] = buffer0[0];
            buffer1[1] = buffer0[1];
            Butterfly(cospi[32], cospi[32], buffer0[2], buffer0[3], ref buffer1, 2, 3, cosBit, in rounding);
            buffer1[4] = buffer0[4];
            buffer1[5] = buffer0[5];
            Butterfly(cospi[32], cospi[32], buffer0[6], buffer0[7], ref buffer1, 6, 7, cosBit, in rounding);

            // Stage 3 combines the rotated and copied pairs into two independent four-value groups.
            for (int group = 0; group < 8; group += 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 2],
                        out buffer0[group + i],
                        out buffer0[group + i + 2]);
                }
            }

            // Stage 4 rotates the upper group by pi/8 while the completed lower group passes through unchanged.
            for (int i = 0; i < 4; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[4] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[16], buffer0[4], cospi[48], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[48], buffer0[4], -cospi[16], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(-cospi[48], buffer0[6], cospi[16], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[16], buffer0[6], cospi[48], buffer0[7], cosBit, in rounding);

            // Stage 5 creates the four final butterfly pairs spanning the two groups.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[i], buffer1[i + 4], out buffer0[i], out buffer0[i + 4]);
            }

            // Stage 6 applies the remaining odd-angle rotations.
            buffer1[0] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[4], buffer0[0], cospi[60], buffer0[1], cosBit, in rounding);
            buffer1[1] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[60], buffer0[0], -cospi[4], buffer0[1], cosBit, in rounding);
            buffer1[2] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[20], buffer0[2], cospi[44], buffer0[3], cosBit, in rounding);
            buffer1[3] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[44], buffer0[2], -cospi[20], buffer0[3], cosBit, in rounding);
            buffer1[4] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[36], buffer0[4], cospi[28], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[28], buffer0[4], -cospi[36], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[52], buffer0[6], cospi[12], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[12], buffer0[6], -cospi[52], buffer0[7], cosBit, in rounding);

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 7 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 8; i++)
            {
                Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
            }
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

            // Stage 1 applies the ADST permutation and signs while the source block is still read-only.
            buffer0[0] = Load<Vector256<int>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 7));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 3));
            buffer0[3] = Load<Vector256<int>>(ref values, inputStride, 4);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 1));
            buffer0[5] = Load<Vector256<int>>(ref values, inputStride, 6);
            buffer0[6] = Load<Vector256<int>>(ref values, inputStride, 2);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 5));

            // Stage 2 rotates the second pair in each four-value group while copying the already aligned pairs.
            buffer1[0] = buffer0[0];
            buffer1[1] = buffer0[1];
            Butterfly(cospi[32], cospi[32], buffer0[2], buffer0[3], ref buffer1, 2, 3, cosBit, in rounding);
            buffer1[4] = buffer0[4];
            buffer1[5] = buffer0[5];
            Butterfly(cospi[32], cospi[32], buffer0[6], buffer0[7], ref buffer1, 6, 7, cosBit, in rounding);

            // Stage 3 combines the rotated and copied pairs into two independent four-value groups.
            for (int group = 0; group < 8; group += 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 2],
                        out buffer0[group + i],
                        out buffer0[group + i + 2]);
                }
            }

            // Stage 4 rotates the upper group by pi/8 while the completed lower group passes through unchanged.
            for (int i = 0; i < 4; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[4] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[16], buffer0[4], cospi[48], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[48], buffer0[4], -cospi[16], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(-cospi[48], buffer0[6], cospi[16], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[16], buffer0[6], cospi[48], buffer0[7], cosBit, in rounding);

            // Stage 5 creates the four final butterfly pairs spanning the two groups.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[i], buffer1[i + 4], out buffer0[i], out buffer0[i + 4]);
            }

            // Stage 6 applies the remaining odd-angle rotations.
            buffer1[0] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[4], buffer0[0], cospi[60], buffer0[1], cosBit, in rounding);
            buffer1[1] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[60], buffer0[0], -cospi[4], buffer0[1], cosBit, in rounding);
            buffer1[2] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[20], buffer0[2], cospi[44], buffer0[3], cosBit, in rounding);
            buffer1[3] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[44], buffer0[2], -cospi[20], buffer0[3], cosBit, in rounding);
            buffer1[4] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[36], buffer0[4], cospi[28], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[28], buffer0[4], -cospi[36], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[52], buffer0[6], cospi[12], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[12], buffer0[6], -cospi[52], buffer0[7], cosBit, in rounding);

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 7 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 8; i++)
            {
                Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
            }
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

            // Stage 1 applies the ADST permutation and signs while the source block is still read-only.
            buffer0[0] = Load<Vector512<int>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 7));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 3));
            buffer0[3] = Load<Vector512<int>>(ref values, inputStride, 4);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 1));
            buffer0[5] = Load<Vector512<int>>(ref values, inputStride, 6);
            buffer0[6] = Load<Vector512<int>>(ref values, inputStride, 2);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 5));

            // Stage 2 rotates the second pair in each four-value group while copying the already aligned pairs.
            buffer1[0] = buffer0[0];
            buffer1[1] = buffer0[1];
            Butterfly(cospi[32], cospi[32], buffer0[2], buffer0[3], ref buffer1, 2, 3, cosBit, in rounding);
            buffer1[4] = buffer0[4];
            buffer1[5] = buffer0[5];
            Butterfly(cospi[32], cospi[32], buffer0[6], buffer0[7], ref buffer1, 6, 7, cosBit, in rounding);

            // Stage 3 combines the rotated and copied pairs into two independent four-value groups.
            for (int group = 0; group < 8; group += 4)
            {
                for (int i = 0; i < 2; i++)
                {
                    Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 2],
                        out buffer0[group + i],
                        out buffer0[group + i + 2]);
                }
            }

            // Stage 4 rotates the upper group by pi/8 while the completed lower group passes through unchanged.
            for (int i = 0; i < 4; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[4] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[16], buffer0[4], cospi[48], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[48], buffer0[4], -cospi[16], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(-cospi[48], buffer0[6], cospi[16], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[16], buffer0[6], cospi[48], buffer0[7], cosBit, in rounding);

            // Stage 5 creates the four final butterfly pairs spanning the two groups.
            for (int i = 0; i < 4; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[i], buffer1[i + 4], out buffer0[i], out buffer0[i + 4]);
            }

            // Stage 6 applies the remaining odd-angle rotations.
            buffer1[0] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[4], buffer0[0], cospi[60], buffer0[1], cosBit, in rounding);
            buffer1[1] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[60], buffer0[0], -cospi[4], buffer0[1], cosBit, in rounding);
            buffer1[2] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[20], buffer0[2], cospi[44], buffer0[3], cosBit, in rounding);
            buffer1[3] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[44], buffer0[2], -cospi[20], buffer0[3], cosBit, in rounding);
            buffer1[4] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[36], buffer0[4], cospi[28], buffer0[5], cosBit, in rounding);
            buffer1[5] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[28], buffer0[4], -cospi[36], buffer0[5], cosBit, in rounding);
            buffer1[6] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[52], buffer0[6], cospi[12], buffer0[7], cosBit, in rounding);
            buffer1[7] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[12], buffer0[6], -cospi[52], buffer0[7], cosBit, in rounding);

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 7 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 8; i++)
            {
                Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
            }
        }
    }
}
