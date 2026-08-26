// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;

/// <summary>
/// Applies the normative horizontal filter used by AV1 super-resolution upscaling.
/// </summary>
internal static class Av1SuperResolutionFilter
{
    /// <summary>
    /// The number of source samples consumed for each output sample.
    /// </summary>
    public const int TapCount = 8;

    /// <summary>
    /// The number of replicated source samples required at each row edge.
    /// </summary>
    public const int SourceBorder = (TapCount / 2) + 1;

    /// <summary>
    /// The number of output samples evaluated together by the vector path.
    /// </summary>
    private const int OutputGroupSize = 4;

    /// <summary>
    /// The number of fractional bits represented by each filter coefficient.
    /// </summary>
    private const int FilterBits = 7;

    /// <summary>
    /// The rounding offset applied before removing the filter-coefficient fractional bits.
    /// </summary>
    private const int FilterRounding = 1 << (FilterBits - 1);

    /// <summary>
    /// The number of filter phases in the normative table, expressed as a base-two exponent.
    /// </summary>
    private const int SubpixelBits = 6;

    /// <summary>
    /// The number of fractional bits used by the horizontal sample position.
    /// </summary>
    private const int ScaleSubpixelBits = 14;

    /// <summary>
    /// The mask selecting the fractional sample position.
    /// </summary>
    private const int ScaleSubpixelMask = (1 << ScaleSubpixelBits) - 1;

    /// <summary>
    /// The number of low position bits omitted when selecting a filter phase.
    /// </summary>
    private const int ScaleExtraBits = ScaleSubpixelBits - SubpixelBits;

    /// <summary>
    /// The half-step bias applied before filter-phase selection.
    /// </summary>
    private const int ScaleExtraOffset = 1 << (ScaleExtraBits - 1);

    /// <summary>
    /// Gets the flattened 64-phase, 8-tap normative AV1 super-resolution filter table.
    /// </summary>
    private static ReadOnlySpan<short> Filters =>
    [
        0, 0, 0, 128, 0, 0, 0, 0, 0, 0, -1, 128, 2, -1, 0, 0,
        0, 1, -3, 127, 4, -2, 1, 0, 0, 1, -4, 127, 6, -3, 1, 0,
        0, 2, -6, 126, 8, -3, 1, 0, 0, 2, -7, 125, 11, -4, 1, 0,
        -1, 2, -8, 125, 13, -5, 2, 0, -1, 3, -9, 124, 15, -6, 2, 0,
        -1, 3, -10, 123, 18, -6, 2, -1, -1, 3, -11, 122, 20, -7, 3, -1,
        -1, 4, -12, 121, 22, -8, 3, -1, -1, 4, -13, 120, 25, -9, 3, -1,
        -1, 4, -14, 118, 28, -9, 3, -1, -1, 4, -15, 117, 30, -10, 4, -1,
        -1, 5, -16, 116, 32, -11, 4, -1, -1, 5, -16, 114, 35, -12, 4, -1,
        -1, 5, -17, 112, 38, -12, 4, -1, -1, 5, -18, 111, 40, -13, 5, -1,
        -1, 5, -18, 109, 43, -14, 5, -1, -1, 6, -19, 107, 45, -14, 5, -1,
        -1, 6, -19, 105, 48, -15, 5, -1, -1, 6, -19, 103, 51, -16, 5, -1,
        -1, 6, -20, 101, 53, -16, 6, -1, -1, 6, -20, 99, 56, -17, 6, -1,
        -1, 6, -20, 97, 58, -17, 6, -1, -1, 6, -20, 95, 61, -18, 6, -1,
        -2, 7, -20, 93, 64, -18, 6, -2, -2, 7, -20, 91, 66, -19, 6, -1,
        -2, 7, -20, 88, 69, -19, 6, -1, -2, 7, -20, 86, 71, -19, 6, -1,
        -2, 7, -20, 84, 74, -20, 7, -2, -2, 7, -20, 81, 76, -20, 7, -1,
        -2, 7, -20, 79, 79, -20, 7, -2, -1, 7, -20, 76, 81, -20, 7, -2,
        -2, 7, -20, 74, 84, -20, 7, -2, -1, 6, -19, 71, 86, -20, 7, -2,
        -1, 6, -19, 69, 88, -20, 7, -2, -1, 6, -19, 66, 91, -20, 7, -2,
        -2, 6, -18, 64, 93, -20, 7, -2, -1, 6, -18, 61, 95, -20, 6, -1,
        -1, 6, -17, 58, 97, -20, 6, -1, -1, 6, -17, 56, 99, -20, 6, -1,
        -1, 6, -16, 53, 101, -20, 6, -1, -1, 5, -16, 51, 103, -19, 6, -1,
        -1, 5, -15, 48, 105, -19, 6, -1, -1, 5, -14, 45, 107, -19, 6, -1,
        -1, 5, -14, 43, 109, -18, 5, -1, -1, 5, -13, 40, 111, -18, 5, -1,
        -1, 4, -12, 38, 112, -17, 5, -1, -1, 4, -12, 35, 114, -16, 5, -1,
        -1, 4, -11, 32, 116, -16, 5, -1, -1, 4, -10, 30, 117, -15, 4, -1,
        -1, 3, -9, 28, 118, -14, 4, -1, -1, 3, -9, 25, 120, -13, 4, -1,
        -1, 3, -8, 22, 121, -12, 4, -1, -1, 3, -7, 20, 122, -11, 3, -1,
        -1, 2, -6, 18, 123, -10, 3, -1, 0, 2, -6, 15, 124, -9, 3, -1,
        0, 2, -5, 13, 125, -8, 2, -1, 0, 1, -4, 11, 125, -7, 2, 0,
        0, 1, -3, 8, 126, -6, 2, 0, 0, 1, -3, 6, 127, -4, 1, 0,
        0, 1, -2, 4, 127, -3, 1, 0, 0, 0, -1, 2, 128, -1, 0, 0
    ];

