// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Builds signed AV1 residual planes and measures sample-domain error.
/// </summary>
internal static partial class Av1ResidualBuilder
{
    /// <summary>
    /// Subtracts an 8-bit prediction plane from its source plane.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction samples.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="residual">The destination residual samples.</param>
    /// <param name="residualStride">The residual row stride.</param>
    /// <param name="width">The number of samples per row.</param>
    /// <param name="height">The number of rows.</param>
    public static void Subtract(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<short> residual,
        int residualStride,
        int width,
        int height)
        => Subtract<byte, ByteOperator>(source, sourceStride, prediction, predictionStride, residual, residualStride, width, height);

    /// <summary>
    /// Subtracts a high-bit-depth prediction plane from its source plane.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction samples.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="residual">The destination residual samples.</param>
    /// <param name="residualStride">The residual row stride.</param>
    /// <param name="width">The number of samples per row.</param>
    /// <param name="height">The number of rows.</param>
    public static void Subtract(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        ReadOnlySpan<ushort> prediction,
        int predictionStride,
        Span<short> residual,
        int residualStride,
        int width,
        int height)
        => Subtract<ushort, UInt16Operator>(source, sourceStride, prediction, predictionStride, residual, residualStride, width, height);

    /// <summary>
    /// Calculates the exact squared error between strided 8-bit sample planes.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction or reconstruction samples.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="width">The number of samples per row.</param>
    /// <param name="height">The number of rows.</param>
    /// <returns>The sum of squared sample differences.</returns>
    public static long SumSquaredError(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        int width,
        int height)
        => SumSquaredError<byte, ByteOperator>(source, sourceStride, prediction, predictionStride, width, height);

    /// <summary>
    /// Calculates the exact squared error between strided high-bit-depth sample planes.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction or reconstruction samples.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="width">The number of samples per row.</param>
    /// <param name="height">The number of rows.</param>
    /// <returns>The sum of squared sample differences.</returns>
    public static long SumSquaredError(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        ReadOnlySpan<ushort> prediction,
        int predictionStride,
        int width,
        int height)
        => SumSquaredError<ushort, UInt16Operator>(source, sourceStride, prediction, predictionStride, width, height);

    /// <summary>
    /// Sums the squares of a contiguous signed residual block.
    /// </summary>
    /// <param name="residual">The residual samples.</param>
    /// <returns>The exact sum of squared sample differences.</returns>
    public static long SumSquares(ReadOnlySpan<short> residual)
    {
        ref short residualBase = ref MemoryMarshal.GetReference(residual);
        long sum = 0;
        int offset = 0;

        // Each short lane widens before multiplication, preserving the full 12-bit residual square.
        // The accumulated scalar is 64-bit because a complete encoder block can exceed 32-bit range.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = residual.Vector512Count<short>();

            for (; vectorCount > 0; vectorCount--, offset += Vector512<short>.Count)
            {
                Vector512<short> values = Unsafe.As<short, Vector512<short>>(ref Unsafe.Add(ref residualBase, offset));
                sum += SumSquares(values);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = residual[offset..].Vector256Count<short>();

            for (; vectorCount > 0; vectorCount--, offset += Vector256<short>.Count)
            {
                Vector256<short> values = Unsafe.As<short, Vector256<short>>(ref Unsafe.Add(ref residualBase, offset));
                sum += SumSquares(values);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = residual[offset..].Vector128Count<short>();

            for (; vectorCount > 0; vectorCount--, offset += Vector128<short>.Count)
            {
                Vector128<short> values = Unsafe.As<short, Vector128<short>>(ref Unsafe.Add(ref residualBase, offset));
                sum += SumSquares(values);
            }
        }

        for (; offset < residual.Length; offset++)
        {
            int value = Unsafe.Add(ref residualBase, offset);
            sum += value * value;
        }

        return sum;
    }

    private static long SumSquaredError<TSample, TOperator>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        int width,
        int height)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        long sum = 0;
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<TSample> sourceRow = source.Slice(y * sourceStride, width);
            ReadOnlySpan<TSample> predictionRow = prediction.Slice(y * predictionStride, width);
            ref TSample sourceBase = ref MemoryMarshal.GetReference(sourceRow);
            ref TSample predictionBase = ref MemoryMarshal.GetReference(predictionRow);
            int x = 0;

            // Descending hardware widths consume every complete vector before the scalar tail. Byte
            // subtraction produces two widened residual vectors; high-bit-depth subtraction produces one.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = sourceRow.Vector512Count<TSample>();

                for (; vectorCount > 0; vectorCount--, x += Vector512<TSample>.Count)
                {
                    Vector512<TSample> sourceVector =
                        Unsafe.As<TSample, Vector512<TSample>>(ref Unsafe.Add(ref sourceBase, x));

                    Vector512<TSample> predictionVector =
                        Unsafe.As<TSample, Vector512<TSample>>(ref Unsafe.Add(ref predictionBase, x));

                    Vector512<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector512<short> upper);
                    sum += SumSquares(lower);
                    if (Vector512<TSample>.Count != Vector512<short>.Count)
                    {
                        sum += SumSquares(upper);
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = sourceRow[x..].Vector256Count<TSample>();

                for (; vectorCount > 0; vectorCount--, x += Vector256<TSample>.Count)
                {
                    Vector256<TSample> sourceVector =
                        Unsafe.As<TSample, Vector256<TSample>>(ref Unsafe.Add(ref sourceBase, x));

                    Vector256<TSample> predictionVector =
                        Unsafe.As<TSample, Vector256<TSample>>(ref Unsafe.Add(ref predictionBase, x));

                    Vector256<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector256<short> upper);
                    sum += SumSquares(lower);
                    if (Vector256<TSample>.Count != Vector256<short>.Count)
                    {
                        sum += SumSquares(upper);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = sourceRow[x..].Vector128Count<TSample>();

                for (; vectorCount > 0; vectorCount--, x += Vector128<TSample>.Count)
                {
                    Vector128<TSample> sourceVector =
                        Unsafe.As<TSample, Vector128<TSample>>(ref Unsafe.Add(ref sourceBase, x));

                    Vector128<TSample> predictionVector =
                        Unsafe.As<TSample, Vector128<TSample>>(ref Unsafe.Add(ref predictionBase, x));

                    Vector128<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector128<short> upper);
                    sum += SumSquares(lower);
                    if (Vector128<TSample>.Count != Vector128<short>.Count)
                    {
                        sum += SumSquares(upper);
                    }
                }
            }

