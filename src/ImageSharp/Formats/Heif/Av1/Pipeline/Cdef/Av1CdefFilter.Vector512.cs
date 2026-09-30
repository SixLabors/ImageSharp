// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;

/// <content>
/// Holds the 512-bit forms of the direction search and the packed-row filter. Each 128-bit lane keeps the layout of
/// the narrower kernels: one 8x8 block per lane for the direction search, and one 8-wide row or two 4-wide rows per
/// lane for the filter, so every shift, unpack and multiply stays lane-local and the results are identical.
/// </content>
internal static partial class Av1CdefFilter
{
    /// <summary>
    /// Finds the dominant directions and directional variances of a list of 8x8 luma blocks.
    /// Reference: cdef_find_dir(), which calls cdef_find_dir_dual() for each pair of blocks.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane containing every block.</param>
    /// <param name="sourceOffsets">The offset of each block's top-left sample.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="coefficientShift">The number of bits above the eight-bit analysis precision.</param>
    /// <param name="directions">Receives each block's zero-based AV1 direction index.</param>
    /// <param name="variances">Receives each block's directional variance.</param>
    public static void FindDirections(
        ReadOnlySpan<ushort> source,
        ReadOnlySpan<int> sourceOffsets,
        int sourceStride,
        int coefficientShift,
        Span<int> directions,
        Span<int> variances)
    {
        int count = sourceOffsets.Length;
        int index = 0;

        // Each width analyses as many independent blocks as it has 128-bit lanes, widest first. A block's
        // direction does not depend on its neighbours, so the grouping does not change any result.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; index + 4 <= count; index += 4)
            {
                FindDirectionsVector512(source, sourceOffsets.Slice(index, 4), sourceStride, coefficientShift, directions.Slice(index, 4), variances.Slice(index, 4));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index + 2 <= count; index += 2)
            {
                FindDirectionsVector(
                    source,
                    sourceOffsets[index],
                    sourceOffsets[index + 1],
                    sourceStride,
                    coefficientShift,
                    out directions[index],
                    out variances[index],
                    out directions[index + 1],
                    out variances[index + 1]);
            }
        }

