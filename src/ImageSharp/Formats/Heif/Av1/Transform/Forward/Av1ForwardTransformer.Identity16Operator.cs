// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Defines the sixteen-point forward identity operator.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Implements the sixteen-point forward identity transform.
    /// </summary>
    internal readonly struct Identity16Operator : IAv1ForwardTransform1dOperator
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
            _ = cosBit;

            // The length-specific normalization is applied directly in the semantic operator so each scalar
            // or SIMD overload retains the exact AV1 identity-transform arithmetic without a forwarding layer.
            for (int i = 0; i < 16; i++)
            {
                int input = Load<int>(ref values, inputStride, i);
                int output = Av1ForwardTransformArithmetic<int>.MultiplyRound(
                    input,
                    2 * Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits);

                Store(ref values, outputStride, i, output);
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
            _ = buffer0;
            _ = buffer1;
            _ = cosBit;

            // The length-specific normalization is applied directly in the semantic operator so each scalar
            // or SIMD overload retains the exact AV1 identity-transform arithmetic without a forwarding layer.
            for (int i = 0; i < 16; i++)
            {
                short input = Load<short>(ref values, inputStride, i);
                short output = Av1ForwardTransformArithmetic<short>.MultiplyRound(
                    input,
                    2 * Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits);

                Store(ref values, outputStride, i, output);
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
            _ = buffer0;
            _ = buffer1;
            _ = cosBit;

            // The length-specific normalization is applied directly in the semantic operator so each scalar
            // or SIMD overload retains the exact AV1 identity-transform arithmetic without a forwarding layer.
            for (int i = 0; i < 16; i++)
            {
                Vector128<short> input = Load<Vector128<short>>(ref values, inputStride, i);
                Vector128<short> output = Av1ForwardTransformArithmetic<Vector128<short>>.MultiplyRound(
                    input,
                    2 * Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits);

                Store(ref values, outputStride, i, output);
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
            _ = buffer0;
            _ = buffer1;
            _ = cosBit;

            // The length-specific normalization is applied directly in the semantic operator so each scalar
            // or SIMD overload retains the exact AV1 identity-transform arithmetic without a forwarding layer.
            for (int i = 0; i < 16; i++)
            {
                Vector256<short> input = Load<Vector256<short>>(ref values, inputStride, i);
                Vector256<short> output = Av1ForwardTransformArithmetic<Vector256<short>>.MultiplyRound(
                    input,
                    2 * Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits);

                Store(ref values, outputStride, i, output);
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
            _ = buffer0;
            _ = buffer1;
            _ = cosBit;

            // The length-specific normalization is applied directly in the semantic operator so each scalar
            // or SIMD overload retains the exact AV1 identity-transform arithmetic without a forwarding layer.
            for (int i = 0; i < 16; i++)
            {
                Vector512<short> input = Load<Vector512<short>>(ref values, inputStride, i);
                Vector512<short> output = Av1ForwardTransformArithmetic<Vector512<short>>.MultiplyRound(
                    input,
                    2 * Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits);

                Store(ref values, outputStride, i, output);
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
            _ = buffer0;
            _ = buffer1;
            _ = cosBit;

            // The length-specific normalization is applied directly in the semantic operator so each scalar
            // or SIMD overload retains the exact AV1 identity-transform arithmetic without a forwarding layer.
            for (int i = 0; i < 16; i++)
            {
                Vector128<int> input = Load<Vector128<int>>(ref values, inputStride, i);
                Vector128<int> output = Av1ForwardTransformArithmetic<Vector128<int>>.MultiplyRound(
                    input,
                    2 * Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits);

                Store(ref values, outputStride, i, output);
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
            _ = buffer0;
            _ = buffer1;
            _ = cosBit;

            // The length-specific normalization is applied directly in the semantic operator so each scalar
            // or SIMD overload retains the exact AV1 identity-transform arithmetic without a forwarding layer.
            for (int i = 0; i < 16; i++)
            {
                Vector256<int> input = Load<Vector256<int>>(ref values, inputStride, i);
                Vector256<int> output = Av1ForwardTransformArithmetic<Vector256<int>>.MultiplyRound(
                    input,
                    2 * Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits);

                Store(ref values, outputStride, i, output);
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
            _ = buffer0;
            _ = buffer1;
            _ = cosBit;

            // The length-specific normalization is applied directly in the semantic operator so each scalar
            // or SIMD overload retains the exact AV1 identity-transform arithmetic without a forwarding layer.
            for (int i = 0; i < 16; i++)
            {
                Vector512<int> input = Load<Vector512<int>>(ref values, inputStride, i);
                Vector512<int> output = Av1ForwardTransformArithmetic<Vector512<int>>.MultiplyRound(
                    input,
                    2 * Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits);

                Store(ref values, outputStride, i, output);
            }
        }
    }
}
