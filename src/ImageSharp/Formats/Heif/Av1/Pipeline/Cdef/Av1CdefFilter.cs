// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

/// <summary>
/// Applies AV1 constrained directional enhancement filtering and derives the dominant direction of reconstructed blocks.
/// </summary>
/// <remarks>
/// Filtering uses signed 16-bit working samples. The 256-bit kernel packs either two complete eight-sample rows or
/// four complete four-sample rows, keeping directional offsets within 128-bit lanes. Direction analysis instead packs
/// one eight-by-eight block per 128-bit lane so AVX2 can evaluate two independent blocks together. Scalar kernels retain
/// the same constrain, clipping, and tie-breaking rules for unsupported hardware and partial edge blocks.
/// </remarks>
internal static class Av1CdefFilter
{
    /// <summary>
    /// The sample value used in the bordered source plane for neighbors outside the coded frame.
    /// </summary>
    public const ushort VeryLarge = 0x4000;

    /// <summary>
    /// Defines storage-specific writes for one filtered row.
    /// </summary>
    /// <typeparam name="TSample">The destination sample storage type.</typeparam>
    private interface IOutputOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Stores four or eight filtered samples from the low vector lanes.
        /// </summary>
        /// <param name="destination">The first element in the destination plane.</param>
        /// <param name="offset">The offset of the first sample to write.</param>
        /// <param name="value">The filtered samples in the low lanes.</param>
        /// <param name="count">The number of valid lanes.</param>
        public static abstract void StoreVector(ref TSample destination, int offset, Vector128<short> value, int count);

