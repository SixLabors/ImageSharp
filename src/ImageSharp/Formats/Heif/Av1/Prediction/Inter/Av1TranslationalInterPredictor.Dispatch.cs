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
internal static partial class Av1TranslationalInterPredictor
{
    /// <summary>
    /// Selects an 8-bit horizontal interpolation operator.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
    /// <param name="intermediateRows">Caller-owned intermediate rows sized by <see cref="GetIntermediateRowLength"/> when both phases are nonzero.</param>
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
        Span<short> intermediateRows)
    {
        DebugGuard.IsTrue(
            horizontalPhase == 0 || verticalPhase == 0 || intermediateRows.Length >= GetIntermediateRowLength(width, height),
            "The two-dimensional intermediate rows hold fewer samples than GetIntermediateRowLength requires.");

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
                    intermediateRows);

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
                    intermediateRows);

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
                    intermediateRows);

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
                    intermediateRows);

                break;
        }
    }

    /// <summary>
    /// Selects a high-bit-depth horizontal interpolation operator.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="intermediateRows">Caller-owned intermediate rows sized by <see cref="GetIntermediateRowLength"/> when both phases are nonzero.</param>
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
        Span<short> intermediateRows)
    {
        DebugGuard.IsTrue(
            horizontalPhase == 0 || verticalPhase == 0 || intermediateRows.Length >= GetIntermediateRowLength(width, height),
            "The two-dimensional intermediate rows hold fewer samples than GetIntermediateRowLength requires.");

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
                    intermediateRows);

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
                    intermediateRows);

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
                    intermediateRows);

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
                    intermediateRows);

                break;
        }
    }

    /// <summary>
    /// Selects a closed 8-bit horizontal interpolation operator for explicit scalar execution.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
    /// <param name="intermediateRows">Caller-owned intermediate rows sized by <see cref="GetIntermediateRowLength"/> when both phases are nonzero.</param>
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
        Span<short> intermediateRows)
    {
        // This benchmark and test entry point closes the same production operators, but ends in the scalar kernels.
        // Thus the SIMD-first decoder path carries no runtime mode flag.
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
                    intermediateRows);

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
                    intermediateRows);

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
                    intermediateRows);

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
                    intermediateRows);

                break;
        }
    }

    /// <summary>
    /// Selects a closed high-bit-depth horizontal interpolation operator for explicit scalar execution.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="intermediateRows">Caller-owned intermediate rows sized by <see cref="GetIntermediateRowLength"/> when both phases are nonzero.</param>
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
        Span<short> intermediateRows)
    {
        // The scalar path closes the production table operators, so that it uses the same normative Q7 coefficients.
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
                    intermediateRows);

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
                    intermediateRows);

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
                    intermediateRows);

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
                    intermediateRows);

                break;
        }
    }

    /// <summary>
    /// Copies an 8-bit integer-position block with the widest vector that fits a complete row prefix.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the first source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
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
            int vectorEnd = (int)(Numerics.Vector512Count<byte>(width) * (nuint)Vector512<byte>.Count);
            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                for (; column < vectorEnd; column += Vector512<byte>.Count)
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
            int vectorEnd = (int)(Numerics.Vector256Count<byte>(width) * (nuint)Vector256<byte>.Count);
            for (int row = 0; row < height; row++)
            {
                ref byte sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                for (; column < vectorEnd; column += Vector256<byte>.Count)
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

                int vectorEnd = (int)(Numerics.Vector128Count<byte>(width) * (nuint)Vector128<byte>.Count);
                int column = 0;
                for (; column < vectorEnd; column += Vector128<byte>.Count)
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
    /// Copies a high-bit-depth integer-position block with the widest vector that fits a complete row prefix.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the first source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
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
            int vectorEnd = (int)(Numerics.Vector512Count<ushort>(width) * (nuint)Vector512<ushort>.Count);
            for (int row = 0; row < height; row++)
            {
                ref ushort sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                for (; column < vectorEnd; column += Vector512<ushort>.Count)
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
            int vectorEnd = (int)(Numerics.Vector256Count<ushort>(width) * (nuint)Vector256<ushort>.Count);
            for (int row = 0; row < height; row++)
            {
                ref ushort sourceRow = ref Unsafe.Add(ref sourceBase, row * sourceStride);
                ref ushort destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                for (; column < vectorEnd; column += Vector256<ushort>.Count)
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
                    // Subsampled chroma of a block smaller than 8x8 can be two samples wide.
                    // The full vector load stays, and the store writes only the row width.
                    StorePartial(Vector128.LoadUnsafe(ref sourceRow), ref destinationRow, width);
                    continue;
                }

                int vectorEnd = (int)(Numerics.Vector128Count<ushort>(width) * (nuint)Vector128<ushort>.Count);
                int column = 0;
                for (; column < vectorEnd; column += Vector128<ushort>.Count)
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
    /// Applies a one-dimensional 8-bit filter with one SIMD width for the complete block.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="coefficients">The filter coefficients, starting at the first applied tap.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="sourceOffset">The offset from the integer-position sample to the sample of the first applied tap.</param>
    /// <param name="tapStride">The distance between consecutive tap samples: one for a horizontal filter, or the row stride for a vertical filter.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
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
    /// Applies a one-dimensional high-bit-depth filter with one SIMD width for the complete block.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="coefficients">The filter coefficients, starting at the first applied tap.</param>
    /// <param name="tapCount">The number of applied filter taps.</param>
    /// <param name="sourceOffset">The offset from the integer-position sample to the sample of the first applied tap.</param>
    /// <param name="tapStride">The distance between consecutive tap samples: one for a horizontal filter, or the row stride for a vertical filter.</param>
    /// <param name="firstRound">The first rounding shift.</param>
    /// <param name="secondRound">The second rounding shift. Zero skips this rounding.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
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
    /// Applies separable two-dimensional filtering to an 8-bit block with one SIMD width.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalCoefficients">The horizontal filter coefficients, starting at the first applied tap.</param>
    /// <param name="horizontalTapCount">The number of applied horizontal taps.</param>
    /// <param name="horizontalSourceOffset">The column offset from the integer-position sample to the first applied horizontal tap.</param>
    /// <param name="verticalCoefficients">The vertical filter coefficients, starting at the first applied tap.</param>
    /// <param name="verticalTapCount">The number of applied vertical taps.</param>
    /// <param name="verticalSourceOffset">The row offset from the integer-position sample to the first applied vertical tap.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="intermediateRows">Signed intermediate storage of at least <see cref="GetIntermediateRowLength"/> elements.</param>
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
        Span<short> intermediateRows)
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
                intermediateRows,
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
                intermediateRows,
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
                intermediateRows,
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
            intermediateRows);
    }

    /// <summary>
    /// Applies separable two-dimensional filtering to a high-bit-depth block with one SIMD width.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalCoefficients">The horizontal filter coefficients, starting at the first applied tap.</param>
    /// <param name="horizontalTapCount">The number of applied horizontal taps.</param>
    /// <param name="horizontalSourceOffset">The column offset from the integer-position sample to the first applied horizontal tap.</param>
    /// <param name="verticalCoefficients">The vertical filter coefficients, starting at the first applied tap.</param>
    /// <param name="verticalTapCount">The number of applied vertical taps.</param>
    /// <param name="verticalSourceOffset">The row offset from the integer-position sample to the first applied vertical tap.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="round0">The rounding shift of the horizontal pass. The vertical pass shifts by 14 minus this value.</param>
    /// <param name="intermediateRows">Signed intermediate storage of at least <see cref="GetIntermediateRowLength"/> elements.</param>
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
        Span<short> intermediateRows)
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
                intermediateRows,
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
                intermediateRows,
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
                intermediateRows,
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
            intermediateRows);
    }

    /// <summary>
    /// Copies an 8-bit integer-position prediction without explicit hardware intrinsics.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the first source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
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
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The distance between source rows in samples.</param>
    /// <param name="sourceOrigin">The index of the first source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
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