            for (; x < width; x++)
            {
                int difference = TOperator.Subtract(
                    Unsafe.Add(ref sourceBase, x),
                    Unsafe.Add(ref predictionBase, x));

                sum += difference * difference;
            }
        }

        return sum;
    }

    private static void Subtract<TSample, TOperator>(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        Span<short> residual,
        int residualStride,
        int width,
        int height)
        where TSample : unmanaged
        where TOperator : struct, IResidualOperator<TSample>
    {
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<TSample> sourceRow = source.Slice(y * sourceStride, width);
            ReadOnlySpan<TSample> predictionRow = prediction.Slice(y * predictionStride, width);
            Span<short> residualRow = residual.Slice(y * residualStride, width);

            ref TSample sourceBase = ref MemoryMarshal.GetReference(sourceRow);
            ref TSample predictionBase = ref MemoryMarshal.GetReference(predictionRow);
            ref short residualBase = ref MemoryMarshal.GetReference(residualRow);
            int x = 0;

            // Each narrower tier resumes at the shared sample offset, preserving SIMD execution for the widest
            // possible remainder while leaving only a sub-vector tail for scalar subtraction.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = sourceRow.Vector512Count<TSample>();

                for (; vectorCount > 0; vectorCount--, x += Vector512<TSample>.Count)
                {
                    Vector512<TSample> sourceVector = Unsafe.As<TSample, Vector512<TSample>>(ref Unsafe.Add(ref sourceBase, x));
                    Vector512<TSample> predictionVector = Unsafe.As<TSample, Vector512<TSample>>(ref Unsafe.Add(ref predictionBase, x));
                    Vector512<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector512<short> upper);

                    Unsafe.As<short, Vector512<short>>(ref Unsafe.Add(ref residualBase, x)) = lower;

                    // Byte vectors widen into two signed-short vectors; high-bit-depth vectors retain one lane per sample.
                    if (Vector512<TSample>.Count != Vector512<short>.Count)
                    {
                        Unsafe.As<short, Vector512<short>>(ref Unsafe.Add(ref residualBase, x + Vector512<short>.Count)) = upper;
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = sourceRow[x..].Vector256Count<TSample>();

                for (; vectorCount > 0; vectorCount--, x += Vector256<TSample>.Count)
                {
                    Vector256<TSample> sourceVector = Unsafe.As<TSample, Vector256<TSample>>(ref Unsafe.Add(ref sourceBase, x));
                    Vector256<TSample> predictionVector = Unsafe.As<TSample, Vector256<TSample>>(ref Unsafe.Add(ref predictionBase, x));
                    Vector256<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector256<short> upper);

                    Unsafe.As<short, Vector256<short>>(ref Unsafe.Add(ref residualBase, x)) = lower;

                    if (Vector256<TSample>.Count != Vector256<short>.Count)
                    {
                        Unsafe.As<short, Vector256<short>>(ref Unsafe.Add(ref residualBase, x + Vector256<short>.Count)) = upper;
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = sourceRow[x..].Vector128Count<TSample>();

                for (; vectorCount > 0; vectorCount--, x += Vector128<TSample>.Count)
                {
                    Vector128<TSample> sourceVector = Unsafe.As<TSample, Vector128<TSample>>(ref Unsafe.Add(ref sourceBase, x));
                    Vector128<TSample> predictionVector = Unsafe.As<TSample, Vector128<TSample>>(ref Unsafe.Add(ref predictionBase, x));
                    Vector128<short> lower = TOperator.Subtract(sourceVector, predictionVector, out Vector128<short> upper);

                    Unsafe.As<short, Vector128<short>>(ref Unsafe.Add(ref residualBase, x)) = lower;

                    if (Vector128<TSample>.Count != Vector128<short>.Count)
                    {
                        Unsafe.As<short, Vector128<short>>(ref Unsafe.Add(ref residualBase, x + Vector128<short>.Count)) = upper;
                    }
                }
            }

            for (; x < width; x++)
            {
                Unsafe.Add(ref residualBase, x) = TOperator.Subtract(
                    Unsafe.Add(ref sourceBase, x),
                    Unsafe.Add(ref predictionBase, x));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SumSquares(Vector128<short> values)
    {
        Vector128<int> lower = Vector128.WidenLower(values);
        Vector128<int> upper = Vector128.WidenUpper(values);
        return (long)Vector128.Sum(lower * lower) + Vector128.Sum(upper * upper);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SumSquares(Vector256<short> values)
    {
        Vector256<int> lower = Vector256.WidenLower(values);
        Vector256<int> upper = Vector256.WidenUpper(values);
        return (long)Vector256.Sum(lower * lower) + Vector256.Sum(upper * upper);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SumSquares(Vector512<short> values)
    {
        Vector512<int> lower = Vector512.WidenLower(values);
        Vector512<int> upper = Vector512.WidenUpper(values);
        return (long)Vector512.Sum(lower * lower) + Vector512.Sum(upper * upper);
    }
}