        /// <summary>
        /// Stores one filtered sample.
        /// </summary>
        /// <param name="destination">The first element in the destination plane.</param>
        /// <param name="offset">The offset of the sample to write.</param>
        /// <param name="value">The filtered sample.</param>
        public static abstract void StoreScalar(ref TSample destination, int offset, int value);
    }

    /// <summary>
    /// Defines which groups of directional taps participate in one closed filter kernel.
    /// </summary>
    private interface IFilterOperator
    {
        /// <summary>
        /// Gets a value indicating whether the primary directional taps are enabled.
        /// </summary>
        public static abstract bool EnablePrimary { get; }

        /// <summary>
        /// Gets a value indicating whether the secondary off-axis taps are enabled.
        /// </summary>
        public static abstract bool EnableSecondary { get; }
    }

    /// <summary>
    /// Copies an eight-bit sample rectangle into the 16-bit CDEF working plane.
    /// </summary>
    /// <param name="source">The source sample plane.</param>
    /// <param name="sourceOffset">The offset of the rectangle's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The destination CDEF working plane.</param>
    /// <param name="destinationOffset">The offset of the rectangle's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="width">The rectangle width in samples.</param>
    /// <param name="height">The even rectangle height in samples.</param>
    public static void CopyPlane(
        ReadOnlySpan<byte> source,
        int sourceOffset,
        int sourceStride,
        Span<ushort> destination,
        int destinationOffset,
        int destinationStride,
        int width,
        int height)
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);

        if (Avx2.IsSupported)
        {
            // AV1 plane dimensions are multiples of four, so the CDEF copy has an even row count. Processing two rows
            // together follows libaom's AVX2 scheduling while each conversion widens sixteen unsigned samples exactly.
            for (int row = 0; row < height; row += 2)
            {
                int firstSourceRow = sourceOffset + (row * sourceStride);
                int secondSourceRow = firstSourceRow + sourceStride;
                int firstDestinationRow = destinationOffset + (row * destinationStride);
                int secondDestinationRow = firstDestinationRow + destinationStride;
                int column = 0;
                for (; column <= width - Vector128<byte>.Count; column += Vector128<byte>.Count)
                {
                    Vector128<byte> first = Vector128.LoadUnsafe(ref sourceBase, (nuint)(firstSourceRow + column));
                    Vector128<byte> second = Vector128.LoadUnsafe(ref sourceBase, (nuint)(secondSourceRow + column));
                    Avx2.ConvertToVector256Int16(first).AsUInt16().StoreUnsafe(ref destinationBase, (nuint)(firstDestinationRow + column));
                    Avx2.ConvertToVector256Int16(second).AsUInt16().StoreUnsafe(ref destinationBase, (nuint)(secondDestinationRow + column));
                }

                CopyRemainingSamples(ref sourceBase, firstSourceRow, ref destinationBase, firstDestinationRow, column, width);
                CopyRemainingSamples(ref sourceBase, secondSourceRow, ref destinationBase, secondDestinationRow, column, width);
            }

            return;
        }

        for (int row = 0; row < height; row++)
        {
            int sourceRow = sourceOffset + (row * sourceStride);
            int destinationRow = destinationOffset + (row * destinationStride);
            CopyRemainingSamples(ref sourceBase, sourceRow, ref destinationBase, destinationRow, 0, width);
        }
    }

    /// <summary>
    /// Copies a 16-bit sample rectangle into the CDEF working plane.
    /// </summary>
    /// <param name="source">The source sample plane.</param>
    /// <param name="sourceOffset">The offset of the rectangle's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The destination CDEF working plane.</param>
    /// <param name="destinationOffset">The offset of the rectangle's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="width">The rectangle width in samples.</param>
    /// <param name="height">The rectangle height in samples.</param>
    public static void CopyPlane(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        Span<ushort> destination,
        int destinationOffset,
        int destinationStride,
        int width,
        int height)
    {
        for (int row = 0; row < height; row++)
        {
            ReadOnlySpan<ushort> sourceRow = source.Slice(sourceOffset + (row * sourceStride), width);

            sourceRow.CopyTo(destination.Slice(destinationOffset + (row * destinationStride), width));
        }
    }

    /// <summary>
    /// Finds the dominant direction of an 8x8 luma block and its directional variance.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="coefficientShift">The number of bits above the eight-bit analysis precision.</param>
    /// <param name="variance">Receives the variance difference between the selected and orthogonal directions.</param>
    /// <returns>The zero-based AV1 direction index.</returns>
    public static int FindDirection(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        int coefficientShift,
        out int variance)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return FindDirectionVector(source, sourceOffset, sourceStride, coefficientShift, out variance);
        }

        return FindDirectionScalar(source, sourceOffset, sourceStride, coefficientShift, out variance);
    }

    /// <summary>
    /// Finds the dominant directions and directional variances of two independent 8x8 luma blocks.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane containing both blocks.</param>
    /// <param name="firstSourceOffset">The offset of the first block's top-left sample.</param>
    /// <param name="secondSourceOffset">The offset of the second block's top-left sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="coefficientShift">The number of bits above the eight-bit analysis precision.</param>
    /// <param name="firstDirection">Receives the first block's zero-based AV1 direction index.</param>
    /// <param name="firstVariance">Receives the first block's directional variance.</param>
    /// <param name="secondDirection">Receives the second block's zero-based AV1 direction index.</param>
    /// <param name="secondVariance">Receives the second block's directional variance.</param>
    public static void FindDirections(
        ReadOnlySpan<ushort> source,
        int firstSourceOffset,
        int secondSourceOffset,
        int sourceStride,
        int coefficientShift,
        out int firstDirection,
        out int firstVariance,
        out int secondDirection,
        out int secondVariance)
    {
        if (Avx2.IsSupported)
        {
            FindDirectionsVector(
                source,
                firstSourceOffset,
                secondSourceOffset,
                sourceStride,
                coefficientShift,
                out firstDirection,
                out firstVariance,
                out secondDirection,
                out secondVariance);

            return;
        }

        firstDirection = FindDirection(source, firstSourceOffset, sourceStride, coefficientShift, out firstVariance);
        secondDirection = FindDirection(source, secondSourceOffset, sourceStride, coefficientShift, out secondVariance);
    }

    /// <summary>
    /// Adjusts a luma primary strength according to the directional variance of its 8x8 block.
    /// </summary>
    /// <param name="strength">The bit-depth-scaled primary strength.</param>
    /// <param name="variance">The directional variance returned by <see cref="FindDirection"/>.</param>
    /// <returns>The variance-adjusted primary strength.</returns>
    public static int AdjustStrength(int strength, int variance)
    {
        int varianceClass = variance >> 6;
        int adjustment = varianceClass != 0 ? Math.Min(Av1Math.MostSignificantBit((uint)varianceClass), 12) : 0;
        return variance != 0 ? ((strength * (4 + adjustment)) + 8) >> 4 : 0;
    }

    /// <summary>
    /// Converts a luma direction to the matching chroma direction for asymmetric subsampling.
    /// </summary>
    /// <param name="direction">The zero-based luma direction index.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <returns>The direction index in the chroma sample grid.</returns>
    public static int ConvertDirection(int direction, int subsamplingX, int subsamplingY)
    {
        if (subsamplingX == subsamplingY)
        {
            return direction;
        }

        return subsamplingX != 0
            ? direction switch
            {
                0 => 7,
                1 => 0,
                2 => 2,
                3 => 4,
                4 => 5,
                _ => 6
            }
            : direction switch
            {
                0 => 1,
                1 or 2 or 3 => 2,
                4 => 3,
                5 => 4,
                6 => 6,
                _ => 0
            };
    }

    /// <summary>
    /// Filters one luma or chroma block into eight-bit sample storage.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The unbordered filtered destination plane.</param>
    /// <param name="destinationOffset">The offset of the block's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="primaryStrength">The bit-depth-scaled primary strength.</param>
    /// <param name="secondaryStrength">The bit-depth-scaled secondary strength.</param>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="primaryDamping">The damping value applied to primary taps.</param>
    /// <param name="secondaryDamping">The damping value applied to secondary taps.</param>
    /// <param name="coefficientShift">The number of bits above eight-bit sample precision.</param>
    /// <param name="blockWidth">The block width in plane samples.</param>
    /// <param name="blockHeight">The block height in plane samples.</param>
    public static void FilterBlock(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        Span<byte> destination,
        int destinationOffset,
        int destinationStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int primaryDamping,
        int secondaryDamping,
        int coefficientShift,
        int blockWidth,
        int blockHeight)
        => FilterBlock<byte, ByteOutputOperator>(
            source,
            sourceOffset,
            sourceStride,
            destination,
            destinationOffset,
            destinationStride,
            primaryStrength,
            secondaryStrength,
            direction,
            primaryDamping,
            secondaryDamping,
            coefficientShift,
            blockWidth,
            blockHeight);

    /// <summary>
    /// Filters one luma or chroma block into 16-bit sample storage.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The unbordered filtered destination plane.</param>
    /// <param name="destinationOffset">The offset of the block's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="primaryStrength">The bit-depth-scaled primary strength.</param>
    /// <param name="secondaryStrength">The bit-depth-scaled secondary strength.</param>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="primaryDamping">The damping value applied to primary taps.</param>
    /// <param name="secondaryDamping">The damping value applied to secondary taps.</param>
    /// <param name="coefficientShift">The number of bits above eight-bit sample precision.</param>
    /// <param name="blockWidth">The block width in plane samples.</param>
    /// <param name="blockHeight">The block height in plane samples.</param>
    public static void FilterBlock(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        Span<ushort> destination,
        int destinationOffset,
        int destinationStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int primaryDamping,
        int secondaryDamping,
        int coefficientShift,
        int blockWidth,
        int blockHeight)
        => FilterBlock<ushort, UInt16OutputOperator>(
            source,
            sourceOffset,
            sourceStride,
            destination,
            destinationOffset,
            destinationStride,
            primaryStrength,
            secondaryStrength,
            direction,
            primaryDamping,
            secondaryDamping,
            coefficientShift,
            blockWidth,
            blockHeight);

    /// <summary>
    /// Selects the packed CDEF kernel or its scalar fallback through one closed output operator.
    /// </summary>
    /// <typeparam name="TSample">The destination sample storage type.</typeparam>
    /// <typeparam name="TOutputOperator">The storage-specific output operator.</typeparam>
    /// <param name="source">The bordered, deblocked source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The unbordered filtered destination plane.</param>
    /// <param name="destinationOffset">The offset of the block's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="primaryStrength">The bit-depth-scaled primary strength.</param>
    /// <param name="secondaryStrength">The bit-depth-scaled secondary strength.</param>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="primaryDamping">The damping value applied to primary taps.</param>
    /// <param name="secondaryDamping">The damping value applied to secondary taps.</param>
    /// <param name="coefficientShift">The number of bits above eight-bit sample precision.</param>
    /// <param name="blockWidth">The block width in plane samples.</param>
    /// <param name="blockHeight">The block height in plane samples.</param>
    private static void FilterBlock<TSample, TOutputOperator>(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        Span<TSample> destination,
        int destinationOffset,
        int destinationStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int primaryDamping,
        int secondaryDamping,
        int coefficientShift,
        int blockWidth,
        int blockHeight)
        where TSample : unmanaged
        where TOutputOperator : struct, IOutputOperator<TSample>
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref TSample destinationBase = ref MemoryMarshal.GetReference(destination);

        // libaom selects one of four closed kernels from the two strength flags. The semantic operator makes the same
        // choice once per block so the JIT removes primary/secondary mode branches from every row and tap.
        if (primaryStrength != 0)
        {
            if (secondaryStrength != 0)
            {
                FilterBlock<TSample, TOutputOperator, PrimaryAndSecondaryFilterOperator>(
                    ref sourceBase,
                    sourceOffset,
                    sourceStride,
                    ref destinationBase,
                    destinationOffset,
                    destinationStride,
                    primaryStrength,
                    secondaryStrength,
                    direction,
                    primaryDamping,
                    secondaryDamping,
                    coefficientShift,
                    blockWidth,
                    blockHeight);
            }
            else
            {
                FilterBlock<TSample, TOutputOperator, PrimaryFilterOperator>(
                    ref sourceBase,
                    sourceOffset,
                    sourceStride,
                    ref destinationBase,
                    destinationOffset,
                    destinationStride,
                    primaryStrength,
                    secondaryStrength,
                    direction,
                    primaryDamping,
                    secondaryDamping,
                    coefficientShift,
                    blockWidth,
                    blockHeight);
            }

            return;
        }

        if (secondaryStrength != 0)
        {
            FilterBlock<TSample, TOutputOperator, SecondaryFilterOperator>(
                ref sourceBase,
                sourceOffset,
                sourceStride,
                ref destinationBase,
                destinationOffset,
                destinationStride,
                primaryStrength,
                secondaryStrength,
                direction,
                primaryDamping,
                secondaryDamping,
                coefficientShift,
                blockWidth,
                blockHeight);

            return;
        }

        FilterBlock<TSample, TOutputOperator, CopyFilterOperator>(
            ref sourceBase,
            sourceOffset,
            sourceStride,
            ref destinationBase,
            destinationOffset,
            destinationStride,
            primaryStrength,
            secondaryStrength,
            direction,
            primaryDamping,
            secondaryDamping,
            coefficientShift,
            blockWidth,
            blockHeight);
    }

    /// <summary>
    /// Selects the packed or scalar implementation of one closed filter kernel.
    /// </summary>
    /// <typeparam name="TSample">The destination sample storage type.</typeparam>
    /// <typeparam name="TOutputOperator">The storage-specific output operator.</typeparam>
    /// <typeparam name="TFilterOperator">The enabled directional-tap operator.</typeparam>
    /// <param name="source">The first element in the bordered source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The first element in the destination plane.</param>
    /// <param name="destinationOffset">The offset of the block's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="primaryStrength">The bit-depth-scaled primary strength.</param>
    /// <param name="secondaryStrength">The bit-depth-scaled secondary strength.</param>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="primaryDamping">The damping value applied to primary taps.</param>
    /// <param name="secondaryDamping">The damping value applied to secondary taps.</param>
    /// <param name="coefficientShift">The number of bits above eight-bit sample precision.</param>
    /// <param name="blockWidth">The block width in plane samples.</param>
    /// <param name="blockHeight">The block height in plane samples.</param>
    private static void FilterBlock<TSample, TOutputOperator, TFilterOperator>(
        ref ushort source,
        int sourceOffset,
        int sourceStride,
        ref TSample destination,
        int destinationOffset,
        int destinationStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int primaryDamping,
        int secondaryDamping,
        int coefficientShift,
        int blockWidth,
        int blockHeight)
        where TSample : unmanaged
        where TOutputOperator : struct, IOutputOperator<TSample>
        where TFilterOperator : struct, IFilterOperator
    {
        if (Avx2.IsSupported)
        {
            FilterBlockWideVector<TSample, TOutputOperator, TFilterOperator>(
                ref source,
                sourceOffset,
                sourceStride,
                ref destination,
                destinationOffset,
                destinationStride,
                primaryStrength,
                secondaryStrength,
                direction,
                primaryDamping,
                secondaryDamping,
                coefficientShift,
                blockWidth,
                blockHeight);

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            FilterBlockVector<TSample, TOutputOperator, TFilterOperator>(
                ref source,
                sourceOffset,
                sourceStride,
                ref destination,
                destinationOffset,
                destinationStride,
                primaryStrength,
                secondaryStrength,
                direction,
                primaryDamping,
                secondaryDamping,
                coefficientShift,
                blockWidth,
                blockHeight);

            return;
        }

        FilterBlockScalar<TSample, TOutputOperator, TFilterOperator>(
            ref source,
            sourceOffset,
            sourceStride,
            ref destination,
            destinationOffset,
            destinationStride,
            primaryStrength,
            secondaryStrength,
            direction,
            primaryDamping,
            secondaryDamping,
            coefficientShift,
            blockWidth,
            blockHeight);
    }

    /// <summary>
    /// Applies one packed CDEF kernel to two 8-wide rows or four 4-wide rows at a time.
    /// </summary>
    /// <typeparam name="TSample">The destination sample storage type.</typeparam>
    /// <typeparam name="TOutputOperator">The storage-specific output operator.</typeparam>
    /// <typeparam name="TFilterOperator">The enabled directional-tap operator.</typeparam>
    /// <param name="source">The first element in the bordered source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The first element in the destination plane.</param>
    /// <param name="destinationOffset">The offset of the block's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="primaryStrength">The bit-depth-scaled primary strength.</param>
    /// <param name="secondaryStrength">The bit-depth-scaled secondary strength.</param>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="primaryDamping">The damping value applied to primary taps.</param>
    /// <param name="secondaryDamping">The damping value applied to secondary taps.</param>
    /// <param name="coefficientShift">The number of bits above eight-bit sample precision.</param>
    /// <param name="blockWidth">The block width in plane samples.</param>
    /// <param name="blockHeight">The block height in plane samples.</param>
    private static void FilterBlockWideVector<TSample, TOutputOperator, TFilterOperator>(
        ref ushort source,
        int sourceOffset,
        int sourceStride,
        ref TSample destination,
        int destinationOffset,
        int destinationStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int primaryDamping,
        int secondaryDamping,
        int coefficientShift,
        int blockWidth,
        int blockHeight)
        where TSample : unmanaged
        where TOutputOperator : struct, IOutputOperator<TSample>
        where TFilterOperator : struct, IFilterOperator
    {
        bool clippingRequired = TFilterOperator.EnablePrimary && TFilterOperator.EnableSecondary;
        int primaryDampingShift = TFilterOperator.EnablePrimary ? Math.Max(0, primaryDamping - Av1Math.MostSignificantBit((uint)primaryStrength)) : 0;
        int secondaryDampingShift = TFilterOperator.EnableSecondary ? Math.Max(0, secondaryDamping - Av1Math.MostSignificantBit((uint)secondaryStrength)) : 0;
        int primaryTapSet = (primaryStrength >> coefficientShift) & 1;
        int primaryNearOffset = TFilterOperator.EnablePrimary ? GetDirectionOffset(direction, 0, sourceStride) : 0;
        int primaryFarOffset = TFilterOperator.EnablePrimary ? GetDirectionOffset(direction, 1, sourceStride) : 0;
        int secondaryNearOffset0 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 2) & 7, 0, sourceStride) : 0;
        int secondaryFarOffset0 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 2) & 7, 1, sourceStride) : 0;
        int secondaryNearOffset1 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 6) & 7, 0, sourceStride) : 0;
        int secondaryFarOffset1 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 6) & 7, 1, sourceStride) : 0;
        Vector256<short> primaryNearWeight = Vector256.Create((short)(primaryTapSet == 0 ? 4 : 3));
        Vector256<short> primaryFarWeight = Vector256.Create((short)(primaryTapSet == 0 ? 2 : 3));
        Vector256<short> secondaryNearWeight = Vector256.Create((short)2);
        Vector256<short> secondaryFarWeight = Vector256.Create((short)1);
        Vector256<short> sentinel = Vector256.Create((short)VeryLarge);
        Vector256<short> zero = Vector256<short>.Zero;
        Vector256<short> rounding = Vector256.Create((short)8);
        Vector256<short> one = Vector256.Create((short)1);
        int rowsPerBatch = blockWidth == 8 ? 2 : 4;

        // The 256-bit lane layout follows libaom: two complete 8-wide rows, or four complete 4-wide rows. Directional
        // offsets therefore remain ordinary source offsets, while all constrain, weight, clip, and round operations
        // advance several output rows together without crossing a row boundary inside any 128-bit lane.
        for (int row = 0; row < blockHeight; row += rowsPerBatch)
        {
            int sourceIndex = sourceOffset + (row * sourceStride);
            Vector256<short> sample = LoadRows(ref source, sourceIndex, sourceStride, blockWidth);
            Vector256<short> sum = zero;
            Vector256<short> minimum = sample;
            Vector256<short> maximum = sample;

            for (int tap = 0; tap < 2; tap++)
            {
                if (TFilterOperator.EnablePrimary)
                {
                    int offset = tap == 0 ? primaryNearOffset : primaryFarOffset;
                    Vector256<short> neighbor0 = LoadRows(ref source, sourceIndex + offset, sourceStride, blockWidth);
                    Vector256<short> neighbor1 = LoadRows(ref source, sourceIndex - offset, sourceStride, blockWidth);
                    Vector256<short> constrained = Constrain(neighbor0, sample, primaryStrength, primaryDampingShift)
                        + Constrain(neighbor1, sample, primaryStrength, primaryDampingShift);

                    sum += constrained * (tap == 0 ? primaryNearWeight : primaryFarWeight);
                    if (clippingRequired)
                    {
                        minimum = Vector256.Min(minimum, Vector256.Min(neighbor0, neighbor1));
                        maximum = MaximumIgnoringSentinel(maximum, neighbor0, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor1, sentinel);
                    }
                }

                if (TFilterOperator.EnableSecondary)
                {
                    int offset0 = tap == 0 ? secondaryNearOffset0 : secondaryFarOffset0;
                    int offset1 = tap == 0 ? secondaryNearOffset1 : secondaryFarOffset1;
                    Vector256<short> neighbor0 = LoadRows(ref source, sourceIndex + offset0, sourceStride, blockWidth);
                    Vector256<short> neighbor1 = LoadRows(ref source, sourceIndex - offset0, sourceStride, blockWidth);
                    Vector256<short> neighbor2 = LoadRows(ref source, sourceIndex + offset1, sourceStride, blockWidth);
                    Vector256<short> neighbor3 = LoadRows(ref source, sourceIndex - offset1, sourceStride, blockWidth);
                    Vector256<short> constrained = Constrain(neighbor0, sample, secondaryStrength, secondaryDampingShift)
                        + Constrain(neighbor1, sample, secondaryStrength, secondaryDampingShift)
                        + Constrain(neighbor2, sample, secondaryStrength, secondaryDampingShift)
                        + Constrain(neighbor3, sample, secondaryStrength, secondaryDampingShift);

                    sum += constrained * (tap == 0 ? secondaryNearWeight : secondaryFarWeight);
                    if (clippingRequired)
                    {
                        minimum = Vector256.Min(minimum, Vector256.Min(Vector256.Min(neighbor0, neighbor1), Vector256.Min(neighbor2, neighbor3)));
                        maximum = MaximumIgnoringSentinel(maximum, neighbor0, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor1, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor2, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor3, sentinel);
                    }
                }
            }

            Vector256<short> correction = (sum >> 15) & one;
            Vector256<short> filtered = sample + ((sum + rounding - correction) >> 4);
            if (clippingRequired)
            {
                filtered = Vector256.Min(Vector256.Max(filtered, minimum), maximum);
            }

            StoreRows<TSample, TOutputOperator>(
                ref destination,
                destinationOffset + (row * destinationStride),
                destinationStride,
                filtered,
                blockWidth);
        }
    }

    /// <summary>
    /// Finds the dominant direction with eight signed 16-bit lanes representing one complete source row.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="coefficientShift">The number of bits above the eight-bit analysis precision.</param>
    /// <param name="variance">Receives the variance difference between the selected and orthogonal directions.</param>
    /// <returns>The zero-based AV1 direction index.</returns>
    private static int FindDirectionVector(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        int coefficientShift,
        out int variance)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        InlineArray8<Vector128<short>> lines = default;
        Vector128<short> analysisBias = Vector128.Create((short)128);

        for (int row = 0; row < 8; row++)
        {
            Vector128<ushort> samples = Vector128.LoadUnsafe(ref sourceBase, (nuint)(sourceOffset + (row * sourceStride)));

            // AV1 direction selection is defined in an eight-bit domain. The signed bias keeps every accumulated line sum
            // within Int16 while preserving identical direction and variance results for 8-, 10-, and 12-bit samples.
            lines[row] = (samples >> coefficientShift).AsInt16() - analysisBias;
        }

        // Vector lanes are written in memory order. These are the low-to-high forms of libaom's set-style constants.
        Vector128<int> foldWeights0 = Vector128.Create(840, 420, 280, 210);
        Vector128<int> foldWeights1 = Vector128.Create(168, 140, 120, 105);
        Vector128<int> diagonalWeights0 = Vector128.Create(0, 0, 420, 210);
        Vector128<int> diagonalWeights1 = Vector128.Create(140, 105, 105, 105);
        InlineArray8<int> costs = default;
        ref int costBase = ref costs[0];

        // The first pass evaluates directions 4..7. Rotating the block counter-clockwise lets the identical arithmetic
        // evaluate directions 0..3, exactly matching libaom's portable vector implementation.
        ComputeDirectionCosts(ref lines, foldWeights0, foldWeights1, diagonalWeights0, diagonalWeights1).StoreUnsafe(ref costBase, 4);
        ReverseTranspose(ref lines);
        ComputeDirectionCosts(ref lines, foldWeights0, foldWeights1, diagonalWeights0, diagonalWeights1).StoreUnsafe(ref costBase);

        return SelectDirection(ref costs, out variance);
    }

    /// <summary>
    /// Finds two dominant directions with each 128-bit lane of a 256-bit vector representing one independent block.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane containing both blocks.</param>
    /// <param name="firstSourceOffset">The offset of the first block's top-left sample.</param>
    /// <param name="secondSourceOffset">The offset of the second block's top-left sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="coefficientShift">The number of bits above the eight-bit analysis precision.</param>
    /// <param name="firstDirection">Receives the first block's zero-based AV1 direction index.</param>
    /// <param name="firstVariance">Receives the first block's directional variance.</param>
    /// <param name="secondDirection">Receives the second block's zero-based AV1 direction index.</param>
    /// <param name="secondVariance">Receives the second block's directional variance.</param>
    private static void FindDirectionsVector(
        ReadOnlySpan<ushort> source,
        int firstSourceOffset,
        int secondSourceOffset,
        int sourceStride,
        int coefficientShift,
        out int firstDirection,
        out int firstVariance,
        out int secondDirection,
        out int secondVariance)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        InlineArray8<Vector256<short>> lines = default;
        Vector256<short> analysisBias = Vector256.Create((short)128);

        for (int row = 0; row < 8; row++)
        {
            Vector128<ushort> first = Vector128.LoadUnsafe(ref sourceBase, (nuint)(firstSourceOffset + (row * sourceStride)));
            Vector128<ushort> second = Vector128.LoadUnsafe(ref sourceBase, (nuint)(secondSourceOffset + (row * sourceStride)));

            // Keeping one block in each 128-bit lane is the critical libaom layout: every byte shift, unpack, multiply,
            // and transpose remains lane-local while one AVX2 instruction advances both direction searches.
            lines[row] = (Vector256.Create(first, second) >> coefficientShift).AsInt16() - analysisBias;
        }

        Vector128<int> foldWeights0 = Vector128.Create(840, 420, 280, 210);
        Vector128<int> foldWeights1 = Vector128.Create(168, 140, 120, 105);
        Vector128<int> diagonalWeights0 = Vector128.Create(0, 0, 420, 210);
        Vector128<int> diagonalWeights1 = Vector128.Create(140, 105, 105, 105);
        Vector256<int> packedFoldWeights0 = Vector256.Create(foldWeights0, foldWeights0);
        Vector256<int> packedFoldWeights1 = Vector256.Create(foldWeights1, foldWeights1);
        Vector256<int> packedDiagonalWeights0 = Vector256.Create(diagonalWeights0, diagonalWeights0);
        Vector256<int> packedDiagonalWeights1 = Vector256.Create(diagonalWeights1, diagonalWeights1);
        InlineArray8<int> firstCosts = default;
        InlineArray8<int> secondCosts = default;
        ref int firstCostBase = ref firstCosts[0];
        ref int secondCostBase = ref secondCosts[0];

        Vector256<int> direction47 = ComputeDirectionCosts(
            ref lines,
            packedFoldWeights0,
            packedFoldWeights1,
            packedDiagonalWeights0,
            packedDiagonalWeights1);

        direction47.GetLower().StoreUnsafe(ref firstCostBase, 4);
        direction47.GetUpper().StoreUnsafe(ref secondCostBase, 4);
        ReverseTranspose(ref lines);
        Vector256<int> direction03 = ComputeDirectionCosts(
            ref lines,
            packedFoldWeights0,
            packedFoldWeights1,
            packedDiagonalWeights0,
            packedDiagonalWeights1);

        direction03.GetLower().StoreUnsafe(ref firstCostBase);
        direction03.GetUpper().StoreUnsafe(ref secondCostBase);
        firstDirection = SelectDirection(ref firstCosts, out firstVariance);
        secondDirection = SelectDirection(ref secondCosts, out secondVariance);
    }

    /// <summary>
    /// Computes four adjacent AV1 direction costs from eight packed source rows.
    /// </summary>
    /// <param name="lines">The eight signed, biased source rows.</param>
    /// <param name="foldWeights0">The line-length weights for the first four folded pairs.</param>
    /// <param name="foldWeights1">The line-length weights for the second four folded pairs.</param>
    /// <param name="diagonalWeights0">The first line-length weights for the shallow diagonal directions.</param>
    /// <param name="diagonalWeights1">The second line-length weights for the shallow diagonal directions.</param>
    /// <returns>The four costs ordered by increasing direction within the current orientation.</returns>
    private static Vector128<int> ComputeDirectionCosts(
        ref InlineArray8<Vector128<short>> lines,
        Vector128<int> foldWeights0,
        Vector128<int> foldWeights1,
        Vector128<int> diagonalWeights0,
        Vector128<int> diagonalWeights1)
    {
        // Byte-lane shifts move whole Int16 samples while inserting zeroes. Each shifted row therefore lands in the
        // vector lanes for one geometric line without gathers or a per-block partial-sum buffer.
        Vector128<short> partial4A = Vector128_.ShiftLeftBytesInVector(lines[0].AsByte(), 14).AsInt16();
        Vector128<short> partial4B = Vector128_.ShiftRightBytesInVector(lines[0].AsByte(), 2).AsInt16();
        partial4A += Vector128_.ShiftLeftBytesInVector(lines[1].AsByte(), 12).AsInt16();
        partial4B += Vector128_.ShiftRightBytesInVector(lines[1].AsByte(), 4).AsInt16();
        Vector128<short> pair = lines[0] + lines[1];
        Vector128<short> partial5A = Vector128_.ShiftLeftBytesInVector(pair.AsByte(), 10).AsInt16();
        Vector128<short> partial5B = Vector128_.ShiftRightBytesInVector(pair.AsByte(), 6).AsInt16();
        Vector128<short> partial7A = Vector128_.ShiftLeftBytesInVector(pair.AsByte(), 4).AsInt16();
        Vector128<short> partial7B = Vector128_.ShiftRightBytesInVector(pair.AsByte(), 12).AsInt16();
        Vector128<short> partial6 = pair;

        partial4A += Vector128_.ShiftLeftBytesInVector(lines[2].AsByte(), 10).AsInt16();
        partial4B += Vector128_.ShiftRightBytesInVector(lines[2].AsByte(), 6).AsInt16();
        partial4A += Vector128_.ShiftLeftBytesInVector(lines[3].AsByte(), 8).AsInt16();
        partial4B += Vector128_.ShiftRightBytesInVector(lines[3].AsByte(), 8).AsInt16();
        pair = lines[2] + lines[3];
        partial5A += Vector128_.ShiftLeftBytesInVector(pair.AsByte(), 8).AsInt16();
        partial5B += Vector128_.ShiftRightBytesInVector(pair.AsByte(), 8).AsInt16();
        partial7A += Vector128_.ShiftLeftBytesInVector(pair.AsByte(), 6).AsInt16();
        partial7B += Vector128_.ShiftRightBytesInVector(pair.AsByte(), 10).AsInt16();
        partial6 += pair;

        partial4A += Vector128_.ShiftLeftBytesInVector(lines[4].AsByte(), 6).AsInt16();
        partial4B += Vector128_.ShiftRightBytesInVector(lines[4].AsByte(), 10).AsInt16();
        partial4A += Vector128_.ShiftLeftBytesInVector(lines[5].AsByte(), 4).AsInt16();
        partial4B += Vector128_.ShiftRightBytesInVector(lines[5].AsByte(), 12).AsInt16();
        pair = lines[4] + lines[5];
        partial5A += Vector128_.ShiftLeftBytesInVector(pair.AsByte(), 6).AsInt16();
        partial5B += Vector128_.ShiftRightBytesInVector(pair.AsByte(), 10).AsInt16();
        partial7A += Vector128_.ShiftLeftBytesInVector(pair.AsByte(), 8).AsInt16();
        partial7B += Vector128_.ShiftRightBytesInVector(pair.AsByte(), 8).AsInt16();
        partial6 += pair;

        partial4A += Vector128_.ShiftLeftBytesInVector(lines[6].AsByte(), 2).AsInt16();
        partial4B += Vector128_.ShiftRightBytesInVector(lines[6].AsByte(), 14).AsInt16();
        partial4A += lines[7];
        pair = lines[6] + lines[7];
        partial5A += Vector128_.ShiftLeftBytesInVector(pair.AsByte(), 4).AsInt16();
        partial5B += Vector128_.ShiftRightBytesInVector(pair.AsByte(), 12).AsInt16();
        partial7A += Vector128_.ShiftLeftBytesInVector(pair.AsByte(), 10).AsInt16();
        partial7B += Vector128_.ShiftRightBytesInVector(pair.AsByte(), 6).AsInt16();
        partial6 += pair;

        Vector128<int> partial4Cost = FoldDirectionPartials(partial4A, partial4B, foldWeights0, foldWeights1);
        Vector128<int> partial5Cost = FoldDirectionPartials(partial5A, partial5B, diagonalWeights0, diagonalWeights1);
        Vector128<int> partial7Cost = FoldDirectionPartials(partial7A, partial7B, diagonalWeights0, diagonalWeights1);
        Vector128<int> partial6Cost = Vector128_.MultiplyAddAdjacent(partial6, partial6) * Vector128.Create(105);
        return HorizontalSumFour(partial4Cost, partial5Cost, partial6Cost, partial7Cost);
    }

    /// <summary>
    /// Computes four adjacent AV1 direction costs for two blocks packed into independent 128-bit lanes.
    /// </summary>
    /// <param name="lines">The eight signed, biased source rows for both blocks.</param>
    /// <param name="foldWeights0">The line-length weights for the first four folded pairs.</param>
    /// <param name="foldWeights1">The line-length weights for the second four folded pairs.</param>
    /// <param name="diagonalWeights0">The first line-length weights for the shallow diagonal directions.</param>
    /// <param name="diagonalWeights1">The second line-length weights for the shallow diagonal directions.</param>
    /// <returns>The four costs per block ordered by increasing direction within the current orientation.</returns>
    private static Vector256<int> ComputeDirectionCosts(
        ref InlineArray8<Vector256<short>> lines,
        Vector256<int> foldWeights0,
        Vector256<int> foldWeights1,
        Vector256<int> diagonalWeights0,
        Vector256<int> diagonalWeights1)
    {
        Vector256<short> partial4A = Avx2.ShiftLeftLogical128BitLane(lines[0].AsByte(), 14).AsInt16();
        Vector256<short> partial4B = Avx2.ShiftRightLogical128BitLane(lines[0].AsByte(), 2).AsInt16();
        partial4A += Avx2.ShiftLeftLogical128BitLane(lines[1].AsByte(), 12).AsInt16();
        partial4B += Avx2.ShiftRightLogical128BitLane(lines[1].AsByte(), 4).AsInt16();
        Vector256<short> pair = lines[0] + lines[1];
        Vector256<short> partial5A = Avx2.ShiftLeftLogical128BitLane(pair.AsByte(), 10).AsInt16();
        Vector256<short> partial5B = Avx2.ShiftRightLogical128BitLane(pair.AsByte(), 6).AsInt16();
        Vector256<short> partial7A = Avx2.ShiftLeftLogical128BitLane(pair.AsByte(), 4).AsInt16();
        Vector256<short> partial7B = Avx2.ShiftRightLogical128BitLane(pair.AsByte(), 12).AsInt16();
        Vector256<short> partial6 = pair;

        partial4A += Avx2.ShiftLeftLogical128BitLane(lines[2].AsByte(), 10).AsInt16();
        partial4B += Avx2.ShiftRightLogical128BitLane(lines[2].AsByte(), 6).AsInt16();
        partial4A += Avx2.ShiftLeftLogical128BitLane(lines[3].AsByte(), 8).AsInt16();
        partial4B += Avx2.ShiftRightLogical128BitLane(lines[3].AsByte(), 8).AsInt16();
        pair = lines[2] + lines[3];
        partial5A += Avx2.ShiftLeftLogical128BitLane(pair.AsByte(), 8).AsInt16();
        partial5B += Avx2.ShiftRightLogical128BitLane(pair.AsByte(), 8).AsInt16();
        partial7A += Avx2.ShiftLeftLogical128BitLane(pair.AsByte(), 6).AsInt16();
        partial7B += Avx2.ShiftRightLogical128BitLane(pair.AsByte(), 10).AsInt16();
        partial6 += pair;

        partial4A += Avx2.ShiftLeftLogical128BitLane(lines[4].AsByte(), 6).AsInt16();
        partial4B += Avx2.ShiftRightLogical128BitLane(lines[4].AsByte(), 10).AsInt16();
        partial4A += Avx2.ShiftLeftLogical128BitLane(lines[5].AsByte(), 4).AsInt16();
        partial4B += Avx2.ShiftRightLogical128BitLane(lines[5].AsByte(), 12).AsInt16();
        pair = lines[4] + lines[5];
        partial5A += Avx2.ShiftLeftLogical128BitLane(pair.AsByte(), 6).AsInt16();
        partial5B += Avx2.ShiftRightLogical128BitLane(pair.AsByte(), 10).AsInt16();
        partial7A += Avx2.ShiftLeftLogical128BitLane(pair.AsByte(), 8).AsInt16();
        partial7B += Avx2.ShiftRightLogical128BitLane(pair.AsByte(), 8).AsInt16();
        partial6 += pair;

        partial4A += Avx2.ShiftLeftLogical128BitLane(lines[6].AsByte(), 2).AsInt16();
        partial4B += Avx2.ShiftRightLogical128BitLane(lines[6].AsByte(), 14).AsInt16();
        partial4A += lines[7];
        pair = lines[6] + lines[7];
        partial5A += Avx2.ShiftLeftLogical128BitLane(pair.AsByte(), 4).AsInt16();
        partial5B += Avx2.ShiftRightLogical128BitLane(pair.AsByte(), 12).AsInt16();
        partial7A += Avx2.ShiftLeftLogical128BitLane(pair.AsByte(), 10).AsInt16();
        partial7B += Avx2.ShiftRightLogical128BitLane(pair.AsByte(), 6).AsInt16();
        partial6 += pair;

        Vector256<int> partial4Cost = FoldDirectionPartials(partial4A, partial4B, foldWeights0, foldWeights1);
        Vector256<int> partial5Cost = FoldDirectionPartials(partial5A, partial5B, diagonalWeights0, diagonalWeights1);
        Vector256<int> partial7Cost = FoldDirectionPartials(partial7A, partial7B, diagonalWeights0, diagonalWeights1);
        Vector256<int> partial6Cost = Avx2.MultiplyAddAdjacent(partial6, partial6) * Vector256.Create(105);
        return HorizontalSumFour(partial4Cost, partial5Cost, partial6Cost, partial7Cost);
    }

    /// <summary>
    /// Squares, weights, and combines the two halves of one set of directional line sums.
    /// </summary>
    /// <param name="partialA">The first eight line sums.</param>
    /// <param name="partialB">The remaining seven line sums followed by zero.</param>
    /// <param name="weights0">The first four line-length weights.</param>
    /// <param name="weights1">The second four line-length weights.</param>
    /// <returns>Four packed weighted partial costs.</returns>
    private static Vector128<int> FoldDirectionPartials(
        Vector128<short> partialA,
        Vector128<short> partialB,
        Vector128<int> weights0,
        Vector128<int> weights1)
    {
        // Reversal aligns equally long lines. Interleaving then gives MultiplyAddAdjacent the [x,y] pairs whose
        // squared magnitudes share one line-length weight, including the unpaired centre line with an inserted zero.
        partialB = Vector128.ShuffleNative(partialB, Vector128.Create((short)6, 5, 4, 3, 2, 1, 0, 7));
        Vector128<short> originalA = partialA;
        partialA = Vector128_.UnpackLow(partialA, partialB);
        partialB = Vector128_.UnpackHigh(originalA, partialB);
        Vector128<int> lower = Vector128_.MultiplyAddAdjacent(partialA, partialA) * weights0;
        Vector128<int> upper = Vector128_.MultiplyAddAdjacent(partialB, partialB) * weights1;
        return lower + upper;
    }

    /// <summary>
    /// Squares, weights, and combines directional line sums for two independent packed blocks.
    /// </summary>
    /// <param name="partialA">The first eight line sums in each 128-bit lane.</param>
    /// <param name="partialB">The remaining seven line sums followed by zero in each 128-bit lane.</param>
    /// <param name="weights0">The first four line-length weights in each 128-bit lane.</param>
    /// <param name="weights1">The second four line-length weights in each 128-bit lane.</param>
    /// <returns>Four packed weighted partial costs per block.</returns>
    private static Vector256<int> FoldDirectionPartials(
        Vector256<short> partialA,
        Vector256<short> partialB,
        Vector256<int> weights0,
        Vector256<int> weights1)
    {
        Vector128<byte> laneShuffle = Vector128.Create((byte)12, 13, 10, 11, 8, 9, 6, 7, 4, 5, 2, 3, 0, 1, 14, 15);
        partialB = Avx2.Shuffle(partialB.AsByte(), Vector256.Create(laneShuffle, laneShuffle)).AsInt16();
        Vector256<short> originalA = partialA;
        partialA = Avx2.UnpackLow(partialA, partialB);
        partialB = Avx2.UnpackHigh(originalA, partialB);
        Vector256<int> lower = Avx2.MultiplyAddAdjacent(partialA, partialA) * weights0;
        Vector256<int> upper = Avx2.MultiplyAddAdjacent(partialB, partialB) * weights1;
        return lower + upper;
    }

    /// <summary>
    /// Horizontally reduces four cost vectors into four direction costs.
    /// </summary>
    /// <param name="cost0">The first direction's four partial costs.</param>
    /// <param name="cost1">The second direction's four partial costs.</param>
    /// <param name="cost2">The third direction's four partial costs.</param>
    /// <param name="cost3">The fourth direction's four partial costs.</param>
    /// <returns>The four horizontally reduced costs.</returns>
    private static Vector128<int> HorizontalSumFour(
        Vector128<int> cost0,
        Vector128<int> cost1,
        Vector128<int> cost2,
        Vector128<int> cost3)
    {
        Vector128<int> pair01Lower = Vector128_.UnpackLow(cost0, cost1);
        Vector128<int> pair23Lower = Vector128_.UnpackLow(cost2, cost3);
        Vector128<int> pair01Upper = Vector128_.UnpackHigh(cost0, cost1);
        Vector128<int> pair23Upper = Vector128_.UnpackHigh(cost2, cost3);
        Vector128<int> quad0 = Vector128_.UnpackLow(pair01Lower.AsInt64(), pair23Lower.AsInt64()).AsInt32();
        Vector128<int> quad1 = Vector128_.UnpackHigh(pair01Lower.AsInt64(), pair23Lower.AsInt64()).AsInt32();
        Vector128<int> quad2 = Vector128_.UnpackLow(pair01Upper.AsInt64(), pair23Upper.AsInt64()).AsInt32();
        Vector128<int> quad3 = Vector128_.UnpackHigh(pair01Upper.AsInt64(), pair23Upper.AsInt64()).AsInt32();
        return (quad0 + quad1) + (quad2 + quad3);
    }

    /// <summary>
    /// Horizontally reduces four cost vectors independently within both 128-bit lanes.
    /// </summary>
    /// <param name="cost0">The first direction's four partial costs per block.</param>
    /// <param name="cost1">The second direction's four partial costs per block.</param>
    /// <param name="cost2">The third direction's four partial costs per block.</param>
    /// <param name="cost3">The fourth direction's four partial costs per block.</param>
    /// <returns>The four horizontally reduced costs per block.</returns>
    private static Vector256<int> HorizontalSumFour(
        Vector256<int> cost0,
        Vector256<int> cost1,
        Vector256<int> cost2,
        Vector256<int> cost3)
    {
        Vector256<int> pair01Lower = Avx2.UnpackLow(cost0, cost1);
        Vector256<int> pair23Lower = Avx2.UnpackLow(cost2, cost3);
        Vector256<int> pair01Upper = Avx2.UnpackHigh(cost0, cost1);
        Vector256<int> pair23Upper = Avx2.UnpackHigh(cost2, cost3);
        Vector256<int> quad0 = Avx2.UnpackLow(pair01Lower.AsInt64(), pair23Lower.AsInt64()).AsInt32();
        Vector256<int> quad1 = Avx2.UnpackHigh(pair01Lower.AsInt64(), pair23Lower.AsInt64()).AsInt32();
        Vector256<int> quad2 = Avx2.UnpackLow(pair01Upper.AsInt64(), pair23Upper.AsInt64()).AsInt32();
        Vector256<int> quad3 = Avx2.UnpackHigh(pair01Upper.AsInt64(), pair23Upper.AsInt64()).AsInt32();
        return (quad0 + quad1) + (quad2 + quad3);
    }

    /// <summary>
    /// Rotates an 8x8 packed sample block counter-clockwise by transposing and reversing its rows.
    /// </summary>
    /// <param name="lines">The source rows, replaced by the rotated rows.</param>
    private static void ReverseTranspose(ref InlineArray8<Vector128<short>> lines)
    {
        Vector128<short> pair01Lower = Vector128_.UnpackLow(lines[0], lines[1]);
        Vector128<short> pair23Lower = Vector128_.UnpackLow(lines[2], lines[3]);
        Vector128<short> pair01Upper = Vector128_.UnpackHigh(lines[0], lines[1]);
        Vector128<short> pair23Upper = Vector128_.UnpackHigh(lines[2], lines[3]);
        Vector128<short> pair45Lower = Vector128_.UnpackLow(lines[4], lines[5]);
        Vector128<short> pair67Lower = Vector128_.UnpackLow(lines[6], lines[7]);
        Vector128<short> pair45Upper = Vector128_.UnpackHigh(lines[4], lines[5]);
        Vector128<short> pair67Upper = Vector128_.UnpackHigh(lines[6], lines[7]);
        Vector128<int> quad03Lower = Vector128_.UnpackLow(pair01Lower.AsInt32(), pair23Lower.AsInt32());
        Vector128<int> quad47Lower = Vector128_.UnpackLow(pair45Lower.AsInt32(), pair67Lower.AsInt32());
        Vector128<int> quad03Middle = Vector128_.UnpackHigh(pair01Lower.AsInt32(), pair23Lower.AsInt32());
        Vector128<int> quad47Middle = Vector128_.UnpackHigh(pair45Lower.AsInt32(), pair67Lower.AsInt32());
        Vector128<int> quad03Upper = Vector128_.UnpackLow(pair01Upper.AsInt32(), pair23Upper.AsInt32());
        Vector128<int> quad47Upper = Vector128_.UnpackLow(pair45Upper.AsInt32(), pair67Upper.AsInt32());
        Vector128<int> quad03Highest = Vector128_.UnpackHigh(pair01Upper.AsInt32(), pair23Upper.AsInt32());
        Vector128<int> quad47Highest = Vector128_.UnpackHigh(pair45Upper.AsInt32(), pair67Upper.AsInt32());

        // Writing in reverse order turns the normal transpose into the counter-clockwise rotation required to reuse
        // the same four-direction cost kernel for the orthogonal half of the search.
        lines[7] = Vector128_.UnpackLow(quad03Lower.AsInt64(), quad47Lower.AsInt64()).AsInt16();
        lines[6] = Vector128_.UnpackHigh(quad03Lower.AsInt64(), quad47Lower.AsInt64()).AsInt16();
        lines[5] = Vector128_.UnpackLow(quad03Middle.AsInt64(), quad47Middle.AsInt64()).AsInt16();
        lines[4] = Vector128_.UnpackHigh(quad03Middle.AsInt64(), quad47Middle.AsInt64()).AsInt16();
        lines[3] = Vector128_.UnpackLow(quad03Upper.AsInt64(), quad47Upper.AsInt64()).AsInt16();
        lines[2] = Vector128_.UnpackHigh(quad03Upper.AsInt64(), quad47Upper.AsInt64()).AsInt16();
        lines[1] = Vector128_.UnpackLow(quad03Highest.AsInt64(), quad47Highest.AsInt64()).AsInt16();
        lines[0] = Vector128_.UnpackHigh(quad03Highest.AsInt64(), quad47Highest.AsInt64()).AsInt16();
    }

    /// <summary>
    /// Rotates two packed 8x8 sample blocks counter-clockwise within their independent 128-bit lanes.
    /// </summary>
    /// <param name="lines">The source rows for both blocks, replaced by the rotated rows.</param>
    private static void ReverseTranspose(ref InlineArray8<Vector256<short>> lines)
    {
        Vector256<short> pair01Lower = Avx2.UnpackLow(lines[0], lines[1]);
        Vector256<short> pair23Lower = Avx2.UnpackLow(lines[2], lines[3]);
        Vector256<short> pair01Upper = Avx2.UnpackHigh(lines[0], lines[1]);
        Vector256<short> pair23Upper = Avx2.UnpackHigh(lines[2], lines[3]);
        Vector256<short> pair45Lower = Avx2.UnpackLow(lines[4], lines[5]);
        Vector256<short> pair67Lower = Avx2.UnpackLow(lines[6], lines[7]);
        Vector256<short> pair45Upper = Avx2.UnpackHigh(lines[4], lines[5]);
        Vector256<short> pair67Upper = Avx2.UnpackHigh(lines[6], lines[7]);
        Vector256<int> quad03Lower = Avx2.UnpackLow(pair01Lower.AsInt32(), pair23Lower.AsInt32());
        Vector256<int> quad47Lower = Avx2.UnpackLow(pair45Lower.AsInt32(), pair67Lower.AsInt32());
        Vector256<int> quad03Middle = Avx2.UnpackHigh(pair01Lower.AsInt32(), pair23Lower.AsInt32());
        Vector256<int> quad47Middle = Avx2.UnpackHigh(pair45Lower.AsInt32(), pair67Lower.AsInt32());
        Vector256<int> quad03Upper = Avx2.UnpackLow(pair01Upper.AsInt32(), pair23Upper.AsInt32());
        Vector256<int> quad47Upper = Avx2.UnpackLow(pair45Upper.AsInt32(), pair67Upper.AsInt32());
        Vector256<int> quad03Highest = Avx2.UnpackHigh(pair01Upper.AsInt32(), pair23Upper.AsInt32());
        Vector256<int> quad47Highest = Avx2.UnpackHigh(pair45Upper.AsInt32(), pair67Upper.AsInt32());

        lines[7] = Avx2.UnpackLow(quad03Lower.AsInt64(), quad47Lower.AsInt64()).AsInt16();
        lines[6] = Avx2.UnpackHigh(quad03Lower.AsInt64(), quad47Lower.AsInt64()).AsInt16();
        lines[5] = Avx2.UnpackLow(quad03Middle.AsInt64(), quad47Middle.AsInt64()).AsInt16();
        lines[4] = Avx2.UnpackHigh(quad03Middle.AsInt64(), quad47Middle.AsInt64()).AsInt16();
        lines[3] = Avx2.UnpackLow(quad03Upper.AsInt64(), quad47Upper.AsInt64()).AsInt16();
        lines[2] = Avx2.UnpackHigh(quad03Upper.AsInt64(), quad47Upper.AsInt64()).AsInt16();
        lines[1] = Avx2.UnpackLow(quad03Highest.AsInt64(), quad47Highest.AsInt64()).AsInt16();
        lines[0] = Avx2.UnpackHigh(quad03Highest.AsInt64(), quad47Highest.AsInt64()).AsInt16();
    }

    /// <summary>
    /// Finds the dominant direction without vector instructions or a frame-block partial-sum buffer.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="coefficientShift">The number of bits above the eight-bit analysis precision.</param>
    /// <param name="variance">Receives the variance difference between the selected and orthogonal directions.</param>
    /// <returns>The zero-based AV1 direction index.</returns>
    private static int FindDirectionScalar(
        ReadOnlySpan<ushort> source,
        int sourceOffset,
        int sourceStride,
        int coefficientShift,
        out int variance)
    {
        InlineArray8<int> costs = default;
        InlineArray16<int> partials = default;
        Span<int> lineSums = partials;

        // The fallback reuses one 15-line accumulator for each direction. Re-reading the 8x8 block is preferable to
        // reserving and clearing the old 120-element partial table on every block when SIMD is explicitly disabled.
        for (int direction = 0; direction < 8; direction++)
        {
            lineSums.Clear();
            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    int value = (source[sourceOffset + (row * sourceStride) + column] >> coefficientShift) - 128;
                    lineSums[GetDirectionLine(direction, row, column)] += value;
                }
            }

            costs[direction] = CalculateDirectionCost(direction, lineSums);
        }

        return SelectDirection(ref costs, out variance);
    }

    /// <summary>
    /// Selects the first maximum direction cost and derives its orthogonal variance difference.
    /// </summary>
    /// <param name="costs">The eight direction costs in ascending direction order.</param>
    /// <param name="variance">Receives the scaled difference from the selected direction's orthogonal cost.</param>
    /// <returns>The zero-based AV1 direction index.</returns>
    private static int SelectDirection(ref InlineArray8<int> costs, out int variance)
    {
        int bestCost = 0;
        int bestDirection = 0;
        for (int direction = 0; direction < 8; direction++)
        {
            if (costs[direction] > bestCost)
            {
                bestCost = costs[direction];
                bestDirection = direction;
            }
        }

        // All directions omit the same sum-of-squares term, so the scaled difference from the orthogonal cost is the
        // directional variance consumed by the luma strength adjustment.
        variance = (bestCost - costs[(bestDirection + 4) & 7]) >> 10;
        return bestDirection;
    }

    /// <summary>
    /// Maps one source coordinate to its line in the requested AV1 direction.
    /// </summary>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="row">The source row within the 8x8 block.</param>
    /// <param name="column">The source column within the 8x8 block.</param>
    /// <returns>The zero-based line index.</returns>
    private static int GetDirectionLine(int direction, int row, int column) => direction switch
    {
        0 => row + column,
        1 => row + (column >> 1),
        2 => row,
        3 => 3 + row - (column >> 1),
        4 => 7 + row - column,
        5 => 3 - (row >> 1) + column,
        6 => column,
        _ => (row >> 1) + column
    };

    /// <summary>
    /// Computes one weighted direction cost from its accumulated line sums.
    /// </summary>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="lineSums">The accumulated line sums.</param>
    /// <returns>The weighted direction cost.</returns>
    private static int CalculateDirectionCost(int direction, ReadOnlySpan<int> lineSums)
    {
        int cost = 0;
        if (direction is 2 or 6)
        {
            for (int line = 0; line < 8; line++)
            {
                cost += lineSums[line] * lineSums[line];
            }

            return cost * 105;
        }

        if ((direction & 1) == 0)
        {
            for (int line = 0; line < 7; line++)
            {
                int mirroredLine = 14 - line;
                cost += ((lineSums[line] * lineSums[line]) + (lineSums[mirroredLine] * lineSums[mirroredLine])) * GetDivisionMultiplier(line + 1);
            }

            return cost + (lineSums[7] * lineSums[7] * 105);
        }

        for (int line = 3; line < 8; line++)
        {
            cost += lineSums[line] * lineSums[line];
        }

        cost *= 105;
        for (int line = 0; line < 3; line++)
        {
            int mirroredLine = 10 - line;
            cost += ((lineSums[line] * lineSums[line]) + (lineSums[mirroredLine] * lineSums[mirroredLine])) * GetDivisionMultiplier((line * 2) + 2);
        }

        return cost;
    }

    /// <summary>
    /// Gets the common multiple used to compare lines of different lengths without division.
    /// </summary>
    /// <param name="lineLength">The number of samples contributing to the line.</param>
    /// <returns>The multiplier equal to 840 divided by the line length.</returns>
    private static int GetDivisionMultiplier(int lineLength) => lineLength switch
    {
        1 => 840,
        2 => 420,
        3 => 280,
        4 => 210,
        5 => 168,
        6 => 140,
        7 => 120,
        _ => 105
    };

    /// <summary>
    /// Applies one packed CDEF kernel with each signed 16-bit lane representing a column in the current output row.
    /// </summary>
    /// <typeparam name="TSample">The destination sample storage type.</typeparam>
    /// <typeparam name="TOutputOperator">The storage-specific output operator.</typeparam>
    /// <typeparam name="TFilterOperator">The enabled directional-tap operator.</typeparam>
    /// <param name="source">The first element in the bordered source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The first element in the destination plane.</param>
    /// <param name="destinationOffset">The offset of the block's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="primaryStrength">The bit-depth-scaled primary strength.</param>
    /// <param name="secondaryStrength">The bit-depth-scaled secondary strength.</param>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="primaryDamping">The damping value applied to primary taps.</param>
    /// <param name="secondaryDamping">The damping value applied to secondary taps.</param>
    /// <param name="coefficientShift">The number of bits above eight-bit sample precision.</param>
    /// <param name="blockWidth">The block width in plane samples.</param>
    /// <param name="blockHeight">The block height in plane samples.</param>
    private static void FilterBlockVector<TSample, TOutputOperator, TFilterOperator>(
        ref ushort source,
        int sourceOffset,
        int sourceStride,
        ref TSample destination,
        int destinationOffset,
        int destinationStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int primaryDamping,
        int secondaryDamping,
        int coefficientShift,
        int blockWidth,
        int blockHeight)
        where TSample : unmanaged
        where TOutputOperator : struct, IOutputOperator<TSample>
        where TFilterOperator : struct, IFilterOperator
    {
        bool clippingRequired = TFilterOperator.EnablePrimary && TFilterOperator.EnableSecondary;
        int primaryDampingShift = TFilterOperator.EnablePrimary ? Math.Max(0, primaryDamping - Av1Math.MostSignificantBit((uint)primaryStrength)) : 0;
        int secondaryDampingShift = TFilterOperator.EnableSecondary ? Math.Max(0, secondaryDamping - Av1Math.MostSignificantBit((uint)secondaryStrength)) : 0;
        int primaryTapSet = (primaryStrength >> coefficientShift) & 1;
        int primaryNearOffset = TFilterOperator.EnablePrimary ? GetDirectionOffset(direction, 0, sourceStride) : 0;
        int primaryFarOffset = TFilterOperator.EnablePrimary ? GetDirectionOffset(direction, 1, sourceStride) : 0;
        int secondaryNearOffset0 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 2) & 7, 0, sourceStride) : 0;
        int secondaryFarOffset0 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 2) & 7, 1, sourceStride) : 0;
        int secondaryNearOffset1 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 6) & 7, 0, sourceStride) : 0;
        int secondaryFarOffset1 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 6) & 7, 1, sourceStride) : 0;
        Vector128<short> primaryNearWeight = Vector128.Create((short)(primaryTapSet == 0 ? 4 : 3));
        Vector128<short> primaryFarWeight = Vector128.Create((short)(primaryTapSet == 0 ? 2 : 3));
        Vector128<short> secondaryNearWeight = Vector128.Create((short)2);
        Vector128<short> secondaryFarWeight = Vector128.Create((short)1);
        Vector128<short> sentinel = Vector128.Create((short)VeryLarge);
        Vector128<short> zero = Vector128<short>.Zero;
        Vector128<short> rounding = Vector128.Create((short)8);
        Vector128<short> one = Vector128.Create((short)1);

        for (int row = 0; row < blockHeight; row++)
        {
            int sourceIndex = sourceOffset + (row * sourceStride);
            Vector128<short> sample = LoadSamples(ref source, sourceIndex, blockWidth);
            Vector128<short> sum = zero;
            Vector128<short> minimum = sample;
            Vector128<short> maximum = sample;

            for (int tap = 0; tap < 2; tap++)
            {
                if (TFilterOperator.EnablePrimary)
                {
                    int offset = tap == 0 ? primaryNearOffset : primaryFarOffset;
                    Vector128<short> neighbor0 = LoadSamples(ref source, sourceIndex + offset, blockWidth);
                    Vector128<short> neighbor1 = LoadSamples(ref source, sourceIndex - offset, blockWidth);
                    Vector128<short> constrained = Constrain(neighbor0, sample, primaryStrength, primaryDampingShift)
                        + Constrain(neighbor1, sample, primaryStrength, primaryDampingShift);

                    sum += constrained * (tap == 0 ? primaryNearWeight : primaryFarWeight);
                    if (clippingRequired)
                    {
                        minimum = Vector128.Min(minimum, Vector128.Min(neighbor0, neighbor1));
                        maximum = MaximumIgnoringSentinel(maximum, neighbor0, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor1, sentinel);
                    }
                }

                if (TFilterOperator.EnableSecondary)
                {
                    int offset0 = tap == 0 ? secondaryNearOffset0 : secondaryFarOffset0;
                    int offset1 = tap == 0 ? secondaryNearOffset1 : secondaryFarOffset1;
                    Vector128<short> neighbor0 = LoadSamples(ref source, sourceIndex + offset0, blockWidth);
                    Vector128<short> neighbor1 = LoadSamples(ref source, sourceIndex - offset0, blockWidth);
                    Vector128<short> neighbor2 = LoadSamples(ref source, sourceIndex + offset1, blockWidth);
                    Vector128<short> neighbor3 = LoadSamples(ref source, sourceIndex - offset1, blockWidth);
                    Vector128<short> constrained = Constrain(neighbor0, sample, secondaryStrength, secondaryDampingShift)
                        + Constrain(neighbor1, sample, secondaryStrength, secondaryDampingShift)
                        + Constrain(neighbor2, sample, secondaryStrength, secondaryDampingShift)
                        + Constrain(neighbor3, sample, secondaryStrength, secondaryDampingShift);

                    sum += constrained * (tap == 0 ? secondaryNearWeight : secondaryFarWeight);
                    if (clippingRequired)
                    {
                        minimum = Vector128.Min(minimum, Vector128.Min(Vector128.Min(neighbor0, neighbor1), Vector128.Min(neighbor2, neighbor3)));
                        maximum = MaximumIgnoringSentinel(maximum, neighbor0, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor1, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor2, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor3, sentinel);
                    }
                }
            }

            // The sign lane contributes the one-unit correction required by AV1's asymmetric rounding for negative sums.
            Vector128<short> correction = (sum >> 15) & one;
            Vector128<short> filtered = sample + ((sum + rounding - correction) >> 4);
            if (clippingRequired)
            {
                filtered = Vector128.Min(Vector128.Max(filtered, minimum), maximum);
            }

            TOutputOperator.StoreVector(ref destination, destinationOffset + (row * destinationStride), filtered, blockWidth);
        }
    }

    /// <summary>
    /// Applies the scalar CDEF fallback to one block.
    /// </summary>
    /// <typeparam name="TSample">The destination sample storage type.</typeparam>
    /// <typeparam name="TOutputOperator">The storage-specific output operator.</typeparam>
    /// <typeparam name="TFilterOperator">The enabled directional-tap operator.</typeparam>
    /// <param name="source">The first element in the bordered source plane.</param>
    /// <param name="sourceOffset">The offset of the block's top-left source sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="destination">The first element in the destination plane.</param>
    /// <param name="destinationOffset">The offset of the block's top-left destination sample.</param>
    /// <param name="destinationStride">The number of samples between adjacent destination rows.</param>
    /// <param name="primaryStrength">The bit-depth-scaled primary strength.</param>
    /// <param name="secondaryStrength">The bit-depth-scaled secondary strength.</param>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="primaryDamping">The damping value applied to primary taps.</param>
    /// <param name="secondaryDamping">The damping value applied to secondary taps.</param>
    /// <param name="coefficientShift">The number of bits above eight-bit sample precision.</param>
    /// <param name="blockWidth">The block width in plane samples.</param>
    /// <param name="blockHeight">The block height in plane samples.</param>
    private static void FilterBlockScalar<TSample, TOutputOperator, TFilterOperator>(
        ref ushort source,
        int sourceOffset,
        int sourceStride,
        ref TSample destination,
        int destinationOffset,
        int destinationStride,
        int primaryStrength,
        int secondaryStrength,
        int direction,
        int primaryDamping,
        int secondaryDamping,
        int coefficientShift,
        int blockWidth,
        int blockHeight)
        where TSample : unmanaged
        where TOutputOperator : struct, IOutputOperator<TSample>
        where TFilterOperator : struct, IFilterOperator
    {
        bool clippingRequired = TFilterOperator.EnablePrimary && TFilterOperator.EnableSecondary;
        int primaryDampingShift = TFilterOperator.EnablePrimary ? Math.Max(0, primaryDamping - Av1Math.MostSignificantBit((uint)primaryStrength)) : 0;
        int secondaryDampingShift = TFilterOperator.EnableSecondary ? Math.Max(0, secondaryDamping - Av1Math.MostSignificantBit((uint)secondaryStrength)) : 0;
        int primaryTapSet = (primaryStrength >> coefficientShift) & 1;
        int primaryNearOffset = TFilterOperator.EnablePrimary ? GetDirectionOffset(direction, 0, sourceStride) : 0;
        int primaryFarOffset = TFilterOperator.EnablePrimary ? GetDirectionOffset(direction, 1, sourceStride) : 0;
        int secondaryNearOffset0 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 2) & 7, 0, sourceStride) : 0;
        int secondaryFarOffset0 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 2) & 7, 1, sourceStride) : 0;
        int secondaryNearOffset1 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 6) & 7, 0, sourceStride) : 0;
        int secondaryFarOffset1 = TFilterOperator.EnableSecondary ? GetDirectionOffset((direction + 6) & 7, 1, sourceStride) : 0;

        for (int row = 0; row < blockHeight; row++)
        {
            for (int column = 0; column < blockWidth; column++)
            {
                int sourceIndex = sourceOffset + (row * sourceStride) + column;
                int sample = Unsafe.Add(ref source, sourceIndex);
                int sum = 0;
                int minimum = sample;
                int maximum = sample;

                for (int tap = 0; tap < 2; tap++)
                {
                    if (TFilterOperator.EnablePrimary)
                    {
                        int offset = tap == 0 ? primaryNearOffset : primaryFarOffset;
                        int neighbor0 = Unsafe.Add(ref source, sourceIndex + offset);
                        int neighbor1 = Unsafe.Add(ref source, sourceIndex - offset);
                        int weight = primaryTapSet == 0 ? (tap == 0 ? 4 : 2) : 3;
                        sum += weight * Constrain(neighbor0 - sample, primaryStrength, primaryDampingShift);
                        sum += weight * Constrain(neighbor1 - sample, primaryStrength, primaryDampingShift);

                        if (clippingRequired)
                        {
                            maximum = neighbor0 != VeryLarge ? Math.Max(maximum, neighbor0) : maximum;
                            maximum = neighbor1 != VeryLarge ? Math.Max(maximum, neighbor1) : maximum;
                            minimum = Math.Min(minimum, Math.Min(neighbor0, neighbor1));
                        }
                    }

                    if (TFilterOperator.EnableSecondary)
                    {
                        int offset0 = tap == 0 ? secondaryNearOffset0 : secondaryFarOffset0;
                        int offset1 = tap == 0 ? secondaryNearOffset1 : secondaryFarOffset1;
                        int neighbor0 = Unsafe.Add(ref source, sourceIndex + offset0);
                        int neighbor1 = Unsafe.Add(ref source, sourceIndex - offset0);
                        int neighbor2 = Unsafe.Add(ref source, sourceIndex + offset1);
                        int neighbor3 = Unsafe.Add(ref source, sourceIndex - offset1);
                        int weight = tap == 0 ? 2 : 1;
                        sum += weight * Constrain(neighbor0 - sample, secondaryStrength, secondaryDampingShift);
                        sum += weight * Constrain(neighbor1 - sample, secondaryStrength, secondaryDampingShift);
                        sum += weight * Constrain(neighbor2 - sample, secondaryStrength, secondaryDampingShift);
                        sum += weight * Constrain(neighbor3 - sample, secondaryStrength, secondaryDampingShift);

                        if (clippingRequired)
                        {
                            maximum = neighbor0 != VeryLarge ? Math.Max(maximum, neighbor0) : maximum;
                            maximum = neighbor1 != VeryLarge ? Math.Max(maximum, neighbor1) : maximum;
                            maximum = neighbor2 != VeryLarge ? Math.Max(maximum, neighbor2) : maximum;
                            maximum = neighbor3 != VeryLarge ? Math.Max(maximum, neighbor3) : maximum;
                            minimum = Math.Min(minimum, Math.Min(Math.Min(neighbor0, neighbor1), Math.Min(neighbor2, neighbor3)));
                        }
                    }
                }

                int filtered = sample + ((8 + sum - (sum < 0 ? 1 : 0)) >> 4);
                TOutputOperator.StoreScalar(
                    ref destination,
                    destinationOffset + (row * destinationStride) + column,
                    clippingRequired ? Av1Math.Clip3(minimum, maximum, filtered) : filtered);
            }
        }
    }

    /// <summary>
    /// Loads four or eight contiguous source samples into signed 16-bit lanes.
    /// </summary>
    /// <param name="source">The first element in the bordered source plane.</param>
    /// <param name="offset">The offset of the first sample to load.</param>
    /// <param name="count">The number of valid samples.</param>
    /// <returns>The samples in the low vector lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> LoadSamples(ref ushort source, int offset, int count)
    {
        if (count == 8)
        {
            return Vector128.LoadUnsafe(ref source, (nuint)offset).AsInt16();
        }

        // Four-wide chroma blocks load exactly 64 bits so the final block never reads beyond its two-sample sentinel border.
        ref byte sourceBytes = ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref source, offset));
        return Vector128.Create(Unsafe.ReadUnaligned<ulong>(ref sourceBytes), 0UL).AsInt16();
    }

    /// <summary>
    /// Widens the remaining samples in one eight-bit source row into the CDEF working plane.
    /// </summary>
    /// <param name="source">The first element in the source plane.</param>
    /// <param name="sourceRow">The offset of the first source sample in the row.</param>
    /// <param name="destination">The first element in the destination plane.</param>
    /// <param name="destinationRow">The offset of the first destination sample in the row.</param>
    /// <param name="column">The first column not already processed by a wider vector path.</param>
    /// <param name="width">The rectangle width in samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyRemainingSamples(
        ref byte source,
        int sourceRow,
        ref ushort destination,
        int destinationRow,
        int column,
        int width)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= width - Vector64<byte>.Count; column += Vector64<byte>.Count)
            {
                ref byte sourceBytes = ref Unsafe.Add(ref source, sourceRow + column);
                ulong packed = Unsafe.ReadUnaligned<ulong>(ref sourceBytes);
                Vector128<ushort> widened = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
                widened.StoreUnsafe(ref destination, (nuint)(destinationRow + column));
            }
        }

        for (; column < width; column++)
        {
            Unsafe.Add(ref destination, destinationRow + column) = Unsafe.Add(ref source, sourceRow + column);
        }
    }

    /// <summary>
    /// Loads two 8-wide rows or four 4-wide rows into the independent row groups of one 256-bit vector.
    /// </summary>
    /// <param name="source">The first element in the bordered source plane.</param>
    /// <param name="offset">The offset of the first sample to load.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <param name="width">The number of valid samples in each row.</param>
    /// <returns>The packed source rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> LoadRows(ref ushort source, int offset, int stride, int width)
    {
        if (width == 8)
        {
            Vector128<short> firstRow = Vector128.LoadUnsafe(ref source, (nuint)offset).AsInt16();
            Vector128<short> secondRow = Vector128.LoadUnsafe(ref source, (nuint)(offset + stride)).AsInt16();
            return Vector256.Create(firstRow, secondRow);
        }

        // Four-row batches use one 64-bit load per row. Pairing two rows in each 128-bit lane preserves the exact row
        // boundaries required by libaom's lane-local shifts while avoiding reads beyond the frame sentinel border.
        Vector64<short> row0 = LoadSamples(ref source, offset, 4).GetLower();
        Vector64<short> row1 = LoadSamples(ref source, offset + stride, 4).GetLower();
        Vector64<short> row2 = LoadSamples(ref source, offset + (2 * stride), 4).GetLower();
        Vector64<short> row3 = LoadSamples(ref source, offset + (3 * stride), 4).GetLower();
        return Vector256.Create(Vector128.Create(row0, row1), Vector128.Create(row2, row3));
    }

    /// <summary>
    /// Stores two 8-wide rows or four 4-wide rows from one packed result vector.
    /// </summary>
    /// <typeparam name="TSample">The destination sample storage type.</typeparam>
    /// <typeparam name="TOutputOperator">The storage-specific output operator.</typeparam>
    /// <param name="destination">The first element in the destination plane.</param>
    /// <param name="offset">The offset of the first row to write.</param>
    /// <param name="stride">The number of samples between adjacent destination rows.</param>
    /// <param name="value">The packed filtered rows.</param>
    /// <param name="width">The number of valid samples in each row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRows<TSample, TOutputOperator>(
        ref TSample destination,
        int offset,
        int stride,
        Vector256<short> value,
        int width)
        where TSample : unmanaged
        where TOutputOperator : struct, IOutputOperator<TSample>
    {
        Vector128<short> lower = value.GetLower();
        Vector128<short> upper = value.GetUpper();
        if (width == 8)
        {
            TOutputOperator.StoreVector(ref destination, offset, lower, 8);
            TOutputOperator.StoreVector(ref destination, offset + stride, upper, 8);
            return;
        }

        TOutputOperator.StoreVector(ref destination, offset, lower, 4);
        TOutputOperator.StoreVector(ref destination, offset + stride, Vector128.Create(lower.GetUpper(), Vector64<short>.Zero), 4);
        TOutputOperator.StoreVector(ref destination, offset + (2 * stride), upper, 4);
        TOutputOperator.StoreVector(ref destination, offset + (3 * stride), Vector128.Create(upper.GetUpper(), Vector64<short>.Zero), 4);
    }

    /// <summary>
    /// Limits packed neighbor differences according to one filter strength and its pre-adjusted damping shift.
    /// </summary>
    /// <param name="neighbor">The neighboring samples.</param>
    /// <param name="sample">The current samples.</param>
    /// <param name="threshold">The bit-depth-scaled filter strength.</param>
    /// <param name="dampingShift">The damping shift after accounting for the threshold magnitude.</param>
    /// <returns>The signed constrained differences.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> Constrain(Vector128<short> neighbor, Vector128<short> sample, int threshold, int dampingShift)
    {
        Vector128<short> difference = neighbor - sample;
        Vector128<short> sign = difference >> 15;
        Vector128<ushort> magnitude = Vector128.Abs(difference).AsUInt16();
        Vector128<ushort> remaining = Vector128.SubtractSaturate(Vector128.Create((ushort)threshold), magnitude >> dampingShift);
        Vector128<short> constrained = Vector128.Min(magnitude, remaining).AsInt16();

        // Adding the all-bits sign before XOR reproduces sign(value) * magnitude without a branch or a multiply.
        return (constrained + sign) ^ sign;
    }

    /// <summary>
    /// Limits packed neighbor differences for several independent output rows.
    /// </summary>
    /// <param name="neighbor">The neighboring samples.</param>
    /// <param name="sample">The current samples.</param>
    /// <param name="threshold">The bit-depth-scaled filter strength.</param>
    /// <param name="dampingShift">The damping shift after accounting for the threshold magnitude.</param>
    /// <returns>The signed constrained differences.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> Constrain(Vector256<short> neighbor, Vector256<short> sample, int threshold, int dampingShift)
    {
        Vector256<short> difference = neighbor - sample;
        Vector256<short> sign = difference >> 15;
        Vector256<ushort> magnitude = Vector256.Abs(difference).AsUInt16();
        Vector256<ushort> remaining = Vector256.SubtractSaturate(Vector256.Create((ushort)threshold), magnitude >> dampingShift);
        Vector256<short> constrained = Vector256.Min(magnitude, remaining).AsInt16();
        return (constrained + sign) ^ sign;
    }

    /// <summary>
    /// Updates a packed maximum while treating unavailable-neighbor sentinels as zero.
    /// </summary>
    /// <param name="maximum">The current per-lane maximum.</param>
    /// <param name="candidate">The candidate neighboring samples.</param>
    /// <param name="sentinel">The unavailable-neighbor sentinel in every lane.</param>
    /// <returns>The updated per-lane maximum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> MaximumIgnoringSentinel(Vector128<short> maximum, Vector128<short> candidate, Vector128<short> sentinel)
    {
        Vector128<short> available = Vector128.ConditionalSelect(Vector128.Equals(candidate, sentinel), Vector128<short>.Zero, candidate);
        return Vector128.Max(maximum, available);
    }

    /// <summary>
    /// Updates packed row maxima while treating unavailable-neighbor sentinels as zero.
    /// </summary>
    /// <param name="maximum">The current per-lane maximum.</param>
    /// <param name="candidate">The candidate neighboring samples.</param>
    /// <param name="sentinel">The unavailable-neighbor sentinel in every lane.</param>
    /// <returns>The updated per-lane maximum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> MaximumIgnoringSentinel(Vector256<short> maximum, Vector256<short> candidate, Vector256<short> sentinel)
    {
        Vector256<short> available = Vector256.ConditionalSelect(Vector256.Equals(candidate, sentinel), Vector256<short>.Zero, candidate);
        return Vector256.Max(maximum, available);
    }

    /// <summary>
    /// Limits one scalar neighbor difference according to a filter strength and damping value.
    /// </summary>
    /// <param name="difference">The signed difference from the current sample.</param>
    /// <param name="threshold">The bit-depth-scaled filter strength.</param>
    /// <param name="dampingShift">The damping shift after accounting for the threshold magnitude.</param>
    /// <returns>The signed constrained difference.</returns>
    private static int Constrain(int difference, int threshold, int dampingShift)
    {
        int magnitude = Math.Abs(difference);
        int constrained = Av1Math.Clip3(0, magnitude, threshold - (magnitude >> dampingShift));
        return difference < 0 ? -constrained : constrained;
    }

    /// <summary>
    /// Converts a direction and tap number to a signed plane-buffer offset.
    /// </summary>
    /// <param name="direction">The zero-based AV1 direction index.</param>
    /// <param name="tap">The zero-based distance index.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <returns>The signed sample offset.</returns>
    private static int GetDirectionOffset(int direction, int tap, int stride) => direction switch
    {
        0 => tap == 0 ? -stride + 1 : (-2 * stride) + 2,
        1 => tap == 0 ? 1 : -stride + 2,
        2 => tap == 0 ? 1 : 2,
        3 => tap == 0 ? 1 : stride + 2,
        4 => tap == 0 ? stride + 1 : (2 * stride) + 2,
        5 => tap == 0 ? stride : (2 * stride) + 1,
        6 => tap == 0 ? stride : 2 * stride,
        _ => tap == 0 ? stride : (2 * stride) - 1
    };

    /// <summary>
    /// Enables both directional tap groups and their combined clipping rule.
    /// </summary>
    private readonly struct PrimaryAndSecondaryFilterOperator : IFilterOperator
    {
        /// <inheritdoc/>
        public static bool EnablePrimary => true;

        /// <inheritdoc/>
        public static bool EnableSecondary => true;
    }

    /// <summary>
    /// Enables only the primary directional taps.
    /// </summary>
    private readonly struct PrimaryFilterOperator : IFilterOperator
    {
        /// <inheritdoc/>
        public static bool EnablePrimary => true;

        /// <inheritdoc/>
        public static bool EnableSecondary => false;
    }

    /// <summary>
    /// Enables only the secondary off-axis taps.
    /// </summary>
    private readonly struct SecondaryFilterOperator : IFilterOperator
    {
        /// <inheritdoc/>
        public static bool EnablePrimary => false;

        /// <inheritdoc/>
        public static bool EnableSecondary => true;
    }

    /// <summary>
    /// Disables both tap groups so the source block is copied unchanged.
    /// </summary>
    private readonly struct CopyFilterOperator : IFilterOperator
    {
        /// <inheritdoc/>
        public static bool EnablePrimary => false;

        /// <inheritdoc/>
        public static bool EnableSecondary => false;
    }

    /// <summary>
    /// Writes filtered samples to eight-bit plane storage.
    /// </summary>
    private readonly struct ByteOutputOperator : IOutputOperator<byte>
    {
        /// <inheritdoc/>
        public static void StoreVector(ref byte destination, int offset, Vector128<short> value, int count)
        {
            Vector64<byte> packed = Vector128.Narrow(value.AsUInt16(), Vector128<ushort>.Zero).GetLower();
            ref byte output = ref Unsafe.Add(ref destination, offset);
            if (count == 8)
            {
                packed.StoreUnsafe(ref output);
            }
            else
            {
                Unsafe.WriteUnaligned(ref output, packed.AsUInt32().ToScalar());
            }
        }

        /// <inheritdoc/>
        public static void StoreScalar(ref byte destination, int offset, int value) => Unsafe.Add(ref destination, offset) = (byte)value;
    }

    /// <summary>
    /// Writes filtered samples to 16-bit plane storage.
    /// </summary>
    private readonly struct UInt16OutputOperator : IOutputOperator<ushort>
    {
        /// <inheritdoc/>
        public static void StoreVector(ref ushort destination, int offset, Vector128<short> value, int count)
        {
            ref ushort output = ref Unsafe.Add(ref destination, offset);
            if (count == 8)
            {
                value.AsUInt16().StoreUnsafe(ref output);
            }
            else
            {
                ref byte outputBytes = ref Unsafe.As<ushort, byte>(ref output);
                Unsafe.WriteUnaligned(ref outputBytes, value.AsUInt64().GetLower().ToScalar());
            }
        }

        /// <inheritdoc/>
        public static void StoreScalar(ref ushort destination, int offset, int value) => Unsafe.Add(ref destination, offset) = (ushort)value;
    }
}
