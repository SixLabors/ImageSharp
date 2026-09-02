// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Builds signed AV1 residual planes from source and prediction samples.
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
}