    /// <summary>
    /// Derives the fixed-point source-position increment for an output row.
    /// </summary>
    /// <param name="inputLength">The coded plane width.</param>
    /// <param name="outputLength">The upscaled plane width.</param>
    /// <returns>The source-position increment with 14 fractional bits.</returns>
    public static int GetConvolveStep(int inputLength, int outputLength)
        => ((inputLength << ScaleSubpixelBits) + (outputLength / 2)) / outputLength;

    /// <summary>
    /// Derives the initial fixed-point source position for an output row.
    /// </summary>
    /// <param name="inputLength">The coded plane width.</param>
    /// <param name="outputLength">The upscaled plane width.</param>
    /// <param name="step">The source-position increment returned by <see cref="GetConvolveStep"/>.</param>
    /// <returns>The initial source position with 14 fractional bits.</returns>
    public static int GetInitialSubpixel(int inputLength, int outputLength, int step)
    {
        int error = (outputLength * step) - (inputLength << ScaleSubpixelBits);
        int initial = (-((outputLength - inputLength) << (ScaleSubpixelBits - 1)) + (outputLength / 2)) / outputLength;

        initial += ScaleExtraOffset - (error / 2);
        return initial & ScaleSubpixelMask;
    }

    /// <summary>
    /// Upscales one replicated-edge eight-bit source row into eight-bit output samples.
    /// </summary>
    /// <param name="source">The coded row with <see cref="SourceBorder"/> replicated samples on each edge.</param>
    /// <param name="destination">The upscaled destination row.</param>
    /// <param name="step">The fixed-point source-position increment.</param>
    /// <param name="initialSubpixel">The initial fixed-point source position.</param>
    public static void UpscaleRow(ReadOnlySpan<byte> source, Span<byte> destination, int step, int initialSubpixel)
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        int sourcePosition = initialSubpixel;
        int column = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= destination.Length - OutputGroupSize; column += OutputGroupSize)
            {
                Vector128<int> filtered = FilterFour(ref sourceBase, sourcePosition, step);
                Vector128<ushort> samples16 = Vector128_.PackUnsignedSaturate(filtered, Vector128<int>.Zero);
                Vector128<byte> samples8 = Vector128_.PackUnsignedSaturate(samples16.AsInt16(), Vector128<short>.Zero);

                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationBase, column), samples8.AsUInt32().GetElement(0));
                sourcePosition += step * OutputGroupSize;
            }
        }

        for (; column < destination.Length; column++)
        {
            Unsafe.Add(ref destinationBase, column) = (byte)FilterOne(ref sourceBase, sourcePosition, byte.MaxValue);
            sourcePosition += step;
        }
    }

    /// <summary>
    /// Upscales one replicated-edge eight-bit source row into 16-bit output storage.
    /// </summary>
    /// <param name="source">The coded row with <see cref="SourceBorder"/> replicated samples on each edge.</param>
    /// <param name="destination">The upscaled destination row.</param>
    /// <param name="step">The fixed-point source-position increment.</param>
    /// <param name="initialSubpixel">The initial fixed-point source position.</param>
    public static void UpscaleRow(ReadOnlySpan<byte> source, Span<ushort> destination, int step, int initialSubpixel)
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        int sourcePosition = initialSubpixel;
        int column = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= destination.Length - OutputGroupSize; column += OutputGroupSize)
            {
                Vector128<int> filtered = FilterFour(ref sourceBase, sourcePosition, step);
                Vector128<ushort> samples = Vector128_.PackUnsignedSaturate(filtered, Vector128<int>.Zero);

                samples.GetLower().StoreUnsafe(ref destinationBase, (nuint)column);
                sourcePosition += step * OutputGroupSize;
            }
        }

        for (; column < destination.Length; column++)
        {
            Unsafe.Add(ref destinationBase, column) = (ushort)FilterOne(ref sourceBase, sourcePosition, byte.MaxValue);
            sourcePosition += step;
        }
    }

    /// <summary>
    /// Upscales one replicated-edge high-bit-depth source row into 16-bit output storage.
    /// </summary>
    /// <param name="source">The coded row with <see cref="SourceBorder"/> replicated samples on each edge.</param>
    /// <param name="destination">The upscaled destination row.</param>
    /// <param name="step">The fixed-point source-position increment.</param>
    /// <param name="initialSubpixel">The initial fixed-point source position.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    public static void UpscaleRow(ReadOnlySpan<ushort> source, Span<ushort> destination, int step, int initialSubpixel, int bitDepth)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        int maximum = (1 << bitDepth) - 1;
        int sourcePosition = initialSubpixel;
        int column = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<ushort> maximumVector = Vector128.Create((ushort)maximum);
            for (; column <= destination.Length - OutputGroupSize; column += OutputGroupSize)
            {
                Vector128<int> filtered = FilterFour(ref sourceBase, sourcePosition, step);
                Vector128<ushort> samples = Vector128.Min(Vector128_.PackUnsignedSaturate(filtered, Vector128<int>.Zero), maximumVector);

                samples.GetLower().StoreUnsafe(ref destinationBase, (nuint)column);
                sourcePosition += step * OutputGroupSize;
            }
        }

        for (; column < destination.Length; column++)
        {
            Unsafe.Add(ref destinationBase, column) = (ushort)FilterOne(ref sourceBase, sourcePosition, maximum);
            sourcePosition += step;
        }
    }

    /// <summary>
    /// Evaluates four consecutive output positions from an eight-bit source row.
    /// </summary>
    /// <param name="source">The first sample in the replicated-edge source row.</param>
    /// <param name="sourcePosition">The first fixed-point source position.</param>
    /// <param name="step">The fixed-point increment between output samples.</param>
    /// <returns>The rounded filter results in output order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> FilterFour(ref byte source, int sourcePosition, int step)
    {
        GetOffsets(sourcePosition, out int sourceOffset0, out int filterOffset0);
        GetOffsets(sourcePosition + step, out int sourceOffset1, out int filterOffset1);
        GetOffsets(sourcePosition + (step * 2), out int sourceOffset2, out int filterOffset2);
        GetOffsets(sourcePosition + (step * 3), out int sourceOffset3, out int filterOffset3);

        ref short filter = ref MemoryMarshal.GetReference(Filters);
        Vector128<short> samples0 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, sourceOffset0))).AsByte()).AsInt16();
        Vector128<short> samples1 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, sourceOffset1))).AsByte()).AsInt16();
        Vector128<short> samples2 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, sourceOffset2))).AsByte()).AsInt16();
        Vector128<short> samples3 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, sourceOffset3))).AsByte()).AsInt16();
        return FilterFour(
            samples0,
            samples1,
            samples2,
            samples3,
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset0),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset1),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset2),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset3));
    }

    /// <summary>
    /// Evaluates four consecutive output positions from a high-bit-depth source row.
    /// </summary>
    /// <param name="source">The first sample in the replicated-edge source row.</param>
    /// <param name="sourcePosition">The first fixed-point source position.</param>
    /// <param name="step">The fixed-point increment between output samples.</param>
    /// <returns>The rounded filter results in output order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> FilterFour(ref ushort source, int sourcePosition, int step)
    {
        GetOffsets(sourcePosition, out int sourceOffset0, out int filterOffset0);
        GetOffsets(sourcePosition + step, out int sourceOffset1, out int filterOffset1);
        GetOffsets(sourcePosition + (step * 2), out int sourceOffset2, out int filterOffset2);
        GetOffsets(sourcePosition + (step * 3), out int sourceOffset3, out int filterOffset3);

        ref short filter = ref MemoryMarshal.GetReference(Filters);
        return FilterFour(
            Vector128.LoadUnsafe(ref source, (nuint)sourceOffset0).AsInt16(),
            Vector128.LoadUnsafe(ref source, (nuint)sourceOffset1).AsInt16(),
            Vector128.LoadUnsafe(ref source, (nuint)sourceOffset2).AsInt16(),
            Vector128.LoadUnsafe(ref source, (nuint)sourceOffset3).AsInt16(),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset0),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset1),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset2),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset3));
    }

    /// <summary>
    /// Multiplies and reduces four independent eight-tap filter inputs.
    /// </summary>
    /// <param name="samples0">The source samples for the first output.</param>
    /// <param name="samples1">The source samples for the second output.</param>
    /// <param name="samples2">The source samples for the third output.</param>
    /// <param name="samples3">The source samples for the fourth output.</param>
    /// <param name="filter0">The coefficients for the first output.</param>
    /// <param name="filter1">The coefficients for the second output.</param>
    /// <param name="filter2">The coefficients for the third output.</param>
    /// <param name="filter3">The coefficients for the fourth output.</param>
    /// <returns>The rounded filter results in output order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> FilterFour(
        Vector128<short> samples0,
        Vector128<short> samples1,
        Vector128<short> samples2,
        Vector128<short> samples3,
        Vector128<short> filter0,
        Vector128<short> filter1,
        Vector128<short> filter2,
        Vector128<short> filter3)
    {
        Vector128<int> products0 = Vector128_.MultiplyAddAdjacent(samples0, filter0);
        Vector128<int> products1 = Vector128_.MultiplyAddAdjacent(samples1, filter1);
        Vector128<int> products2 = Vector128_.MultiplyAddAdjacent(samples2, filter2);
        Vector128<int> products3 = Vector128_.MultiplyAddAdjacent(samples3, filter3);

        // libaom reduces four independent filters in two horizontal-add stages so the four complete sums occupy
        // consecutive lanes. Keeping that arrangement also allows both destination forms to use one packed store.
        Vector128<int> pairs01 = HorizontalAdd(products0, products1);
        Vector128<int> pairs23 = HorizontalAdd(products2, products3);
        return (HorizontalAdd(pairs01, pairs23) + Vector128.Create(FilterRounding)) >> FilterBits;
    }

    /// <summary>
    /// Horizontally adds adjacent 32-bit lanes from two filter-product vectors.
    /// </summary>
    /// <param name="left">The first filter-product vector.</param>
    /// <param name="right">The second filter-product vector.</param>
    /// <returns>The adjacent sums from <paramref name="left"/> followed by those from <paramref name="right"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> HorizontalAdd(Vector128<int> left, Vector128<int> right)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.HorizontalAdd(left, right);
        }

        if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd.Arm64.AddPairwise(left, right);
        }

        if (AdvSimd.IsSupported)
        {
            Vector64<int> leftPairs = AdvSimd.AddPairwise(left.GetLower(), left.GetUpper());
            Vector64<int> rightPairs = AdvSimd.AddPairwise(right.GetLower(), right.GetUpper());
            return Vector128.Create(leftPairs, rightPairs);
        }

        Vector128<int> evenIndices = Vector128.Create(0, 2, 0, 2);
        Vector128<int> oddIndices = Vector128.Create(1, 3, 1, 3);
        Vector128<int> leftPairsFallback = Vector128.ShuffleNative(left, evenIndices) + Vector128.ShuffleNative(left, oddIndices);
        Vector128<int> rightPairsFallback = Vector128.ShuffleNative(right, evenIndices) + Vector128.ShuffleNative(right, oddIndices);
        return Vector128.Create(leftPairsFallback.GetLower(), rightPairsFallback.GetLower());
    }

    /// <summary>
    /// Evaluates one eight-bit source position for the scalar remainder.
    /// </summary>
    /// <param name="source">The first sample in the replicated-edge source row.</param>
    /// <param name="sourcePosition">The fixed-point source position.</param>
    /// <param name="maximum">The largest permitted output sample.</param>
    /// <returns>The rounded and clipped output sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FilterOne(ref byte source, int sourcePosition, int maximum)
    {
        GetOffsets(sourcePosition, out int sourceOffset, out int filterOffset);

        ref short filter = ref MemoryMarshal.GetReference(Filters);
        int sum = 0;
        for (int tap = 0; tap < TapCount; tap++)
        {
            sum += Unsafe.Add(ref source, sourceOffset + tap) * Unsafe.Add(ref filter, filterOffset + tap);
        }

        return Av1Math.Clip3(0, maximum, (sum + FilterRounding) >> FilterBits);
    }

    /// <summary>
    /// Evaluates one high-bit-depth source position for the scalar remainder.
    /// </summary>
    /// <param name="source">The first sample in the replicated-edge source row.</param>
    /// <param name="sourcePosition">The fixed-point source position.</param>
    /// <param name="maximum">The largest permitted output sample.</param>
    /// <returns>The rounded and clipped output sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FilterOne(ref ushort source, int sourcePosition, int maximum)
    {
        GetOffsets(sourcePosition, out int sourceOffset, out int filterOffset);

        ref short filter = ref MemoryMarshal.GetReference(Filters);
        int sum = 0;
        for (int tap = 0; tap < TapCount; tap++)
        {
            sum += Unsafe.Add(ref source, sourceOffset + tap) * Unsafe.Add(ref filter, filterOffset + tap);
        }

        return Av1Math.Clip3(0, maximum, (sum + FilterRounding) >> FilterBits);
    }

    /// <summary>
    /// Resolves the source and filter-table offsets for one fixed-point position.
    /// </summary>
    /// <param name="sourcePosition">The fixed-point source position.</param>
    /// <param name="sourceOffset">Receives the first source tap relative to the replicated-edge row.</param>
    /// <param name="filterOffset">Receives the first coefficient for the selected filter phase.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GetOffsets(int sourcePosition, out int sourceOffset, out int filterOffset)
    {
        int integerPosition = sourcePosition >> ScaleSubpixelBits;
        int filterPhase = (sourcePosition & ScaleSubpixelMask) >> ScaleExtraBits;
        sourceOffset = SourceBorder + integerPosition - (TapCount / 2);
        filterOffset = filterPhase * TapCount;
    }
}
