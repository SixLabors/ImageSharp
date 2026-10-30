// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

/// <summary>
/// Converts strided sample blocks and canonically ordered JPEG XL DCT coefficients.
/// </summary>
/// <remarks>
/// The coefficient matrix has min(rows, columns) rows and max(rows, columns) columns. A tall or square source therefore has transposed coefficients.
/// Each SIMD lane holds an independent one-dimensional transform. The two axes retain libjxl's operation order and normalization.
/// </remarks>
internal static partial class JxlDct
{
    /// <summary>
    /// Gets the maximum one-dimensional workspace length, in floats, for the enabled SIMD width and supported 256-point transforms.
    /// </summary>
    /// <remarks>
    /// The first bank holds the current vectors. The second bank holds the even and odd stages.
    /// Recursive calls exchange these banks after their input values are no longer needed. No transform call allocates storage.
    /// The lane count follows the same hardware checks as transform dispatch. Scalar execution requires one lane per position.
    /// </remarks>
    public static int MaximumWorkingSetLength
    {
        get
        {
            // Dispatch cannot enter a wider worker when its hardware check is false. Reserve two banks only for the widest enabled worker.
            int lanes = Vector512.IsHardwareAccelerated ? Vector512<float>.Count
                : Vector256.IsHardwareAccelerated ? Vector256<float>.Count
                : Vector128.IsHardwareAccelerated ? Vector128<float>.Count : 1;

            return 2 * JxlAcStrategy.MaximumBlockDimension * lanes;
        }
    }

    /// <summary>
    /// Computes the normalized forward DCT without changing the source pixels.
    /// </summary>
    /// <param name="rows">The source height. Both dimensions are powers of two from one through 256.</param>
    /// <param name="columns">The source width.</param>
    /// <param name="source">The borrowed source samples. Source and output storage must not overlap.</param>
    /// <param name="sourceStride">The physical source row stride, in floats.</param>
    /// <param name="coefficients">The contiguous output coefficient matrix in canonical order.</param>
    /// <param name="workspace">Borrowed scratch for one sample block and two banks of max(rows, columns) SIMD vectors.</param>
    public static void ComputeScaledDct(int rows, int columns, ReadOnlySpan<float> source, int sourceStride, Span<float> coefficients, Span<float> workspace)
    {
        int area = rows * columns;
        bool transposed = rows >= columns;

        // The forward order is height, then width. Retain the first-axis matrix in sample order until each complete row band is consumed.
        // Transposed final stores would overwrite later bands in the coefficient buffer, so that case uses the existing block-sized scratch region.
        Span<float> intermediate = transposed ? workspace[..area] : coefficients[..area];
        Span<float> work = transposed ? workspace[area..] : workspace;
        ForwardColumns(rows, columns, source, sourceStride, intermediate, columns, work);
        ForwardRows(rows, columns, intermediate, columns, coefficients, transposed ? rows : columns, transposed, work);
    }

    /// <summary>
    /// Restores pixels from a canonical coefficient matrix with the inverse DCT.
    /// </summary>
    /// <param name="rows">The restored height. Both dimensions are powers of two from one through 256.</param>
    /// <param name="columns">The restored width.</param>
    /// <param name="coefficients">The contiguous coefficient matrix. Its contents are consumed and can change.</param>
    /// <param name="pixels">The borrowed destination samples. This storage must not overlap the coefficient or scratch storage.</param>
    /// <param name="pixelsStride">The physical destination row stride, in floats.</param>
    /// <param name="workspace">Borrowed scratch for two banks of max(rows, columns) SIMD vectors.</param>
    public static void ComputeScaledInverseDct(int rows, int columns, Span<float> coefficients, Span<float> pixels, int pixelsStride, Span<float> workspace)
    {
        // Inverse axes run in the opposite order: width, then height. The first axis reuses the consumed coefficients.
        // Only the final axis writes pixels. Row-band loads finish before their same-orientation stores, so no source clone is needed.
        if (rows >= columns)
        {
            InverseColumns(columns, rows, coefficients, rows, coefficients, rows, workspace);
            InverseRows(columns, rows, coefficients, rows, pixels, pixelsStride, true, workspace);
        }
        else
        {
            InverseRows(rows, columns, coefficients, columns, coefficients, columns, false, workspace);
            InverseColumns(rows, columns, coefficients, columns, pixels, pixelsStride, workspace);
        }
    }

