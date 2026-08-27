// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Selects interpolation filters and the widest supported traversal for a translational prediction block.
/// </content>
internal static partial class Av1InterPredictor
{
    /// <summary>
    /// Selects an 8-bit horizontal interpolation operator.
    /// </summary>
    private static void Dispatch(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        Span<short> scratch)
    {
        switch (horizontalFilter)
        {
            case Av1InterpolationFilter.Regular:
                DispatchVertical<RegularOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    scratch);

                break;
            case Av1InterpolationFilter.Smooth:
                DispatchVertical<SmoothOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    scratch);

                break;
            case Av1InterpolationFilter.Sharp:
                DispatchVertical<SharpOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    scratch);

                break;
            default:
                DispatchVertical<BilinearOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    scratch);

                break;
        }
    }

    /// <summary>
    /// Selects a high-bit-depth horizontal interpolation operator.
    /// </summary>
    private static void Dispatch(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        int bitDepth,
        Span<short> scratch)
    {
        switch (horizontalFilter)
        {
            case Av1InterpolationFilter.Regular:
                DispatchVertical<RegularOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch);

                break;
            case Av1InterpolationFilter.Smooth:
                DispatchVertical<SmoothOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch);

                break;
            case Av1InterpolationFilter.Sharp:
                DispatchVertical<SharpOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch);

                break;
            default:
                DispatchVertical<BilinearOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch);

                break;
        }
    }

    /// <summary>
    /// Selects a closed 8-bit horizontal interpolation operator for explicit scalar execution.
    /// </summary>
    private static void DispatchScalar(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        Span<short> scratch)
    {
        // The benchmark/test entry point closes the same production operators explicitly, but terminates in the
        // scalar kernels without carrying a runtime mode flag through the SIMD-first decoder path.
        switch (horizontalFilter)
        {
            case Av1InterpolationFilter.Regular:
                DispatchVerticalScalar<RegularOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    scratch);

                break;
            case Av1InterpolationFilter.Smooth:
                DispatchVerticalScalar<SmoothOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    scratch);

                break;
            case Av1InterpolationFilter.Sharp:
                DispatchVerticalScalar<SharpOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    scratch);

                break;
            default:
                DispatchVerticalScalar<BilinearOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    scratch);

                break;
        }
    }

    /// <summary>
    /// Selects a closed high-bit-depth horizontal interpolation operator for explicit scalar execution.
    /// </summary>
    private static void DispatchScalar(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        int bitDepth,
        Span<short> scratch)
    {
        // Closing the production table operators here keeps scalar parity coverage on the same normative Q7 data.
        switch (horizontalFilter)
        {
            case Av1InterpolationFilter.Regular:
                DispatchVerticalScalar<RegularOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch);

                break;
            case Av1InterpolationFilter.Smooth:
                DispatchVerticalScalar<SmoothOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch);

                break;
            case Av1InterpolationFilter.Sharp:
                DispatchVerticalScalar<SharpOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch);

                break;
            default:
                DispatchVerticalScalar<BilinearOperator>(
                    source,
                    sourceStride,
                    sourceOrigin,
                    destination,
                    destinationStride,
                    width,
                    height,
                    verticalFilter,
                    horizontalPhase,
                    verticalPhase,
                    bitDepth,
                    scratch);

                break;
        }
    }

    /// <summary>
    /// Copies an 8-bit integer-position block using the widest vector that fits a complete row prefix.
    /// </summary>
    private static void Copy(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);

        if (Vector512.IsHardwareAccelerated && Vector<int>.Count == Vector512<int>.Count && width >= Vector512<byte>.Count)
        {
            int vectorEnd = width - Vector512<byte>.Count;
            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                for (; column <= vectorEnd; column += Vector512<byte>.Count)
                {
                    Vector512.LoadUnsafe(ref sourceRow, (nuint)column).StoreUnsafe(ref destinationRow, (nuint)column);
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = Unsafe.Add(ref sourceRow, column);
                }
            }

            return;
        }

        if (Vector256.IsHardwareAccelerated && width >= Vector256<byte>.Count)
        {
            int vectorEnd = width - Vector256<byte>.Count;
            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                for (; column <= vectorEnd; column += Vector256<byte>.Count)
                {
                    Vector256.LoadUnsafe(ref sourceRow, (nuint)column).StoreUnsafe(ref destinationRow, (nuint)column);
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = Unsafe.Add(ref sourceRow, column);
                }
            }

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                if (width < Vector128<byte>.Count)
                {
                    StorePartial(Vector128.LoadUnsafe(ref sourceRow), ref destinationRow, width);
                    continue;
                }

                int vectorEnd = width - Vector128<byte>.Count;
                int column = 0;
                for (; column <= vectorEnd; column += Vector128<byte>.Count)
                {
                    Vector128.LoadUnsafe(ref sourceRow, (nuint)column).StoreUnsafe(ref destinationRow, (nuint)column);
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = Unsafe.Add(ref sourceRow, column);
                }
            }

            return;
        }

        CopyScalar(source, sourceStride, sourceOrigin, destination, destinationStride, width, height);
    }

    /// <summary>
    /// Copies a high-bit-depth integer-position block using the widest vector that fits a complete row prefix.
    /// </summary>
    private static void Copy(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height)
    {
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);

        if (Vector512.IsHardwareAccelerated && Vector<int>.Count == Vector512<int>.Count && width >= Vector512<ushort>.Count)
        {
            int vectorEnd = width - Vector512<ushort>.Count;
            for (int row = 0; row < height; row++)
            {
                ref ushort sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                for (; column <= vectorEnd; column += Vector512<ushort>.Count)
                {
                    Vector512.LoadUnsafe(ref sourceRow, (nuint)column).StoreUnsafe(ref destinationRow, (nuint)column);
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = Unsafe.Add(ref sourceRow, column);
                }
            }

            return;
        }

        if (Vector256.IsHardwareAccelerated && width >= Vector256<ushort>.Count)
        {
            int vectorEnd = width - Vector256<ushort>.Count;
            for (int row = 0; row < height; row++)
            {
                ref ushort sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                for (; column <= vectorEnd; column += Vector256<ushort>.Count)
                {
                    Vector256.LoadUnsafe(ref sourceRow, (nuint)column).StoreUnsafe(ref destinationRow, (nuint)column);
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = Unsafe.Add(ref sourceRow, column);
                }
            }

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (int row = 0; row < height; row++)
            {
                ref ushort sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                if (width < Vector128<ushort>.Count)
                {
                    // Four high-bit-depth samples occupy exactly the lower 64 bits of the vector.
                    Vector128.LoadUnsafe(ref sourceRow).GetLower().StoreUnsafe(ref destinationRow);
                    continue;
                }

                int vectorEnd = width - Vector128<ushort>.Count;
                int column = 0;
                for (; column <= vectorEnd; column += Vector128<ushort>.Count)
                {
                    Vector128.LoadUnsafe(ref sourceRow, (nuint)column).StoreUnsafe(ref destinationRow, (nuint)column);
                }

                for (; column < width; column++)
                {
                    Unsafe.Add(ref destinationRow, column) = Unsafe.Add(ref sourceRow, column);
                }
            }

            return;
        }

        CopyScalar(source, sourceStride, sourceOrigin, destination, destinationStride, width, height);
    }

    /// <summary>
    /// Applies a one-dimensional 8-bit filter using one SIMD width for the complete block.
    /// </summary>
    private static void FilterDirect(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> coefficients,
        int tapCount,
        int sourceOffset,
        int tapStride,
        int firstRound,
        int secondRound)
    {
        if (Vector512.IsHardwareAccelerated && Vector<int>.Count == Vector512<int>.Count && width >= Vector512<byte>.Count)
        {
            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                coefficients,
                tapCount,
                sourceOffset,
                tapStride,
                firstRound,
                secondRound,
                Vector512<int>.Zero);

            return;
        }

        if (Vector256.IsHardwareAccelerated && width >= Vector256<byte>.Count)
        {
            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                coefficients,
                tapCount,
                sourceOffset,
                tapStride,
                firstRound,
                secondRound,
                Vector256<int>.Zero);

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                coefficients,
                tapCount,
                sourceOffset,
                tapStride,
                firstRound,
                secondRound,
                Vector128<int>.Zero);

            return;
        }

        FilterDirectScalar(
            source,
            sourceStride,
            sourceOrigin,
            destination,
            destinationStride,
            width,
            height,
            coefficients,
            tapCount,
            sourceOffset,
            tapStride,
            firstRound,
            secondRound);
    }

    /// <summary>
    /// Applies a one-dimensional high-bit-depth filter using one SIMD width for the complete block.
    /// </summary>
    private static void FilterDirect(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> coefficients,
        int tapCount,
        int sourceOffset,
        int tapStride,
        int firstRound,
        int secondRound,
        int bitDepth)
    {
        if (Vector512.IsHardwareAccelerated && Vector<int>.Count == Vector512<int>.Count && width >= Vector512<ushort>.Count)
        {
            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                coefficients,
                tapCount,
                sourceOffset,
                tapStride,
                firstRound,
                secondRound,
                bitDepth,
                Vector512<int>.Zero);

            return;
        }

        if (Vector256.IsHardwareAccelerated && width >= Vector256<ushort>.Count)
        {
            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                coefficients,
                tapCount,
                sourceOffset,
                tapStride,
                firstRound,
                secondRound,
                bitDepth,
                Vector256<int>.Zero);

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            FilterDirect(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                coefficients,
                tapCount,
                sourceOffset,
                tapStride,
                firstRound,
                secondRound,
                bitDepth,
                Vector128<int>.Zero);

            return;
        }

        FilterDirectScalar(
            source,
            sourceStride,
            sourceOrigin,
            destination,
            destinationStride,
            width,
            height,
            coefficients,
            tapCount,
            sourceOffset,
            tapStride,
            firstRound,
            secondRound,
            bitDepth);
    }

    /// <summary>
    /// Applies separable two-dimensional filtering to an 8-bit block using one SIMD width.
    /// </summary>
    private static void Filter2D(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> horizontalCoefficients,
        int horizontalTapCount,
        int horizontalSourceOffset,
        ReadOnlySpan<short> verticalCoefficients,
        int verticalTapCount,
        int verticalSourceOffset,
        int bitDepth,
        Span<short> scratch)
    {
        if (Vector512.IsHardwareAccelerated && Vector<int>.Count == Vector512<int>.Count && width >= Vector512<byte>.Count)
        {
            Filter2D(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                horizontalCoefficients,
                horizontalTapCount,
                horizontalSourceOffset,
                verticalCoefficients,
                verticalTapCount,
                verticalSourceOffset,
                bitDepth,
                Round0Bits,
                scratch,
                Vector512<int>.Zero);

            return;
        }

        if (Vector256.IsHardwareAccelerated && width >= Vector256<byte>.Count)
        {
            Filter2D(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                horizontalCoefficients,
                horizontalTapCount,
                horizontalSourceOffset,
                verticalCoefficients,
                verticalTapCount,
                verticalSourceOffset,
                bitDepth,
                Round0Bits,
                scratch,
                Vector256<int>.Zero);

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Filter2D(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                horizontalCoefficients,
                horizontalTapCount,
                horizontalSourceOffset,
                verticalCoefficients,
                verticalTapCount,
                verticalSourceOffset,
                bitDepth,
                Round0Bits,
                scratch,
                Vector128<int>.Zero);

            return;
        }

        Filter2DScalar(
            source,
            sourceStride,
            sourceOrigin,
            destination,
            destinationStride,
            width,
            height,
            horizontalCoefficients,
            horizontalTapCount,
            horizontalSourceOffset,
            verticalCoefficients,
            verticalTapCount,
            verticalSourceOffset,
            bitDepth,
            Round0Bits,
            scratch);
    }

    /// <summary>
    /// Applies separable two-dimensional filtering to a high-bit-depth block using one SIMD width.
    /// </summary>
    private static void Filter2D(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> horizontalCoefficients,
        int horizontalTapCount,
        int horizontalSourceOffset,
        ReadOnlySpan<short> verticalCoefficients,
        int verticalTapCount,
        int verticalSourceOffset,
        int bitDepth,
        int round0,
        Span<short> scratch)
    {
        if (Vector512.IsHardwareAccelerated && Vector<int>.Count == Vector512<int>.Count && width >= Vector512<ushort>.Count)
        {
            Filter2D(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                horizontalCoefficients,
                horizontalTapCount,
                horizontalSourceOffset,
                verticalCoefficients,
                verticalTapCount,
                verticalSourceOffset,
                bitDepth,
                round0,
                scratch,
                Vector512<int>.Zero);

            return;
        }

        if (Vector256.IsHardwareAccelerated && width >= Vector256<ushort>.Count)
        {
            Filter2D(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                horizontalCoefficients,
                horizontalTapCount,
                horizontalSourceOffset,
                verticalCoefficients,
                verticalTapCount,
                verticalSourceOffset,
                bitDepth,
                round0,
                scratch,
                Vector256<int>.Zero);

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Filter2D(
                source,
                sourceStride,
                sourceOrigin,
                destination,
                destinationStride,
                width,
                height,
                horizontalCoefficients,
                horizontalTapCount,
                horizontalSourceOffset,
                verticalCoefficients,
                verticalTapCount,
                verticalSourceOffset,
                bitDepth,
                round0,
                scratch,
                Vector128<int>.Zero);

            return;
        }

        Filter2DScalar(
            source,
            sourceStride,
            sourceOrigin,
            destination,
            destinationStride,
            width,
            height,
            horizontalCoefficients,
            horizontalTapCount,
            horizontalSourceOffset,
            verticalCoefficients,
            verticalTapCount,
            verticalSourceOffset,
            bitDepth,
            round0,
            scratch);
    }

    /// <summary>
    /// Copies an 8-bit integer-position prediction without explicit hardware intrinsics.
    /// </summary>
    private static void CopyScalar(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);

        for (int row = 0; row < height; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

            for (int column = 0; column < width; column++)
            {
                Unsafe.Add(ref destinationRow, column) = Unsafe.Add(ref sourceRow, column);
            }
        }
    }

    /// <summary>
    /// Copies a high-bit-depth integer-position prediction without explicit hardware intrinsics.
    /// </summary>
    private static void CopyScalar(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        int sourceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height)
    {
        ref ushort sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);

        for (int row = 0; row < height; row++)
        {
            ref ushort sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
            ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

            for (int column = 0; column < width; column++)
            {
                Unsafe.Add(ref destinationRow, column) = Unsafe.Add(ref sourceRow, column);
            }
        }
    }
}
