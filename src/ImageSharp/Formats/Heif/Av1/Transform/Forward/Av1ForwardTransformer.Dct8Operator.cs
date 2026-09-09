// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Implements the eight-point forward DCT stage network.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Implements the eight-point forward transform for every supported lane width.
    /// </summary>
    internal readonly struct Dct8Operator : IAv1ForwardTransform1dOperator
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

            // Stages 1 and 2 split the even and odd terms. The asymmetric destinations mirror Highway's buffer
            // ownership, allowing the later even butterflies to write their final coefficients directly to the block.
            Av1ForwardTransformArithmetic<int>.AddSubtract(
                Load<int>(ref values, inputStride, 0),
                Load<int>(ref values, inputStride, 7),
                out buffer0[0],
                out buffer1[7]);

            Av1ForwardTransformArithmetic<int>.AddSubtract(
                Load<int>(ref values, inputStride, 1),
                Load<int>(ref values, inputStride, 6),
                out buffer0[1],
                out buffer0[6]);

            Av1ForwardTransformArithmetic<int>.AddSubtract(
                Load<int>(ref values, inputStride, 2),
                Load<int>(ref values, inputStride, 5),
                out buffer0[2],
                out buffer0[5]);

            Av1ForwardTransformArithmetic<int>.AddSubtract(
                Load<int>(ref values, inputStride, 3),
                Load<int>(ref values, inputStride, 4),
                out buffer0[3],
                out buffer1[4]);

            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[0], buffer0[3], out buffer1[0], out buffer1[3]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer0[1], buffer0[2], out buffer1[1], out buffer1[2]);
            Av1ForwardTransformArithmetic<int>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[5],
                buffer0[6],
                out buffer1[5],
                out buffer1[6],
                cosBit,
                in rounding);

            // Stage 3 completes the even half directly in coefficient order and prepares the four remaining odd terms.
            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[32],
                cospi[32],
                buffer1[0],
                buffer1[1],
                out int output0,
                out int output4,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[16],
                cospi[48],
                buffer1[3],
                buffer1[2],
                out int output2,
                out int output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[4], buffer1[5], out buffer0[4], out buffer0[5]);
            Av1ForwardTransformArithmetic<int>.AddSubtract(buffer1[7], buffer1[6], out buffer0[7], out buffer0[6]);

            // Highway fuses the final two stages because no intermediate value is reused after either rotation.
            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[8],
                cospi[56],
                buffer0[7],
                buffer0[4],
                out int output1,
                out int output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<int>.Butterfly(
                cospi[40],
                cospi[24],
                buffer0[6],
                buffer0[5],
                out int output5,
                out int output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
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

            // Stages 1 and 2 split the even and odd terms. The asymmetric destinations mirror Highway's buffer
            // ownership, allowing the later even butterflies to write their final coefficients directly to the block.
            Av1ForwardTransformArithmetic<short>.AddSubtract(
                Load<short>(ref values, inputStride, 0),
                Load<short>(ref values, inputStride, 7),
                out buffer0[0],
                out buffer1[7]);

            Av1ForwardTransformArithmetic<short>.AddSubtract(
                Load<short>(ref values, inputStride, 1),
                Load<short>(ref values, inputStride, 6),
                out buffer0[1],
                out buffer0[6]);

            Av1ForwardTransformArithmetic<short>.AddSubtract(
                Load<short>(ref values, inputStride, 2),
                Load<short>(ref values, inputStride, 5),
                out buffer0[2],
                out buffer0[5]);

            Av1ForwardTransformArithmetic<short>.AddSubtract(
                Load<short>(ref values, inputStride, 3),
                Load<short>(ref values, inputStride, 4),
                out buffer0[3],
                out buffer1[4]);

            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[0], buffer0[3], out buffer1[0], out buffer1[3]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer0[1], buffer0[2], out buffer1[1], out buffer1[2]);
            Av1ForwardTransformArithmetic<short>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[5],
                buffer0[6],
                out buffer1[5],
                out buffer1[6],
                cosBit,
                in rounding);

            // Stage 3 completes the even half directly in coefficient order and prepares the four remaining odd terms.
            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[32],
                cospi[32],
                buffer1[0],
                buffer1[1],
                out short output0,
                out short output4,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[16],
                cospi[48],
                buffer1[3],
                buffer1[2],
                out short output2,
                out short output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[4], buffer1[5], out buffer0[4], out buffer0[5]);
            Av1ForwardTransformArithmetic<short>.AddSubtract(buffer1[7], buffer1[6], out buffer0[7], out buffer0[6]);

            // Highway fuses the final two stages because no intermediate value is reused after either rotation.
            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[8],
                cospi[56],
                buffer0[7],
                buffer0[4],
                out short output1,
                out short output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<short>.Butterfly(
                cospi[40],
                cospi[24],
                buffer0[6],
                buffer0[5],
                out short output5,
                out short output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
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

            // Stages 1 and 2 split the even and odd terms. The asymmetric destinations mirror Highway's buffer
            // ownership, allowing the later even butterflies to write their final coefficients directly to the block.
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(
                Load<Vector128<short>>(ref values, inputStride, 0),
                Load<Vector128<short>>(ref values, inputStride, 7),
                out buffer0[0],
                out buffer1[7]);

            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(
                Load<Vector128<short>>(ref values, inputStride, 1),
                Load<Vector128<short>>(ref values, inputStride, 6),
                out buffer0[1],
                out buffer0[6]);

            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(
                Load<Vector128<short>>(ref values, inputStride, 2),
                Load<Vector128<short>>(ref values, inputStride, 5),
                out buffer0[2],
                out buffer0[5]);

            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(
                Load<Vector128<short>>(ref values, inputStride, 3),
                Load<Vector128<short>>(ref values, inputStride, 4),
                out buffer0[3],
                out buffer1[4]);

            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[0], buffer0[3], out buffer1[0], out buffer1[3]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer0[1], buffer0[2], out buffer1[1], out buffer1[2]);
            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[5],
                buffer0[6],
                out buffer1[5],
                out buffer1[6],
                cosBit,
                in rounding);

            // Stage 3 completes the even half directly in coefficient order and prepares the four remaining odd terms.
            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer1[0],
                buffer1[1],
                out Vector128<short> output0,
                out Vector128<short> output4,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer1[3],
                buffer1[2],
                out Vector128<short> output2,
                out Vector128<short> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[4], buffer1[5], out buffer0[4], out buffer0[5]);
            Av1ForwardTransformArithmetic<Vector128<short>>.AddSubtract(buffer1[7], buffer1[6], out buffer0[7], out buffer0[6]);

            // Highway fuses the final two stages because no intermediate value is reused after either rotation.
            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer0[7],
                buffer0[4],
                out Vector128<short> output1,
                out Vector128<short> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<short>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer0[6],
                buffer0[5],
                out Vector128<short> output5,
                out Vector128<short> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
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

            // Stages 1 and 2 split the even and odd terms. The asymmetric destinations mirror Highway's buffer
            // ownership, allowing the later even butterflies to write their final coefficients directly to the block.
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(
                Load<Vector256<short>>(ref values, inputStride, 0),
                Load<Vector256<short>>(ref values, inputStride, 7),
                out buffer0[0],
                out buffer1[7]);

            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(
                Load<Vector256<short>>(ref values, inputStride, 1),
                Load<Vector256<short>>(ref values, inputStride, 6),
                out buffer0[1],
                out buffer0[6]);

            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(
                Load<Vector256<short>>(ref values, inputStride, 2),
                Load<Vector256<short>>(ref values, inputStride, 5),
                out buffer0[2],
                out buffer0[5]);

            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(
                Load<Vector256<short>>(ref values, inputStride, 3),
                Load<Vector256<short>>(ref values, inputStride, 4),
                out buffer0[3],
                out buffer1[4]);

            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[0], buffer0[3], out buffer1[0], out buffer1[3]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer0[1], buffer0[2], out buffer1[1], out buffer1[2]);
            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[5],
                buffer0[6],
                out buffer1[5],
                out buffer1[6],
                cosBit,
                in rounding);

            // Stage 3 completes the even half directly in coefficient order and prepares the four remaining odd terms.
            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer1[0],
                buffer1[1],
                out Vector256<short> output0,
                out Vector256<short> output4,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer1[3],
                buffer1[2],
                out Vector256<short> output2,
                out Vector256<short> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[4], buffer1[5], out buffer0[4], out buffer0[5]);
            Av1ForwardTransformArithmetic<Vector256<short>>.AddSubtract(buffer1[7], buffer1[6], out buffer0[7], out buffer0[6]);

            // Highway fuses the final two stages because no intermediate value is reused after either rotation.
            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer0[7],
                buffer0[4],
                out Vector256<short> output1,
                out Vector256<short> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<short>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer0[6],
                buffer0[5],
                out Vector256<short> output5,
                out Vector256<short> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
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

            // Stages 1 and 2 split the even and odd terms. The asymmetric destinations mirror Highway's buffer
            // ownership, allowing the later even butterflies to write their final coefficients directly to the block.
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(
                Load<Vector512<short>>(ref values, inputStride, 0),
                Load<Vector512<short>>(ref values, inputStride, 7),
                out buffer0[0],
                out buffer1[7]);

            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(
                Load<Vector512<short>>(ref values, inputStride, 1),
                Load<Vector512<short>>(ref values, inputStride, 6),
                out buffer0[1],
                out buffer0[6]);

            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(
                Load<Vector512<short>>(ref values, inputStride, 2),
                Load<Vector512<short>>(ref values, inputStride, 5),
                out buffer0[2],
                out buffer0[5]);

            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(
                Load<Vector512<short>>(ref values, inputStride, 3),
                Load<Vector512<short>>(ref values, inputStride, 4),
                out buffer0[3],
                out buffer1[4]);

            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[0], buffer0[3], out buffer1[0], out buffer1[3]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer0[1], buffer0[2], out buffer1[1], out buffer1[2]);
            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[5],
                buffer0[6],
                out buffer1[5],
                out buffer1[6],
                cosBit,
                in rounding);

            // Stage 3 completes the even half directly in coefficient order and prepares the four remaining odd terms.
            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer1[0],
                buffer1[1],
                out Vector512<short> output0,
                out Vector512<short> output4,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer1[3],
                buffer1[2],
                out Vector512<short> output2,
                out Vector512<short> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[4], buffer1[5], out buffer0[4], out buffer0[5]);
            Av1ForwardTransformArithmetic<Vector512<short>>.AddSubtract(buffer1[7], buffer1[6], out buffer0[7], out buffer0[6]);

            // Highway fuses the final two stages because no intermediate value is reused after either rotation.
            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer0[7],
                buffer0[4],
                out Vector512<short> output1,
                out Vector512<short> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<short>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer0[6],
                buffer0[5],
                out Vector512<short> output5,
                out Vector512<short> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
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

            // Stages 1 and 2 split the even and odd terms. The asymmetric destinations mirror Highway's buffer
            // ownership, allowing the later even butterflies to write their final coefficients directly to the block.
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(
                Load<Vector128<int>>(ref values, inputStride, 0),
                Load<Vector128<int>>(ref values, inputStride, 7),
                out buffer0[0],
                out buffer1[7]);

            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(
                Load<Vector128<int>>(ref values, inputStride, 1),
                Load<Vector128<int>>(ref values, inputStride, 6),
                out buffer0[1],
                out buffer0[6]);

            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(
                Load<Vector128<int>>(ref values, inputStride, 2),
                Load<Vector128<int>>(ref values, inputStride, 5),
                out buffer0[2],
                out buffer0[5]);

            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(
                Load<Vector128<int>>(ref values, inputStride, 3),
                Load<Vector128<int>>(ref values, inputStride, 4),
                out buffer0[3],
                out buffer1[4]);

            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[0], buffer0[3], out buffer1[0], out buffer1[3]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer0[1], buffer0[2], out buffer1[1], out buffer1[2]);
            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[5],
                buffer0[6],
                out buffer1[5],
                out buffer1[6],
                cosBit,
                in rounding);

            // Stage 3 completes the even half directly in coefficient order and prepares the four remaining odd terms.
            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer1[0],
                buffer1[1],
                out Vector128<int> output0,
                out Vector128<int> output4,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer1[3],
                buffer1[2],
                out Vector128<int> output2,
                out Vector128<int> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[4], buffer1[5], out buffer0[4], out buffer0[5]);
            Av1ForwardTransformArithmetic<Vector128<int>>.AddSubtract(buffer1[7], buffer1[6], out buffer0[7], out buffer0[6]);

            // Highway fuses the final two stages because no intermediate value is reused after either rotation.
            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer0[7],
                buffer0[4],
                out Vector128<int> output1,
                out Vector128<int> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector128<int>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer0[6],
                buffer0[5],
                out Vector128<int> output5,
                out Vector128<int> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
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

            // Stages 1 and 2 split the even and odd terms. The asymmetric destinations mirror Highway's buffer
            // ownership, allowing the later even butterflies to write their final coefficients directly to the block.
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(
                Load<Vector256<int>>(ref values, inputStride, 0),
                Load<Vector256<int>>(ref values, inputStride, 7),
                out buffer0[0],
                out buffer1[7]);

            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(
                Load<Vector256<int>>(ref values, inputStride, 1),
                Load<Vector256<int>>(ref values, inputStride, 6),
                out buffer0[1],
                out buffer0[6]);

            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(
                Load<Vector256<int>>(ref values, inputStride, 2),
                Load<Vector256<int>>(ref values, inputStride, 5),
                out buffer0[2],
                out buffer0[5]);

            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(
                Load<Vector256<int>>(ref values, inputStride, 3),
                Load<Vector256<int>>(ref values, inputStride, 4),
                out buffer0[3],
                out buffer1[4]);

            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[0], buffer0[3], out buffer1[0], out buffer1[3]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer0[1], buffer0[2], out buffer1[1], out buffer1[2]);
            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[5],
                buffer0[6],
                out buffer1[5],
                out buffer1[6],
                cosBit,
                in rounding);

            // Stage 3 completes the even half directly in coefficient order and prepares the four remaining odd terms.
            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer1[0],
                buffer1[1],
                out Vector256<int> output0,
                out Vector256<int> output4,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer1[3],
                buffer1[2],
                out Vector256<int> output2,
                out Vector256<int> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[4], buffer1[5], out buffer0[4], out buffer0[5]);
            Av1ForwardTransformArithmetic<Vector256<int>>.AddSubtract(buffer1[7], buffer1[6], out buffer0[7], out buffer0[6]);

            // Highway fuses the final two stages because no intermediate value is reused after either rotation.
            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer0[7],
                buffer0[4],
                out Vector256<int> output1,
                out Vector256<int> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector256<int>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer0[6],
                buffer0[5],
                out Vector256<int> output5,
                out Vector256<int> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
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

            // Stages 1 and 2 split the even and odd terms. The asymmetric destinations mirror Highway's buffer
            // ownership, allowing the later even butterflies to write their final coefficients directly to the block.
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(
                Load<Vector512<int>>(ref values, inputStride, 0),
                Load<Vector512<int>>(ref values, inputStride, 7),
                out buffer0[0],
                out buffer1[7]);

            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(
                Load<Vector512<int>>(ref values, inputStride, 1),
                Load<Vector512<int>>(ref values, inputStride, 6),
                out buffer0[1],
                out buffer0[6]);

            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(
                Load<Vector512<int>>(ref values, inputStride, 2),
                Load<Vector512<int>>(ref values, inputStride, 5),
                out buffer0[2],
                out buffer0[5]);

            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(
                Load<Vector512<int>>(ref values, inputStride, 3),
                Load<Vector512<int>>(ref values, inputStride, 4),
                out buffer0[3],
                out buffer1[4]);

            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[0], buffer0[3], out buffer1[0], out buffer1[3]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer0[1], buffer0[2], out buffer1[1], out buffer1[2]);
            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                -cospi[32],
                cospi[32],
                buffer0[5],
                buffer0[6],
                out buffer1[5],
                out buffer1[6],
                cosBit,
                in rounding);

            // Stage 3 completes the even half directly in coefficient order and prepares the four remaining odd terms.
            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[32],
                cospi[32],
                buffer1[0],
                buffer1[1],
                out Vector512<int> output0,
                out Vector512<int> output4,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[16],
                cospi[48],
                buffer1[3],
                buffer1[2],
                out Vector512<int> output2,
                out Vector512<int> output6,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[4], buffer1[5], out buffer0[4], out buffer0[5]);
            Av1ForwardTransformArithmetic<Vector512<int>>.AddSubtract(buffer1[7], buffer1[6], out buffer0[7], out buffer0[6]);

            // Highway fuses the final two stages because no intermediate value is reused after either rotation.
            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[8],
                cospi[56],
                buffer0[7],
                buffer0[4],
                out Vector512<int> output1,
                out Vector512<int> output7,
                cosBit,
                in rounding);

            Av1ForwardTransformArithmetic<Vector512<int>>.Butterfly(
                cospi[40],
                cospi[24],
                buffer0[6],
                buffer0[5],
                out Vector512<int> output5,
                out Vector512<int> output3,
                cosBit,
                in rounding);

            Store(ref values, outputStride, 0, output0);
            Store(ref values, outputStride, 1, output1);
            Store(ref values, outputStride, 2, output2);
            Store(ref values, outputStride, 3, output3);
            Store(ref values, outputStride, 4, output4);
            Store(ref values, outputStride, 5, output5);
            Store(ref values, outputStride, 6, output6);
            Store(ref values, outputStride, 7, output7);
        }
    }
}