    /// <summary>
    /// Applies the forward or inverse AFV basis to one four-by-four sample block.
    /// </summary>
    /// <param name="source">Sixteen input samples. The destination must not overlap this span.</param>
    /// <param name="basis">Sixteen rows of sixteen coefficients. Row j contains input j's contribution to each output position.</param>
    /// <param name="destination">Storage for sixteen output samples. Later samples remain unchanged.</param>
    public static void TransformAfv4x4(ReadOnlySpan<float> source, ReadOnlySpan<float[]> basis, Span<float> destination)
    {
        ref float output = ref MemoryMarshal.GetReference(destination);

        // A lane identifies one output position. Accumulate inputs in their serialized basis order, as libjxl does.
        // The basis is shared constant data. Borrow its existing arrays directly; no matrix packing or reduction buffer is needed.
        if (Vector512.IsHardwareAccelerated)
        {
            for (int i = 0; i < 16; i += Vector512<float>.Count)
            {
                Vector512<float> result = Vector512<float>.Zero;

                for (int j = 0; j < 16; j++)
                {
                    ref float row = ref MemoryMarshal.GetArrayDataReference(basis[j]);
                    Vector512<float> weights = Vector512.LoadUnsafe(ref row, (uint)i);
                    result = Vector512.MultiplyAddEstimate(Vector512.Create(source[j]), weights, result);
                }

                result.StoreUnsafe(ref output, (uint)i);
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            for (int i = 0; i < 16; i += Vector256<float>.Count)
            {
                Vector256<float> result = Vector256<float>.Zero;

                for (int j = 0; j < 16; j++)
                {
                    ref float row = ref MemoryMarshal.GetArrayDataReference(basis[j]);
                    Vector256<float> weights = Vector256.LoadUnsafe(ref row, (uint)i);
                    result = Vector256.MultiplyAddEstimate(Vector256.Create(source[j]), weights, result);
                }

                result.StoreUnsafe(ref output, (uint)i);
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            for (int i = 0; i < 16; i += Vector128<float>.Count)
            {
                Vector128<float> result = Vector128<float>.Zero;

                for (int j = 0; j < 16; j++)
                {
                    ref float row = ref MemoryMarshal.GetArrayDataReference(basis[j]);
                    Vector128<float> weights = Vector128.LoadUnsafe(ref row, (uint)i);
                    result = Vector128.MultiplyAddEstimate(Vector128.Create(source[j]), weights, result);
                }

                result.StoreUnsafe(ref output, (uint)i);
            }
        }
        else
        {
            for (int i = 0; i < 16; i++)
            {
                float result = 0;

                for (int j = 0; j < 16; j++)
                {
                    result = float.MultiplyAddEstimate(source[j], basis[j][i], result);
                }

                destination[i] = result;
            }
        }
    }

    /// <summary>
    /// Selects complete SIMD batches once for each independent axis. Remaining one- or two-value batches use scalar arithmetic.
    /// </summary>
    /// <param name="length">The number of positions along each one-dimensional transform.</param>
    /// <param name="count">The number of independent transforms.</param>
    /// <param name="source">The borrowed input matrix.</param>
    /// <param name="sourceStride">The input row stride, in floats.</param>
    /// <param name="destination">The borrowed output matrix.</param>
    /// <param name="destinationStride">The output row stride, in floats.</param>
    /// <param name="workspace">The shared two-bank transform workspace.</param>
    private static void ForwardColumns(
        int length,
        int count,
        ReadOnlySpan<float> source,
        int sourceStride,
        Span<float> destination,
        int destinationStride,
        Span<float> workspace)
    {
        int first = 0;

        if (Vector512.IsHardwareAccelerated && count - first >= Vector512<float>.Count)
        {
            first = ForwardColumns512(length, count, source, sourceStride, destination, destinationStride, first, workspace);
        }

        if (Vector256.IsHardwareAccelerated && count - first >= Vector256<float>.Count)
        {
            first = ForwardColumns256(length, count, source, sourceStride, destination, destinationStride, first, workspace);
        }

        if (Vector128.IsHardwareAccelerated && count - first >= Vector128<float>.Count)
        {
            first = ForwardColumns128(length, count, source, sourceStride, destination, destinationStride, first, workspace);
        }

        if (first < count)
        {
            ForwardColumnsScalar(length, count, source, sourceStride, destination, destinationStride, first, workspace);
        }
    }

    /// <summary>
    /// Selects complete SIMD batches once for each independent axis. Remaining one- or two-value batches use scalar arithmetic.
    /// </summary>
    /// <param name="count">The number of independent transforms.</param>
    /// <param name="length">The number of positions along each one-dimensional transform.</param>
    /// <param name="source">The borrowed input matrix.</param>
    /// <param name="sourceStride">The input row stride, in floats.</param>
    /// <param name="destination">The borrowed output matrix.</param>
    /// <param name="destinationStride">The output row stride, in floats.</param>
    /// <param name="transposed">Whether transformed positions become destination rows and independent input rows become destination columns.</param>
    /// <param name="workspace">The shared two-bank transform workspace.</param>
    private static void ForwardRows(
        int count,
        int length,
        ReadOnlySpan<float> source,
        int sourceStride,
        Span<float> destination,
        int destinationStride,
        bool transposed,
        Span<float> workspace)
    {
        int first = 0;

        if (Vector512.IsHardwareAccelerated && count - first >= Vector512<float>.Count && length >= Vector512<float>.Count)
        {
            first = ForwardRows512(length, count, source, sourceStride, destination, destinationStride, transposed, first, workspace);
        }

        if (Vector256.IsHardwareAccelerated && count - first >= Vector256<float>.Count && length >= Vector256<float>.Count)
        {
            first = ForwardRows256(length, count, source, sourceStride, destination, destinationStride, transposed, first, workspace);
        }

        if (Vector128.IsHardwareAccelerated && count - first >= Vector128<float>.Count && length >= Vector128<float>.Count)
        {
            first = ForwardRows128(length, count, source, sourceStride, destination, destinationStride, transposed, first, workspace);
        }

        if (first < count)
        {
            ForwardRowsScalar(length, count, source, sourceStride, destination, destinationStride, transposed, first, workspace);
        }
    }

    /// <summary>
    /// Selects complete SIMD batches once for each independent axis. Remaining one- or two-value batches use scalar arithmetic.
    /// </summary>
    /// <param name="length">The number of positions along each one-dimensional transform.</param>
    /// <param name="count">The number of independent transforms.</param>
    /// <param name="source">The borrowed input matrix.</param>
    /// <param name="sourceStride">The input row stride, in floats.</param>
    /// <param name="destination">The borrowed output matrix.</param>
    /// <param name="destinationStride">The output row stride, in floats.</param>
    /// <param name="workspace">The shared two-bank transform workspace.</param>
    private static void InverseColumns(
        int length,
        int count,
        ReadOnlySpan<float> source,
        int sourceStride,
        Span<float> destination,
        int destinationStride,
        Span<float> workspace)
    {
        int first = 0;

        if (Vector512.IsHardwareAccelerated && count - first >= Vector512<float>.Count)
        {
            first = InverseColumns512(length, count, source, sourceStride, destination, destinationStride, first, workspace);
        }

        if (Vector256.IsHardwareAccelerated && count - first >= Vector256<float>.Count)
        {
            first = InverseColumns256(length, count, source, sourceStride, destination, destinationStride, first, workspace);
        }

        if (Vector128.IsHardwareAccelerated && count - first >= Vector128<float>.Count)
        {
            first = InverseColumns128(length, count, source, sourceStride, destination, destinationStride, first, workspace);
        }

        if (first < count)
        {
            InverseColumnsScalar(length, count, source, sourceStride, destination, destinationStride, first, workspace);
        }
    }

    /// <summary>
    /// Selects complete SIMD batches once for each independent axis. Remaining one- or two-value batches use scalar arithmetic.
    /// </summary>
    /// <param name="count">The number of independent transforms.</param>
    /// <param name="length">The number of positions along each one-dimensional transform.</param>
    /// <param name="source">The borrowed input matrix.</param>
    /// <param name="sourceStride">The input row stride, in floats.</param>
    /// <param name="destination">The borrowed output matrix.</param>
    /// <param name="destinationStride">The output row stride, in floats.</param>
    /// <param name="transposed">Whether transformed positions become destination rows and independent input rows become destination columns.</param>
    /// <param name="workspace">The shared two-bank transform workspace.</param>
    private static void InverseRows(
        int count,
        int length,
        ReadOnlySpan<float> source,
        int sourceStride,
        Span<float> destination,
        int destinationStride,
        bool transposed,
        Span<float> workspace)
    {
        int first = 0;

        if (Vector512.IsHardwareAccelerated && count - first >= Vector512<float>.Count && length >= Vector512<float>.Count)
        {
            first = InverseRows512(length, count, source, sourceStride, destination, destinationStride, transposed, first, workspace);
        }

        if (Vector256.IsHardwareAccelerated && count - first >= Vector256<float>.Count && length >= Vector256<float>.Count)
        {
            first = InverseRows256(length, count, source, sourceStride, destination, destinationStride, transposed, first, workspace);
        }

        if (Vector128.IsHardwareAccelerated && count - first >= Vector128<float>.Count && length >= Vector128<float>.Count)
        {
            first = InverseRows128(length, count, source, sourceStride, destination, destinationStride, transposed, first, workspace);
        }

        if (first < count)
        {
            InverseRowsScalar(length, count, source, sourceStride, destination, destinationStride, transposed, first, workspace);
        }
    }
}