        for (; index < count; index++)
        {
            directions[index] = FindDirection(source, sourceOffsets[index], sourceStride, coefficientShift, out variances[index]);
        }
    }

    /// <summary>
    /// Finds four dominant directions with each 128-bit lane of a 512-bit vector representing one independent block.
    /// </summary>
    /// <param name="source">The bordered, deblocked source plane containing the blocks.</param>
    /// <param name="sourceOffsets">The offsets of the four blocks' top-left samples.</param>
    /// <param name="sourceStride">The number of samples between adjacent source rows.</param>
    /// <param name="coefficientShift">The number of bits above the eight-bit analysis precision.</param>
    /// <param name="directions">Receives the four zero-based AV1 direction indices.</param>
    /// <param name="variances">Receives the four directional variances.</param>
    private static void FindDirectionsVector512(
        ReadOnlySpan<ushort> source,
        ReadOnlySpan<int> sourceOffsets,
        int sourceStride,
        int coefficientShift,
        Span<int> directions,
        Span<int> variances)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        InlineArray8<Vector512<short>> lines = default;
        Vector512<short> analysisBias = Vector512.Create((short)128);
        int offset0 = sourceOffsets[0];
        int offset1 = sourceOffsets[1];
        int offset2 = sourceOffsets[2];
        int offset3 = sourceOffsets[3];

        for (int row = 0; row < 8; row++)
        {
            int rowOffset = row * sourceStride;
            Vector128<ushort> block0 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(offset0 + rowOffset));
            Vector128<ushort> block1 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(offset1 + rowOffset));
            Vector128<ushort> block2 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(offset2 + rowOffset));
            Vector128<ushort> block3 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(offset3 + rowOffset));
            Vector512<ushort> packed = Vector512.Create(Vector256.Create(block0, block1), Vector256.Create(block2, block3));

            // The bias keeps every line sum within Int16, as in the narrower kernels.
            lines[row] = (packed >> coefficientShift).AsInt16() - analysisBias;
        }

        Vector128<int> foldWeights0 = Vector128.Create(840, 420, 280, 210);
        Vector128<int> foldWeights1 = Vector128.Create(168, 140, 120, 105);
        Vector128<int> diagonalWeights0 = Vector128.Create(0, 0, 420, 210);
        Vector128<int> diagonalWeights1 = Vector128.Create(140, 105, 105, 105);
        Vector512<int> packedFoldWeights0 = Vector512.Create(Vector256.Create(foldWeights0, foldWeights0), Vector256.Create(foldWeights0, foldWeights0));
        Vector512<int> packedFoldWeights1 = Vector512.Create(Vector256.Create(foldWeights1, foldWeights1), Vector256.Create(foldWeights1, foldWeights1));
        Vector512<int> packedDiagonalWeights0 = Vector512.Create(Vector256.Create(diagonalWeights0, diagonalWeights0), Vector256.Create(diagonalWeights0, diagonalWeights0));
        Vector512<int> packedDiagonalWeights1 = Vector512.Create(Vector256.Create(diagonalWeights1, diagonalWeights1), Vector256.Create(diagonalWeights1, diagonalWeights1));

        Vector512<int> direction47 = ComputeDirectionCosts(
            ref lines,
            packedFoldWeights0,
            packedFoldWeights1,
            packedDiagonalWeights0,
            packedDiagonalWeights1);

        ReverseTranspose(ref lines);
        Vector512<int> direction03 = ComputeDirectionCosts(
            ref lines,
            packedFoldWeights0,
            packedFoldWeights1,
            packedDiagonalWeights0,
            packedDiagonalWeights1);

        // 128-bit lane n holds block n's four costs. Directions 0..3 come from the rotated pass and 4..7 from the first.
        InlineArray16<int> lowerCosts = default;
        InlineArray16<int> upperCosts = default;
        direction03.StoreUnsafe(ref lowerCosts[0]);
        direction47.StoreUnsafe(ref upperCosts[0]);
        InlineArray8<int> costs = default;
        Span<int> blockCosts = costs;
        for (int block = 0; block < 4; block++)
        {
            ((ReadOnlySpan<int>)lowerCosts).Slice(block * 4, 4).CopyTo(blockCosts);
            ((ReadOnlySpan<int>)upperCosts).Slice(block * 4, 4).CopyTo(blockCosts[4..]);
            directions[block] = SelectDirection(ref costs, out variances[block]);
        }
    }

    /// <summary>
    /// Computes four adjacent AV1 direction costs for four blocks packed into independent 128-bit lanes.
    /// </summary>
    /// <param name="lines">The eight signed, biased source rows for the four blocks.</param>
    /// <param name="foldWeights0">The line-length weights for the first four folded pairs.</param>
    /// <param name="foldWeights1">The line-length weights for the second four folded pairs.</param>
    /// <param name="diagonalWeights0">The first line-length weights for the shallow diagonal directions.</param>
    /// <param name="diagonalWeights1">The second line-length weights for the shallow diagonal directions.</param>
    /// <returns>The four costs per block ordered by increasing direction within the current orientation.</returns>
    private static Vector512<int> ComputeDirectionCosts(
        ref InlineArray8<Vector512<short>> lines,
        Vector512<int> foldWeights0,
        Vector512<int> foldWeights1,
        Vector512<int> diagonalWeights0,
        Vector512<int> diagonalWeights1)
    {
        Vector512<short> partial4A = Vector512_.ShiftLeftBytesInLane(lines[0].AsByte(), 14).AsInt16();
        Vector512<short> partial4B = Vector512_.ShiftRightBytesInLane(lines[0].AsByte(), 2).AsInt16();
        partial4A += Vector512_.ShiftLeftBytesInLane(lines[1].AsByte(), 12).AsInt16();
        partial4B += Vector512_.ShiftRightBytesInLane(lines[1].AsByte(), 4).AsInt16();
        Vector512<short> pair = lines[0] + lines[1];
        Vector512<short> partial5A = Vector512_.ShiftLeftBytesInLane(pair.AsByte(), 10).AsInt16();
        Vector512<short> partial5B = Vector512_.ShiftRightBytesInLane(pair.AsByte(), 6).AsInt16();
        Vector512<short> partial7A = Vector512_.ShiftLeftBytesInLane(pair.AsByte(), 4).AsInt16();
        Vector512<short> partial7B = Vector512_.ShiftRightBytesInLane(pair.AsByte(), 12).AsInt16();
        Vector512<short> partial6 = pair;

        partial4A += Vector512_.ShiftLeftBytesInLane(lines[2].AsByte(), 10).AsInt16();
        partial4B += Vector512_.ShiftRightBytesInLane(lines[2].AsByte(), 6).AsInt16();
        partial4A += Vector512_.ShiftLeftBytesInLane(lines[3].AsByte(), 8).AsInt16();
        partial4B += Vector512_.ShiftRightBytesInLane(lines[3].AsByte(), 8).AsInt16();
        pair = lines[2] + lines[3];
        partial5A += Vector512_.ShiftLeftBytesInLane(pair.AsByte(), 8).AsInt16();
        partial5B += Vector512_.ShiftRightBytesInLane(pair.AsByte(), 8).AsInt16();
        partial7A += Vector512_.ShiftLeftBytesInLane(pair.AsByte(), 6).AsInt16();
        partial7B += Vector512_.ShiftRightBytesInLane(pair.AsByte(), 10).AsInt16();
        partial6 += pair;

        partial4A += Vector512_.ShiftLeftBytesInLane(lines[4].AsByte(), 6).AsInt16();
        partial4B += Vector512_.ShiftRightBytesInLane(lines[4].AsByte(), 10).AsInt16();
        partial4A += Vector512_.ShiftLeftBytesInLane(lines[5].AsByte(), 4).AsInt16();
        partial4B += Vector512_.ShiftRightBytesInLane(lines[5].AsByte(), 12).AsInt16();
        pair = lines[4] + lines[5];
        partial5A += Vector512_.ShiftLeftBytesInLane(pair.AsByte(), 6).AsInt16();
        partial5B += Vector512_.ShiftRightBytesInLane(pair.AsByte(), 10).AsInt16();
        partial7A += Vector512_.ShiftLeftBytesInLane(pair.AsByte(), 8).AsInt16();
        partial7B += Vector512_.ShiftRightBytesInLane(pair.AsByte(), 8).AsInt16();
        partial6 += pair;

        partial4A += Vector512_.ShiftLeftBytesInLane(lines[6].AsByte(), 2).AsInt16();
        partial4B += Vector512_.ShiftRightBytesInLane(lines[6].AsByte(), 14).AsInt16();
        partial4A += lines[7];
        pair = lines[6] + lines[7];
        partial5A += Vector512_.ShiftLeftBytesInLane(pair.AsByte(), 4).AsInt16();
        partial5B += Vector512_.ShiftRightBytesInLane(pair.AsByte(), 12).AsInt16();
        partial7A += Vector512_.ShiftLeftBytesInLane(pair.AsByte(), 10).AsInt16();
        partial7B += Vector512_.ShiftRightBytesInLane(pair.AsByte(), 6).AsInt16();
        partial6 += pair;

        Vector512<int> partial4Cost = FoldDirectionPartials(partial4A, partial4B, foldWeights0, foldWeights1);
        Vector512<int> partial5Cost = FoldDirectionPartials(partial5A, partial5B, diagonalWeights0, diagonalWeights1);
        Vector512<int> partial7Cost = FoldDirectionPartials(partial7A, partial7B, diagonalWeights0, diagonalWeights1);
        Vector512<int> partial6Cost = Vector512_.MultiplyAddAdjacent(partial6, partial6) * Vector512.Create(105);
        return HorizontalSumFour(partial4Cost, partial5Cost, partial6Cost, partial7Cost);
    }

    /// <summary>
    /// Squares, weights, and combines directional line sums for four independent packed blocks.
    /// </summary>
    /// <param name="partialA">The first eight line sums in each 128-bit lane.</param>
    /// <param name="partialB">The remaining seven line sums followed by zero in each 128-bit lane.</param>
    /// <param name="weights0">The first four line-length weights in each 128-bit lane.</param>
    /// <param name="weights1">The second four line-length weights in each 128-bit lane.</param>
    /// <returns>Four packed weighted partial costs per block.</returns>
    private static Vector512<int> FoldDirectionPartials(
        Vector512<short> partialA,
        Vector512<short> partialB,
        Vector512<int> weights0,
        Vector512<int> weights1)
    {
        Vector128<byte> laneShuffle = Vector128.Create((byte)12, 13, 10, 11, 8, 9, 6, 7, 4, 5, 2, 3, 0, 1, 14, 15);
        Vector256<byte> pairShuffle = Vector256.Create(laneShuffle, laneShuffle);
        partialB = Vector512_.ShufflePerLane(partialB.AsByte(), Vector512.Create(pairShuffle, pairShuffle)).AsInt16();
        Vector512<short> originalA = partialA;
        partialA = Vector512_.UnpackLow(partialA, partialB);
        partialB = Vector512_.UnpackHigh(originalA, partialB);
        Vector512<int> lower = Vector512_.MultiplyAddAdjacent(partialA, partialA) * weights0;
        Vector512<int> upper = Vector512_.MultiplyAddAdjacent(partialB, partialB) * weights1;
        return lower + upper;
    }

    /// <summary>
    /// Horizontally reduces four cost vectors independently within each 128-bit lane.
    /// </summary>
    /// <param name="cost0">The first direction's four partial costs per block.</param>
    /// <param name="cost1">The second direction's four partial costs per block.</param>
    /// <param name="cost2">The third direction's four partial costs per block.</param>
    /// <param name="cost3">The fourth direction's four partial costs per block.</param>
    /// <returns>The four horizontally reduced costs per block.</returns>
    private static Vector512<int> HorizontalSumFour(
        Vector512<int> cost0,
        Vector512<int> cost1,
        Vector512<int> cost2,
        Vector512<int> cost3)
    {
        Vector512<int> pair01Lower = Vector512_.UnpackLow(cost0, cost1);
        Vector512<int> pair23Lower = Vector512_.UnpackLow(cost2, cost3);
        Vector512<int> pair01Upper = Vector512_.UnpackHigh(cost0, cost1);
        Vector512<int> pair23Upper = Vector512_.UnpackHigh(cost2, cost3);
        Vector512<int> quad0 = Vector512_.UnpackLow(pair01Lower.AsInt64(), pair23Lower.AsInt64()).AsInt32();
        Vector512<int> quad1 = Vector512_.UnpackHigh(pair01Lower.AsInt64(), pair23Lower.AsInt64()).AsInt32();
        Vector512<int> quad2 = Vector512_.UnpackLow(pair01Upper.AsInt64(), pair23Upper.AsInt64()).AsInt32();
        Vector512<int> quad3 = Vector512_.UnpackHigh(pair01Upper.AsInt64(), pair23Upper.AsInt64()).AsInt32();
        return (quad0 + quad1) + (quad2 + quad3);
    }

    /// <summary>
    /// Rotates four packed 8x8 sample blocks counter-clockwise within their independent 128-bit lanes.
    /// </summary>
    /// <param name="lines">The source rows for the four blocks, replaced by the rotated rows.</param>
    private static void ReverseTranspose(ref InlineArray8<Vector512<short>> lines)
    {
        Vector512<short> pair01Lower = Vector512_.UnpackLow(lines[0], lines[1]);
        Vector512<short> pair23Lower = Vector512_.UnpackLow(lines[2], lines[3]);
        Vector512<short> pair01Upper = Vector512_.UnpackHigh(lines[0], lines[1]);
        Vector512<short> pair23Upper = Vector512_.UnpackHigh(lines[2], lines[3]);
        Vector512<short> pair45Lower = Vector512_.UnpackLow(lines[4], lines[5]);
        Vector512<short> pair67Lower = Vector512_.UnpackLow(lines[6], lines[7]);
        Vector512<short> pair45Upper = Vector512_.UnpackHigh(lines[4], lines[5]);
        Vector512<short> pair67Upper = Vector512_.UnpackHigh(lines[6], lines[7]);
        Vector512<int> quad03Lower = Vector512_.UnpackLow(pair01Lower.AsInt32(), pair23Lower.AsInt32());
        Vector512<int> quad47Lower = Vector512_.UnpackLow(pair45Lower.AsInt32(), pair67Lower.AsInt32());
        Vector512<int> quad03Middle = Vector512_.UnpackHigh(pair01Lower.AsInt32(), pair23Lower.AsInt32());
        Vector512<int> quad47Middle = Vector512_.UnpackHigh(pair45Lower.AsInt32(), pair67Lower.AsInt32());
        Vector512<int> quad03Upper = Vector512_.UnpackLow(pair01Upper.AsInt32(), pair23Upper.AsInt32());
        Vector512<int> quad47Upper = Vector512_.UnpackLow(pair45Upper.AsInt32(), pair67Upper.AsInt32());
        Vector512<int> quad03Highest = Vector512_.UnpackHigh(pair01Upper.AsInt32(), pair23Upper.AsInt32());
        Vector512<int> quad47Highest = Vector512_.UnpackHigh(pair45Upper.AsInt32(), pair67Upper.AsInt32());

        lines[7] = Vector512_.UnpackLow(quad03Lower.AsInt64(), quad47Lower.AsInt64()).AsInt16();
        lines[6] = Vector512_.UnpackHigh(quad03Lower.AsInt64(), quad47Lower.AsInt64()).AsInt16();
        lines[5] = Vector512_.UnpackLow(quad03Middle.AsInt64(), quad47Middle.AsInt64()).AsInt16();
        lines[4] = Vector512_.UnpackHigh(quad03Middle.AsInt64(), quad47Middle.AsInt64()).AsInt16();
        lines[3] = Vector512_.UnpackLow(quad03Upper.AsInt64(), quad47Upper.AsInt64()).AsInt16();
        lines[2] = Vector512_.UnpackHigh(quad03Upper.AsInt64(), quad47Upper.AsInt64()).AsInt16();
        lines[1] = Vector512_.UnpackLow(quad03Highest.AsInt64(), quad47Highest.AsInt64()).AsInt16();
        lines[0] = Vector512_.UnpackHigh(quad03Highest.AsInt64(), quad47Highest.AsInt64()).AsInt16();
    }

    /// <summary>
    /// Applies one packed CDEF kernel to four 8-wide rows or eight 4-wide rows at a time.
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
    /// <param name="blockHeight">The block height in plane samples, a multiple of the rows in one vector.</param>
    private static void FilterBlockVector512<TSample, TOutputOperator, TFilterOperator>(
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
        Vector512<short> primaryNearWeight = Vector512.Create((short)(primaryTapSet == 0 ? 4 : 3));
        Vector512<short> primaryFarWeight = Vector512.Create((short)(primaryTapSet == 0 ? 2 : 3));
        Vector512<short> secondaryNearWeight = Vector512.Create((short)2);
        Vector512<short> secondaryFarWeight = Vector512.Create((short)1);
        Vector512<short> sentinel = Vector512.Create((short)VeryLarge);
        Vector512<short> rounding = Vector512.Create((short)8);
        Vector512<short> one = Vector512.Create((short)1);
        int rowsPerBatch = blockWidth == 8 ? 4 : 8;

        // Each 256-bit half holds the rows of one 256-bit kernel step, so the directional offsets remain ordinary
        // source offsets and the arithmetic advances four 8-wide rows or eight 4-wide rows together.
        for (int row = 0; row < blockHeight; row += rowsPerBatch)
        {
            int sourceIndex = sourceOffset + (row * sourceStride);
            Vector512<short> sample = LoadRows512(ref source, sourceIndex, sourceStride, blockWidth);
            Vector512<short> sum = Vector512<short>.Zero;
            Vector512<short> minimum = sample;
            Vector512<short> maximum = sample;

            for (int tap = 0; tap < 2; tap++)
            {
                if (TFilterOperator.EnablePrimary)
                {
                    int offset = tap == 0 ? primaryNearOffset : primaryFarOffset;
                    Vector512<short> neighbor0 = LoadRows512(ref source, sourceIndex + offset, sourceStride, blockWidth);
                    Vector512<short> neighbor1 = LoadRows512(ref source, sourceIndex - offset, sourceStride, blockWidth);
                    Vector512<short> constrained = Constrain(neighbor0, sample, primaryStrength, primaryDampingShift)
                        + Constrain(neighbor1, sample, primaryStrength, primaryDampingShift);

                    sum += constrained * (tap == 0 ? primaryNearWeight : primaryFarWeight);
                    if (clippingRequired)
                    {
                        minimum = Vector512.Min(minimum, Vector512.Min(neighbor0, neighbor1));
                        maximum = MaximumIgnoringSentinel(maximum, neighbor0, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor1, sentinel);
                    }
                }

                if (TFilterOperator.EnableSecondary)
                {
                    int offset0 = tap == 0 ? secondaryNearOffset0 : secondaryFarOffset0;
                    int offset1 = tap == 0 ? secondaryNearOffset1 : secondaryFarOffset1;
                    Vector512<short> neighbor0 = LoadRows512(ref source, sourceIndex + offset0, sourceStride, blockWidth);
                    Vector512<short> neighbor1 = LoadRows512(ref source, sourceIndex - offset0, sourceStride, blockWidth);
                    Vector512<short> neighbor2 = LoadRows512(ref source, sourceIndex + offset1, sourceStride, blockWidth);
                    Vector512<short> neighbor3 = LoadRows512(ref source, sourceIndex - offset1, sourceStride, blockWidth);
                    Vector512<short> constrained = Constrain(neighbor0, sample, secondaryStrength, secondaryDampingShift)
                        + Constrain(neighbor1, sample, secondaryStrength, secondaryDampingShift)
                        + Constrain(neighbor2, sample, secondaryStrength, secondaryDampingShift)
                        + Constrain(neighbor3, sample, secondaryStrength, secondaryDampingShift);

                    sum += constrained * (tap == 0 ? secondaryNearWeight : secondaryFarWeight);
                    if (clippingRequired)
                    {
                        minimum = Vector512.Min(minimum, Vector512.Min(Vector512.Min(neighbor0, neighbor1), Vector512.Min(neighbor2, neighbor3)));
                        maximum = MaximumIgnoringSentinel(maximum, neighbor0, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor1, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor2, sentinel);
                        maximum = MaximumIgnoringSentinel(maximum, neighbor3, sentinel);
                    }
                }
            }

            Vector512<short> correction = (sum >> 15) & one;
            Vector512<short> filtered = sample + ((sum + rounding - correction) >> 4);
            if (clippingRequired)
            {
                filtered = Vector512.Min(Vector512.Max(filtered, minimum), maximum);
            }

            int destinationIndex = destinationOffset + (row * destinationStride);
            int halfRows = rowsPerBatch >> 1;
            StoreRows<TSample, TOutputOperator>(ref destination, destinationIndex, destinationStride, filtered.GetLower(), blockWidth);
            StoreRows<TSample, TOutputOperator>(ref destination, destinationIndex + (halfRows * destinationStride), destinationStride, filtered.GetUpper(), blockWidth);
        }
    }

    /// <summary>
    /// Loads four 8-wide rows or eight 4-wide rows into the two 256-bit halves of one vector.
    /// </summary>
    /// <param name="source">The first element in the bordered source plane.</param>
    /// <param name="offset">The offset of the first sample to load.</param>
    /// <param name="stride">The number of samples between adjacent rows.</param>
    /// <param name="width">The number of valid samples in each row.</param>
    /// <returns>The packed source rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<short> LoadRows512(ref ushort source, int offset, int stride, int width)
    {
        int halfRows = width == 8 ? 2 : 4;
        return Vector512.Create(
            LoadRows(ref source, offset, stride, width),
            LoadRows(ref source, offset + (halfRows * stride), stride, width));
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
    private static Vector512<short> Constrain(Vector512<short> neighbor, Vector512<short> sample, int threshold, int dampingShift)
    {
        Vector512<short> difference = neighbor - sample;
        Vector512<short> sign = difference >> 15;
        Vector512<ushort> magnitude = Vector512.Abs(difference).AsUInt16();
        Vector512<ushort> remaining = Vector512.SubtractSaturate(Vector512.Create((ushort)threshold), magnitude >> dampingShift);
        Vector512<short> constrained = Vector512.Min(magnitude, remaining).AsInt16();
        return (constrained + sign) ^ sign;
    }

    /// <summary>
    /// Updates packed row maxima while treating unavailable-neighbor sentinels as zero.
    /// </summary>
    /// <param name="maximum">The current per-lane maximum.</param>
    /// <param name="candidate">The candidate neighboring samples.</param>
    /// <param name="sentinel">The unavailable-neighbor sentinel in every lane.</param>
    /// <returns>The updated per-lane maximum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<short> MaximumIgnoringSentinel(Vector512<short> maximum, Vector512<short> candidate, Vector512<short> sentinel)
    {
        Vector512<short> available = Vector512.ConditionalSelect(Vector512.Equals(candidate, sentinel), Vector512<short>.Zero, candidate);
        return Vector512.Max(maximum, available);
    }
}
