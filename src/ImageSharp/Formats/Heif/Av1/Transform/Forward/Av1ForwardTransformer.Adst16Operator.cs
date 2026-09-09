// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines the sixteen-point forward ADST operator.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Implements the sixteen-point forward asymmetric discrete sine transform.
    /// </summary>
    internal readonly struct Adst16Operator : IAv1ForwardTransform1dOperator
    {
        /// <summary>
        /// Gets the first cosine index for each final rotation.
        /// </summary>
        private static ReadOnlySpan<byte> FinalWeights => [2, 10, 18, 26, 34, 42, 50, 58];

        /// <summary>
        /// Gets the fixed coefficient permutation.
        /// </summary>
        private static ReadOnlySpan<byte> OutputOrder => [1, 14, 3, 12, 5, 10, 7, 8, 9, 6, 11, 4, 13, 2, 15, 0];

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

            // Stage 1 is the bit-reversed ADST input order with the normative alternating signs.
            buffer0[0] = Load<int>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 15));
            buffer0[2] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 7));
            buffer0[3] = Load<int>(ref values, inputStride, 8);
            buffer0[4] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 3));
            buffer0[5] = Load<int>(ref values, inputStride, 12);
            buffer0[6] = Load<int>(ref values, inputStride, 4);
            buffer0[7] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 11));
            buffer0[8] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 1));
            buffer0[9] = Load<int>(ref values, inputStride, 14);
            buffer0[10] = Load<int>(ref values, inputStride, 6);
            buffer0[11] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 9));
            buffer0[12] = Load<int>(ref values, inputStride, 2);
            buffer0[13] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 13));
            buffer0[14] = Av1ForwardTransformArithmetic<int>.Negate(Load<int>(ref values, inputStride, 5));
            buffer0[15] = Load<int>(ref values, inputStride, 10);

            // Stage 2 rotates the second pair in each group of four while copying the first pair unchanged.
            for (int group = 0; group < 16; group += 4)
            {
                buffer1[group] = buffer0[group];
                buffer1[group + 1] = buffer0[group + 1];
                Butterfly(cospi[32], cospi[32], buffer0[group + 2], buffer0[group + 3], ref buffer1, group + 2, group + 3, cosBit, in rounding);
            }

            // Stage 3 combines adjacent pairs within each group of four.
            for (int group = 0; group < 16; group += 4)
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

            // Stage 4 rotates the upper pair of each eight-value group by pi/8.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    buffer1[group + i] = buffer0[group + i];
                }

                buffer1[group + 4] = Av1ForwardTransformArithmetic<int>.HalfButterfly(
                    cospi[16], buffer0[group + 4], cospi[48], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 5] = Av1ForwardTransformArithmetic<int>.HalfButterfly(
                    cospi[48], buffer0[group + 4], -cospi[16], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 6] = Av1ForwardTransformArithmetic<int>.HalfButterfly(
                    -cospi[48], buffer0[group + 6], cospi[16], buffer0[group + 7], cosBit, in rounding);

                buffer1[group + 7] = Av1ForwardTransformArithmetic<int>.HalfButterfly(
                    cospi[16], buffer0[group + 6], cospi[48], buffer0[group + 7], cosBit, in rounding);
            }

            // Stage 5 combines the lower and upper quartets within each eight-value group.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    Av1ForwardTransformArithmetic<int>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 4],
                        out buffer0[group + i],
                        out buffer0[group + i + 4]);
                }
            }

            // Stage 6 rotates the upper octet by pi/16 while retaining the completed lower octet.
            for (int i = 0; i < 8; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[8] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[8], buffer0[8], cospi[56], buffer0[9], cosBit, in rounding);
            buffer1[9] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[56], buffer0[8], -cospi[8], buffer0[9], cosBit, in rounding);
            buffer1[10] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[40], buffer0[10], cospi[24], buffer0[11], cosBit, in rounding);
            buffer1[11] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[24], buffer0[10], -cospi[40], buffer0[11], cosBit, in rounding);
            buffer1[12] = Av1ForwardTransformArithmetic<int>.HalfButterfly(-cospi[56], buffer0[12], cospi[8], buffer0[13], cosBit, in rounding);
            buffer1[13] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[8], buffer0[12], cospi[56], buffer0[13], cosBit, in rounding);
            buffer1[14] = Av1ForwardTransformArithmetic<int>.HalfButterfly(-cospi[24], buffer0[14], cospi[40], buffer0[15], cosBit, in rounding);
            buffer1[15] = Av1ForwardTransformArithmetic<int>.HalfButterfly(cospi[40], buffer0[14], cospi[24], buffer0[15], cosBit, in rounding);

            // Stage 7 creates the eight final butterfly pairs spanning both octets.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[i], buffer1[i + 8], out buffer0[i], out buffer0[i + 8]);
            }

            ReadOnlySpan<byte> finalWeights = FinalWeights;

            // Stage 8 applies the final odd-angle rotations. The compact weight table preserves their normative order
            // without allocating a per-call array or duplicating the complementary cosine-index calculation.
            for (int pair = 0; pair < 8; pair++)
            {
                int first = finalWeights[pair];
                int second = 64 - first;
                int index = pair * 2;
                buffer1[index] = Av1ForwardTransformArithmetic<int>.HalfButterfly(
                    cospi[first], buffer0[index], cospi[second], buffer0[index + 1], cosBit, in rounding);

                buffer1[index + 1] = Av1ForwardTransformArithmetic<int>.HalfButterfly(
                    cospi[second], buffer0[index], -cospi[first], buffer0[index + 1], cosBit, in rounding);
            }

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 9 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 16; i++)
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

            // Stage 1 is the bit-reversed ADST input order with the normative alternating signs.
            buffer0[0] = Load<short>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 15));
            buffer0[2] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 7));
            buffer0[3] = Load<short>(ref values, inputStride, 8);
            buffer0[4] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 3));
            buffer0[5] = Load<short>(ref values, inputStride, 12);
            buffer0[6] = Load<short>(ref values, inputStride, 4);
            buffer0[7] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 11));
            buffer0[8] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 1));
            buffer0[9] = Load<short>(ref values, inputStride, 14);
            buffer0[10] = Load<short>(ref values, inputStride, 6);
            buffer0[11] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 9));
            buffer0[12] = Load<short>(ref values, inputStride, 2);
            buffer0[13] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 13));
            buffer0[14] = Av1ForwardTransformArithmetic<short>.Negate(Load<short>(ref values, inputStride, 5));
            buffer0[15] = Load<short>(ref values, inputStride, 10);

            // Stage 2 rotates the second pair in each group of four while copying the first pair unchanged.
            for (int group = 0; group < 16; group += 4)
            {
                buffer1[group] = buffer0[group];
                buffer1[group + 1] = buffer0[group + 1];
                Butterfly(cospi[32], cospi[32], buffer0[group + 2], buffer0[group + 3], ref buffer1, group + 2, group + 3, cosBit, in rounding);
            }

            // Stage 3 combines adjacent pairs within each group of four.
            for (int group = 0; group < 16; group += 4)
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

            // Stage 4 rotates the upper pair of each eight-value group by pi/8.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    buffer1[group + i] = buffer0[group + i];
                }

                buffer1[group + 4] = Av1ForwardTransformArithmetic<short>.HalfButterfly(
                    cospi[16], buffer0[group + 4], cospi[48], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 5] = Av1ForwardTransformArithmetic<short>.HalfButterfly(
                    cospi[48], buffer0[group + 4], -cospi[16], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 6] = Av1ForwardTransformArithmetic<short>.HalfButterfly(
                    -cospi[48], buffer0[group + 6], cospi[16], buffer0[group + 7], cosBit, in rounding);

                buffer1[group + 7] = Av1ForwardTransformArithmetic<short>.HalfButterfly(
                    cospi[16], buffer0[group + 6], cospi[48], buffer0[group + 7], cosBit, in rounding);
            }

            // Stage 5 combines the lower and upper quartets within each eight-value group.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    Av1ForwardTransformArithmetic<short>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 4],
                        out buffer0[group + i],
                        out buffer0[group + i + 4]);
                }
            }

            // Stage 6 rotates the upper octet by pi/16 while retaining the completed lower octet.
            for (int i = 0; i < 8; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[8] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[8], buffer0[8], cospi[56], buffer0[9], cosBit, in rounding);
            buffer1[9] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[56], buffer0[8], -cospi[8], buffer0[9], cosBit, in rounding);
            buffer1[10] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[40], buffer0[10], cospi[24], buffer0[11], cosBit, in rounding);
            buffer1[11] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[24], buffer0[10], -cospi[40], buffer0[11], cosBit, in rounding);
            buffer1[12] = Av1ForwardTransformArithmetic<short>.HalfButterfly(-cospi[56], buffer0[12], cospi[8], buffer0[13], cosBit, in rounding);
            buffer1[13] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[8], buffer0[12], cospi[56], buffer0[13], cosBit, in rounding);
            buffer1[14] = Av1ForwardTransformArithmetic<short>.HalfButterfly(-cospi[24], buffer0[14], cospi[40], buffer0[15], cosBit, in rounding);
            buffer1[15] = Av1ForwardTransformArithmetic<short>.HalfButterfly(cospi[40], buffer0[14], cospi[24], buffer0[15], cosBit, in rounding);

            // Stage 7 creates the eight final butterfly pairs spanning both octets.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[i], buffer1[i + 8], out buffer0[i], out buffer0[i + 8]);
            }

            ReadOnlySpan<byte> finalWeights = FinalWeights;

            // Stage 8 applies the final odd-angle rotations. The compact weight table preserves their normative order
            // without allocating a per-call array or duplicating the complementary cosine-index calculation.
            for (int pair = 0; pair < 8; pair++)
            {
                int first = finalWeights[pair];
                int second = 64 - first;
                int index = pair * 2;
                buffer1[index] = Av1ForwardTransformArithmetic<short>.HalfButterfly(
                    cospi[first], buffer0[index], cospi[second], buffer0[index + 1], cosBit, in rounding);

                buffer1[index + 1] = Av1ForwardTransformArithmetic<short>.HalfButterfly(
                    cospi[second], buffer0[index], -cospi[first], buffer0[index + 1], cosBit, in rounding);
            }

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 9 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 16; i++)
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

            // Stage 1 is the bit-reversed ADST input order with the normative alternating signs.
            buffer0[0] = Load<Vector128<short>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 15));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 7));
            buffer0[3] = Load<Vector128<short>>(ref values, inputStride, 8);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 3));
            buffer0[5] = Load<Vector128<short>>(ref values, inputStride, 12);
            buffer0[6] = Load<Vector128<short>>(ref values, inputStride, 4);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 11));
            buffer0[8] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 1));
            buffer0[9] = Load<Vector128<short>>(ref values, inputStride, 14);
            buffer0[10] = Load<Vector128<short>>(ref values, inputStride, 6);
            buffer0[11] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 9));
            buffer0[12] = Load<Vector128<short>>(ref values, inputStride, 2);
            buffer0[13] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 13));
            buffer0[14] = Av1ForwardTransformArithmetic<Vector128<short>>.Negate(Load<Vector128<short>>(ref values, inputStride, 5));
            buffer0[15] = Load<Vector128<short>>(ref values, inputStride, 10);

            // Stage 2 rotates the second pair in each group of four while copying the first pair unchanged.
            for (int group = 0; group < 16; group += 4)
            {
                buffer1[group] = buffer0[group];
                buffer1[group + 1] = buffer0[group + 1];
                Butterfly(cospi[32], cospi[32], buffer0[group + 2], buffer0[group + 3], ref buffer1, group + 2, group + 3, cosBit, in rounding);
            }

            // Stage 3 combines adjacent pairs within each group of four.
            for (int group = 0; group < 16; group += 4)
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

            // Stage 4 rotates the upper pair of each eight-value group by pi/8.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    buffer1[group + i] = buffer0[group + i];
                }

                buffer1[group + 4] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(
                    cospi[16], buffer0[group + 4], cospi[48], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 5] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(
                    cospi[48], buffer0[group + 4], -cospi[16], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 6] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(
                    -cospi[48], buffer0[group + 6], cospi[16], buffer0[group + 7], cosBit, in rounding);

                buffer1[group + 7] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(
                    cospi[16], buffer0[group + 6], cospi[48], buffer0[group + 7], cosBit, in rounding);
            }

            // Stage 5 combines the lower and upper quartets within each eight-value group.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 4],
                        out buffer0[group + i],
                        out buffer0[group + i + 4]);
                }
            }

            // Stage 6 rotates the upper octet by pi/16 while retaining the completed lower octet.
            for (int i = 0; i < 8; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[8] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[8], buffer0[8], cospi[56], buffer0[9], cosBit, in rounding);
            buffer1[9] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[56], buffer0[8], -cospi[8], buffer0[9], cosBit, in rounding);
            buffer1[10] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[40], buffer0[10], cospi[24], buffer0[11], cosBit, in rounding);
            buffer1[11] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[24], buffer0[10], -cospi[40], buffer0[11], cosBit, in rounding);
            buffer1[12] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(-cospi[56], buffer0[12], cospi[8], buffer0[13], cosBit, in rounding);
            buffer1[13] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[8], buffer0[12], cospi[56], buffer0[13], cosBit, in rounding);
            buffer1[14] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(-cospi[24], buffer0[14], cospi[40], buffer0[15], cosBit, in rounding);
            buffer1[15] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(cospi[40], buffer0[14], cospi[24], buffer0[15], cosBit, in rounding);

            // Stage 7 creates the eight final butterfly pairs spanning both octets.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[i], buffer1[i + 8], out buffer0[i], out buffer0[i + 8]);
            }

            ReadOnlySpan<byte> finalWeights = FinalWeights;

            // Stage 8 applies the final odd-angle rotations. The compact weight table preserves their normative order
            // without allocating a per-call array or duplicating the complementary cosine-index calculation.
            for (int pair = 0; pair < 8; pair++)
            {
                int first = finalWeights[pair];
                int second = 64 - first;
                int index = pair * 2;
                buffer1[index] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(
                    cospi[first], buffer0[index], cospi[second], buffer0[index + 1], cosBit, in rounding);

                buffer1[index + 1] = Av1ForwardTransformArithmetic<Vector128<short>>.HalfButterfly(
                    cospi[second], buffer0[index], -cospi[first], buffer0[index + 1], cosBit, in rounding);
            }

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 9 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 16; i++)
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

            // Stage 1 is the bit-reversed ADST input order with the normative alternating signs.
            buffer0[0] = Load<Vector256<short>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 15));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 7));
            buffer0[3] = Load<Vector256<short>>(ref values, inputStride, 8);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 3));
            buffer0[5] = Load<Vector256<short>>(ref values, inputStride, 12);
            buffer0[6] = Load<Vector256<short>>(ref values, inputStride, 4);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 11));
            buffer0[8] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 1));
            buffer0[9] = Load<Vector256<short>>(ref values, inputStride, 14);
            buffer0[10] = Load<Vector256<short>>(ref values, inputStride, 6);
            buffer0[11] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 9));
            buffer0[12] = Load<Vector256<short>>(ref values, inputStride, 2);
            buffer0[13] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 13));
            buffer0[14] = Av1ForwardTransformArithmetic<Vector256<short>>.Negate(Load<Vector256<short>>(ref values, inputStride, 5));
            buffer0[15] = Load<Vector256<short>>(ref values, inputStride, 10);

            // Stage 2 rotates the second pair in each group of four while copying the first pair unchanged.
            for (int group = 0; group < 16; group += 4)
            {
                buffer1[group] = buffer0[group];
                buffer1[group + 1] = buffer0[group + 1];
                Butterfly(cospi[32], cospi[32], buffer0[group + 2], buffer0[group + 3], ref buffer1, group + 2, group + 3, cosBit, in rounding);
            }

            // Stage 3 combines adjacent pairs within each group of four.
            for (int group = 0; group < 16; group += 4)
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

            // Stage 4 rotates the upper pair of each eight-value group by pi/8.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    buffer1[group + i] = buffer0[group + i];
                }

                buffer1[group + 4] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(
                    cospi[16], buffer0[group + 4], cospi[48], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 5] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(
                    cospi[48], buffer0[group + 4], -cospi[16], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 6] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(
                    -cospi[48], buffer0[group + 6], cospi[16], buffer0[group + 7], cosBit, in rounding);

                buffer1[group + 7] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(
                    cospi[16], buffer0[group + 6], cospi[48], buffer0[group + 7], cosBit, in rounding);
            }

            // Stage 5 combines the lower and upper quartets within each eight-value group.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 4],
                        out buffer0[group + i],
                        out buffer0[group + i + 4]);
                }
            }

            // Stage 6 rotates the upper octet by pi/16 while retaining the completed lower octet.
            for (int i = 0; i < 8; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[8] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[8], buffer0[8], cospi[56], buffer0[9], cosBit, in rounding);
            buffer1[9] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[56], buffer0[8], -cospi[8], buffer0[9], cosBit, in rounding);
            buffer1[10] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[40], buffer0[10], cospi[24], buffer0[11], cosBit, in rounding);
            buffer1[11] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[24], buffer0[10], -cospi[40], buffer0[11], cosBit, in rounding);
            buffer1[12] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(-cospi[56], buffer0[12], cospi[8], buffer0[13], cosBit, in rounding);
            buffer1[13] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[8], buffer0[12], cospi[56], buffer0[13], cosBit, in rounding);
            buffer1[14] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(-cospi[24], buffer0[14], cospi[40], buffer0[15], cosBit, in rounding);
            buffer1[15] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(cospi[40], buffer0[14], cospi[24], buffer0[15], cosBit, in rounding);

            // Stage 7 creates the eight final butterfly pairs spanning both octets.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[i], buffer1[i + 8], out buffer0[i], out buffer0[i + 8]);
            }

            ReadOnlySpan<byte> finalWeights = FinalWeights;

            // Stage 8 applies the final odd-angle rotations. The compact weight table preserves their normative order
            // without allocating a per-call array or duplicating the complementary cosine-index calculation.
            for (int pair = 0; pair < 8; pair++)
            {
                int first = finalWeights[pair];
                int second = 64 - first;
                int index = pair * 2;
                buffer1[index] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(
                    cospi[first], buffer0[index], cospi[second], buffer0[index + 1], cosBit, in rounding);

                buffer1[index + 1] = Av1ForwardTransformArithmetic<Vector256<short>>.HalfButterfly(
                    cospi[second], buffer0[index], -cospi[first], buffer0[index + 1], cosBit, in rounding);
            }

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 9 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 16; i++)
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

            // Stage 1 is the bit-reversed ADST input order with the normative alternating signs.
            buffer0[0] = Load<Vector512<short>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 15));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 7));
            buffer0[3] = Load<Vector512<short>>(ref values, inputStride, 8);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 3));
            buffer0[5] = Load<Vector512<short>>(ref values, inputStride, 12);
            buffer0[6] = Load<Vector512<short>>(ref values, inputStride, 4);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 11));
            buffer0[8] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 1));
            buffer0[9] = Load<Vector512<short>>(ref values, inputStride, 14);
            buffer0[10] = Load<Vector512<short>>(ref values, inputStride, 6);
            buffer0[11] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 9));
            buffer0[12] = Load<Vector512<short>>(ref values, inputStride, 2);
            buffer0[13] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 13));
            buffer0[14] = Av1ForwardTransformArithmetic<Vector512<short>>.Negate(Load<Vector512<short>>(ref values, inputStride, 5));
            buffer0[15] = Load<Vector512<short>>(ref values, inputStride, 10);

            // Stage 2 rotates the second pair in each group of four while copying the first pair unchanged.
            for (int group = 0; group < 16; group += 4)
            {
                buffer1[group] = buffer0[group];
                buffer1[group + 1] = buffer0[group + 1];
                Butterfly(cospi[32], cospi[32], buffer0[group + 2], buffer0[group + 3], ref buffer1, group + 2, group + 3, cosBit, in rounding);
            }

            // Stage 3 combines adjacent pairs within each group of four.
            for (int group = 0; group < 16; group += 4)
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

            // Stage 4 rotates the upper pair of each eight-value group by pi/8.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    buffer1[group + i] = buffer0[group + i];
                }

                buffer1[group + 4] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(
                    cospi[16], buffer0[group + 4], cospi[48], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 5] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(
                    cospi[48], buffer0[group + 4], -cospi[16], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 6] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(
                    -cospi[48], buffer0[group + 6], cospi[16], buffer0[group + 7], cosBit, in rounding);

                buffer1[group + 7] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(
                    cospi[16], buffer0[group + 6], cospi[48], buffer0[group + 7], cosBit, in rounding);
            }

            // Stage 5 combines the lower and upper quartets within each eight-value group.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 4],
                        out buffer0[group + i],
                        out buffer0[group + i + 4]);
                }
            }

            // Stage 6 rotates the upper octet by pi/16 while retaining the completed lower octet.
            for (int i = 0; i < 8; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[8] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[8], buffer0[8], cospi[56], buffer0[9], cosBit, in rounding);
            buffer1[9] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[56], buffer0[8], -cospi[8], buffer0[9], cosBit, in rounding);
            buffer1[10] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[40], buffer0[10], cospi[24], buffer0[11], cosBit, in rounding);
            buffer1[11] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[24], buffer0[10], -cospi[40], buffer0[11], cosBit, in rounding);
            buffer1[12] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(-cospi[56], buffer0[12], cospi[8], buffer0[13], cosBit, in rounding);
            buffer1[13] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[8], buffer0[12], cospi[56], buffer0[13], cosBit, in rounding);
            buffer1[14] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(-cospi[24], buffer0[14], cospi[40], buffer0[15], cosBit, in rounding);
            buffer1[15] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(cospi[40], buffer0[14], cospi[24], buffer0[15], cosBit, in rounding);

            // Stage 7 creates the eight final butterfly pairs spanning both octets.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[i], buffer1[i + 8], out buffer0[i], out buffer0[i + 8]);
            }

            ReadOnlySpan<byte> finalWeights = FinalWeights;

            // Stage 8 applies the final odd-angle rotations. The compact weight table preserves their normative order
            // without allocating a per-call array or duplicating the complementary cosine-index calculation.
            for (int pair = 0; pair < 8; pair++)
            {
                int first = finalWeights[pair];
                int second = 64 - first;
                int index = pair * 2;
                buffer1[index] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(
                    cospi[first], buffer0[index], cospi[second], buffer0[index + 1], cosBit, in rounding);

                buffer1[index + 1] = Av1ForwardTransformArithmetic<Vector512<short>>.HalfButterfly(
                    cospi[second], buffer0[index], -cospi[first], buffer0[index + 1], cosBit, in rounding);
            }

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 9 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 16; i++)
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

            // Stage 1 is the bit-reversed ADST input order with the normative alternating signs.
            buffer0[0] = Load<Vector128<int>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 15));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 7));
            buffer0[3] = Load<Vector128<int>>(ref values, inputStride, 8);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 3));
            buffer0[5] = Load<Vector128<int>>(ref values, inputStride, 12);
            buffer0[6] = Load<Vector128<int>>(ref values, inputStride, 4);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 11));
            buffer0[8] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 1));
            buffer0[9] = Load<Vector128<int>>(ref values, inputStride, 14);
            buffer0[10] = Load<Vector128<int>>(ref values, inputStride, 6);
            buffer0[11] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 9));
            buffer0[12] = Load<Vector128<int>>(ref values, inputStride, 2);
            buffer0[13] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 13));
            buffer0[14] = Av1ForwardTransformArithmetic<Vector128<int>>.Negate(Load<Vector128<int>>(ref values, inputStride, 5));
            buffer0[15] = Load<Vector128<int>>(ref values, inputStride, 10);

            // Stage 2 rotates the second pair in each group of four while copying the first pair unchanged.
            for (int group = 0; group < 16; group += 4)
            {
                buffer1[group] = buffer0[group];
                buffer1[group + 1] = buffer0[group + 1];
                Butterfly(cospi[32], cospi[32], buffer0[group + 2], buffer0[group + 3], ref buffer1, group + 2, group + 3, cosBit, in rounding);
            }

            // Stage 3 combines adjacent pairs within each group of four.
            for (int group = 0; group < 16; group += 4)
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

            // Stage 4 rotates the upper pair of each eight-value group by pi/8.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    buffer1[group + i] = buffer0[group + i];
                }

                buffer1[group + 4] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(
                    cospi[16], buffer0[group + 4], cospi[48], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 5] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(
                    cospi[48], buffer0[group + 4], -cospi[16], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 6] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(
                    -cospi[48], buffer0[group + 6], cospi[16], buffer0[group + 7], cosBit, in rounding);

                buffer1[group + 7] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(
                    cospi[16], buffer0[group + 6], cospi[48], buffer0[group + 7], cosBit, in rounding);
            }

            // Stage 5 combines the lower and upper quartets within each eight-value group.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 4],
                        out buffer0[group + i],
                        out buffer0[group + i + 4]);
                }
            }

            // Stage 6 rotates the upper octet by pi/16 while retaining the completed lower octet.
            for (int i = 0; i < 8; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[8] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[8], buffer0[8], cospi[56], buffer0[9], cosBit, in rounding);
            buffer1[9] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[56], buffer0[8], -cospi[8], buffer0[9], cosBit, in rounding);
            buffer1[10] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[40], buffer0[10], cospi[24], buffer0[11], cosBit, in rounding);
            buffer1[11] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[24], buffer0[10], -cospi[40], buffer0[11], cosBit, in rounding);
            buffer1[12] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(-cospi[56], buffer0[12], cospi[8], buffer0[13], cosBit, in rounding);
            buffer1[13] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[8], buffer0[12], cospi[56], buffer0[13], cosBit, in rounding);
            buffer1[14] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(-cospi[24], buffer0[14], cospi[40], buffer0[15], cosBit, in rounding);
            buffer1[15] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(cospi[40], buffer0[14], cospi[24], buffer0[15], cosBit, in rounding);

            // Stage 7 creates the eight final butterfly pairs spanning both octets.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[i], buffer1[i + 8], out buffer0[i], out buffer0[i + 8]);
            }

            ReadOnlySpan<byte> finalWeights = FinalWeights;

            // Stage 8 applies the final odd-angle rotations. The compact weight table preserves their normative order
            // without allocating a per-call array or duplicating the complementary cosine-index calculation.
            for (int pair = 0; pair < 8; pair++)
            {
                int first = finalWeights[pair];
                int second = 64 - first;
                int index = pair * 2;
                buffer1[index] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(
                    cospi[first], buffer0[index], cospi[second], buffer0[index + 1], cosBit, in rounding);

                buffer1[index + 1] = Av1ForwardTransformArithmetic<Vector128<int>>.HalfButterfly(
                    cospi[second], buffer0[index], -cospi[first], buffer0[index + 1], cosBit, in rounding);
            }

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 9 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 16; i++)
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

            // Stage 1 is the bit-reversed ADST input order with the normative alternating signs.
            buffer0[0] = Load<Vector256<int>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 15));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 7));
            buffer0[3] = Load<Vector256<int>>(ref values, inputStride, 8);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 3));
            buffer0[5] = Load<Vector256<int>>(ref values, inputStride, 12);
            buffer0[6] = Load<Vector256<int>>(ref values, inputStride, 4);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 11));
            buffer0[8] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 1));
            buffer0[9] = Load<Vector256<int>>(ref values, inputStride, 14);
            buffer0[10] = Load<Vector256<int>>(ref values, inputStride, 6);
            buffer0[11] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 9));
            buffer0[12] = Load<Vector256<int>>(ref values, inputStride, 2);
            buffer0[13] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 13));
            buffer0[14] = Av1ForwardTransformArithmetic<Vector256<int>>.Negate(Load<Vector256<int>>(ref values, inputStride, 5));
            buffer0[15] = Load<Vector256<int>>(ref values, inputStride, 10);

            // Stage 2 rotates the second pair in each group of four while copying the first pair unchanged.
            for (int group = 0; group < 16; group += 4)
            {
                buffer1[group] = buffer0[group];
                buffer1[group + 1] = buffer0[group + 1];
                Butterfly(cospi[32], cospi[32], buffer0[group + 2], buffer0[group + 3], ref buffer1, group + 2, group + 3, cosBit, in rounding);
            }

            // Stage 3 combines adjacent pairs within each group of four.
            for (int group = 0; group < 16; group += 4)
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

            // Stage 4 rotates the upper pair of each eight-value group by pi/8.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    buffer1[group + i] = buffer0[group + i];
                }

                buffer1[group + 4] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(
                    cospi[16], buffer0[group + 4], cospi[48], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 5] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(
                    cospi[48], buffer0[group + 4], -cospi[16], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 6] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(
                    -cospi[48], buffer0[group + 6], cospi[16], buffer0[group + 7], cosBit, in rounding);

                buffer1[group + 7] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(
                    cospi[16], buffer0[group + 6], cospi[48], buffer0[group + 7], cosBit, in rounding);
            }

            // Stage 5 combines the lower and upper quartets within each eight-value group.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 4],
                        out buffer0[group + i],
                        out buffer0[group + i + 4]);
                }
            }

            // Stage 6 rotates the upper octet by pi/16 while retaining the completed lower octet.
            for (int i = 0; i < 8; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[8] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[8], buffer0[8], cospi[56], buffer0[9], cosBit, in rounding);
            buffer1[9] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[56], buffer0[8], -cospi[8], buffer0[9], cosBit, in rounding);
            buffer1[10] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[40], buffer0[10], cospi[24], buffer0[11], cosBit, in rounding);
            buffer1[11] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[24], buffer0[10], -cospi[40], buffer0[11], cosBit, in rounding);
            buffer1[12] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(-cospi[56], buffer0[12], cospi[8], buffer0[13], cosBit, in rounding);
            buffer1[13] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[8], buffer0[12], cospi[56], buffer0[13], cosBit, in rounding);
            buffer1[14] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(-cospi[24], buffer0[14], cospi[40], buffer0[15], cosBit, in rounding);
            buffer1[15] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(cospi[40], buffer0[14], cospi[24], buffer0[15], cosBit, in rounding);

            // Stage 7 creates the eight final butterfly pairs spanning both octets.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[i], buffer1[i + 8], out buffer0[i], out buffer0[i + 8]);
            }

            ReadOnlySpan<byte> finalWeights = FinalWeights;

            // Stage 8 applies the final odd-angle rotations. The compact weight table preserves their normative order
            // without allocating a per-call array or duplicating the complementary cosine-index calculation.
            for (int pair = 0; pair < 8; pair++)
            {
                int first = finalWeights[pair];
                int second = 64 - first;
                int index = pair * 2;
                buffer1[index] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(
                    cospi[first], buffer0[index], cospi[second], buffer0[index + 1], cosBit, in rounding);

                buffer1[index + 1] = Av1ForwardTransformArithmetic<Vector256<int>>.HalfButterfly(
                    cospi[second], buffer0[index], -cospi[first], buffer0[index + 1], cosBit, in rounding);
            }

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 9 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 16; i++)
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

            // Stage 1 is the bit-reversed ADST input order with the normative alternating signs.
            buffer0[0] = Load<Vector512<int>>(ref values, inputStride, 0);
            buffer0[1] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 15));
            buffer0[2] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 7));
            buffer0[3] = Load<Vector512<int>>(ref values, inputStride, 8);
            buffer0[4] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 3));
            buffer0[5] = Load<Vector512<int>>(ref values, inputStride, 12);
            buffer0[6] = Load<Vector512<int>>(ref values, inputStride, 4);
            buffer0[7] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 11));
            buffer0[8] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 1));
            buffer0[9] = Load<Vector512<int>>(ref values, inputStride, 14);
            buffer0[10] = Load<Vector512<int>>(ref values, inputStride, 6);
            buffer0[11] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 9));
            buffer0[12] = Load<Vector512<int>>(ref values, inputStride, 2);
            buffer0[13] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 13));
            buffer0[14] = Av1ForwardTransformArithmetic<Vector512<int>>.Negate(Load<Vector512<int>>(ref values, inputStride, 5));
            buffer0[15] = Load<Vector512<int>>(ref values, inputStride, 10);

            // Stage 2 rotates the second pair in each group of four while copying the first pair unchanged.
            for (int group = 0; group < 16; group += 4)
            {
                buffer1[group] = buffer0[group];
                buffer1[group + 1] = buffer0[group + 1];
                Butterfly(cospi[32], cospi[32], buffer0[group + 2], buffer0[group + 3], ref buffer1, group + 2, group + 3, cosBit, in rounding);
            }

            // Stage 3 combines adjacent pairs within each group of four.
            for (int group = 0; group < 16; group += 4)
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

            // Stage 4 rotates the upper pair of each eight-value group by pi/8.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    buffer1[group + i] = buffer0[group + i];
                }

                buffer1[group + 4] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(
                    cospi[16], buffer0[group + 4], cospi[48], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 5] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(
                    cospi[48], buffer0[group + 4], -cospi[16], buffer0[group + 5], cosBit, in rounding);

                buffer1[group + 6] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(
                    -cospi[48], buffer0[group + 6], cospi[16], buffer0[group + 7], cosBit, in rounding);

                buffer1[group + 7] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(
                    cospi[16], buffer0[group + 6], cospi[48], buffer0[group + 7], cosBit, in rounding);
            }

            // Stage 5 combines the lower and upper quartets within each eight-value group.
            for (int group = 0; group < 16; group += 8)
            {
                for (int i = 0; i < 4; i++)
                {
                    Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(
                        buffer1[group + i],
                        buffer1[group + i + 4],
                        out buffer0[group + i],
                        out buffer0[group + i + 4]);
                }
            }

            // Stage 6 rotates the upper octet by pi/16 while retaining the completed lower octet.
            for (int i = 0; i < 8; i++)
            {
                buffer1[i] = buffer0[i];
            }

            buffer1[8] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[8], buffer0[8], cospi[56], buffer0[9], cosBit, in rounding);
            buffer1[9] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[56], buffer0[8], -cospi[8], buffer0[9], cosBit, in rounding);
            buffer1[10] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[40], buffer0[10], cospi[24], buffer0[11], cosBit, in rounding);
            buffer1[11] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[24], buffer0[10], -cospi[40], buffer0[11], cosBit, in rounding);
            buffer1[12] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(-cospi[56], buffer0[12], cospi[8], buffer0[13], cosBit, in rounding);
            buffer1[13] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[8], buffer0[12], cospi[56], buffer0[13], cosBit, in rounding);
            buffer1[14] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(-cospi[24], buffer0[14], cospi[40], buffer0[15], cosBit, in rounding);
            buffer1[15] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(cospi[40], buffer0[14], cospi[24], buffer0[15], cosBit, in rounding);

            // Stage 7 creates the eight final butterfly pairs spanning both octets.
            for (int i = 0; i < 8; i++)
            {
                Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[i], buffer1[i + 8], out buffer0[i], out buffer0[i + 8]);
            }

            ReadOnlySpan<byte> finalWeights = FinalWeights;

            // Stage 8 applies the final odd-angle rotations. The compact weight table preserves their normative order
            // without allocating a per-call array or duplicating the complementary cosine-index calculation.
            for (int pair = 0; pair < 8; pair++)
            {
                int first = finalWeights[pair];
                int second = 64 - first;
                int index = pair * 2;
                buffer1[index] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(
                    cospi[first], buffer0[index], cospi[second], buffer0[index + 1], cosBit, in rounding);

                buffer1[index + 1] = Av1ForwardTransformArithmetic<Vector512<int>>.HalfButterfly(
                    cospi[second], buffer0[index], -cospi[first], buffer0[index + 1], cosBit, in rounding);
            }

            ReadOnlySpan<byte> outputOrder = OutputOrder;

            // Stage 9 maps the rotated values to ascending AV1 ADST coefficient order.
            for (int i = 0; i < 16; i++)
            {
                Store(ref values, outputStride, i, buffer1[outputOrder[i]]);
            }
        }
    }
}
