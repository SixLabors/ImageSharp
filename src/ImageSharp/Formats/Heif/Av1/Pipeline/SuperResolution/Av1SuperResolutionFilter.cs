// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.SuperResolution;

/// <summary>
/// Applies the normative horizontal filter used by AV1 super-resolution upscaling.
/// </summary>
/// <remarks>
/// Four output coordinates are evaluated together, but each coordinate starts as its own eight-lane vector of source
/// taps. Pairwise multiply-add produces four partial sums per output, and two horizontal reductions transpose those
/// four independent dot products into consecutive output lanes. This layout supports arbitrary fixed-point source
/// steps and filter phases without gathers or a temporary coefficient matrix.
/// </remarks>
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
        => UpscaleRowCore(source, destination, step, initialSubpixel, byte.MaxValue);

    /// <summary>
    /// Upscales one replicated-edge eight-bit source row into 16-bit output storage.
    /// </summary>
    /// <param name="source">The coded row with <see cref="SourceBorder"/> replicated samples on each edge.</param>
    /// <param name="destination">The upscaled destination row.</param>
    /// <param name="step">The fixed-point source-position increment.</param>
    /// <param name="initialSubpixel">The initial fixed-point source position.</param>
    public static void UpscaleRow(ReadOnlySpan<byte> source, Span<ushort> destination, int step, int initialSubpixel)
        => UpscaleRowCore(source, destination, step, initialSubpixel, byte.MaxValue);

    /// <summary>
    /// Upscales one replicated-edge high-bit-depth source row into 16-bit output storage.
    /// </summary>
    /// <param name="source">The coded row with <see cref="SourceBorder"/> replicated samples on each edge.</param>
    /// <param name="destination">The upscaled destination row.</param>
    /// <param name="step">The fixed-point source-position increment.</param>
    /// <param name="initialSubpixel">The initial fixed-point source position.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    public static void UpscaleRow(ReadOnlySpan<ushort> source, Span<ushort> destination, int step, int initialSubpixel, int bitDepth)
        => UpscaleRowCore(source, destination, step, initialSubpixel, (1 << bitDepth) - 1);

    /// <summary>
    /// Upscales one replicated-edge source row of either depth.
    /// </summary>
    /// <typeparam name="TSource">Byte or ushort, selected by the coded depth.</typeparam>
    /// <typeparam name="TDestination">Byte or ushort, selected by the destination plane.</typeparam>
    /// <param name="source">The coded row with <see cref="SourceBorder"/> replicated samples on each edge.</param>
    /// <param name="destination">The upscaled destination row.</param>
    /// <param name="step">The fixed-point source-position increment.</param>
    /// <param name="initialSubpixel">The initial fixed-point source position.</param>
    /// <param name="maximum">The largest sample the coded depth permits.</param>
    /// <remarks>
    /// Every output position samples the source at its own fixed-point position with its own set of
    /// taps, so four outputs are evaluated together and the position advances by four steps. The
    /// eight-tap kernel overshoots on an edge, so both the vector stage and the scalar tail clip to
    /// the coded depth. Reference: av1_upscale_normative_rows().
    /// </remarks>
    private static void UpscaleRowCore<TSource, TDestination>(
        ReadOnlySpan<TSource> source,
        Span<TDestination> destination,
        int step,
        int initialSubpixel,
        int maximum)
        where TSource : unmanaged
        where TDestination : unmanaged
    {
        ref TSource sourceBase = ref MemoryMarshal.GetReference(source);
        ref TDestination destinationBase = ref MemoryMarshal.GetReference(destination);
        int sourcePosition = initialSubpixel;
        int column = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<ushort> maximumVector = Vector128.Create((ushort)maximum);
            for (; column <= destination.Length - OutputGroupSize; column += OutputGroupSize)
            {
                Vector128<int> filtered = FilterFour(ref sourceBase, sourcePosition, step);

                // The saturating pack holds the results at zero and below 65536. The clip that
                // follows is what brings them inside the coded depth, and it is the same clip the
                // scalar tail applies.
                Vector128<ushort> samples = Vector128.Min(Vector128_.PackUnsignedSaturate(filtered, Vector128<int>.Zero), maximumVector);

                StoreFour(samples, ref destinationBase, column);
                sourcePosition += step * OutputGroupSize;
            }
        }

        for (; column < destination.Length; column++)
        {
            WriteSample(ref destinationBase, column, FilterOne(ref sourceBase, sourcePosition, maximum));
            sourcePosition += step;
        }
    }

    /// <summary>
    /// Stores four upscaled samples at the depth of the destination plane.
    /// </summary>
    /// <typeparam name="TDestination">Byte or ushort, selected by the destination plane.</typeparam>
    /// <param name="samples">The four clipped samples in the lowest lanes.</param>
    /// <param name="destination">The first sample of the destination row.</param>
    /// <param name="column">The destination column.</param>
    /// <remarks>
    /// The samples are already inside the range of the destination, so the narrowing an eight-bit
    /// plane needs cannot lose a value.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreFour<TDestination>(Vector128<ushort> samples, ref TDestination destination, int column)
        where TDestination : unmanaged
    {
        ref TDestination sample = ref Unsafe.Add(ref destination, column);
        if (Unsafe.SizeOf<TDestination>() == 1)
        {
            Vector128<byte> packed = Vector128_.PackUnsignedSaturate(samples.AsInt16(), Vector128<short>.Zero);
            Unsafe.WriteUnaligned(ref Unsafe.As<TDestination, byte>(ref sample), packed.AsUInt32().GetElement(0));
            return;
        }

        samples.GetLower().StoreUnsafe(ref Unsafe.As<TDestination, ushort>(ref sample));
    }

    /// <summary>
    /// Writes one upscaled sample at the depth of the destination plane.
    /// </summary>
    /// <typeparam name="TDestination">Byte or ushort, selected by the destination plane.</typeparam>
    /// <param name="destination">The first sample of the destination row.</param>
    /// <param name="column">The destination column.</param>
    /// <param name="value">The clipped sample.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteSample<TDestination>(ref TDestination destination, int column, int value)
        where TDestination : unmanaged
    {
        ref TDestination sample = ref Unsafe.Add(ref destination, column);
        if (Unsafe.SizeOf<TDestination>() == 1)
        {
            Unsafe.As<TDestination, byte>(ref sample) = (byte)value;
            return;
        }

        Unsafe.As<TDestination, ushort>(ref sample) = (ushort)value;
    }

    /// <summary>
    /// Evaluates four consecutive output positions from a source row of either depth.
    /// </summary>
    /// <typeparam name="TSource">Byte or ushort, selected by the coded depth.</typeparam>
    /// <param name="source">The first sample in the replicated-edge source row.</param>
    /// <param name="sourcePosition">The first fixed-point source position.</param>
    /// <param name="step">The fixed-point increment between output samples.</param>
    /// <returns>The rounded filter results in output order.</returns>
    /// <remarks>
    /// The replicated edge guarantees that all eight taps of every output are contiguous, including
    /// the first and the last, so each tap set is one load. An eight-bit row is read through a
    /// packed integer and widened, which touches only the eight samples the output owns. A
    /// high-bit-depth row is at most twelve bits, so its signed view stays positive and the same
    /// pairwise multiply-add serves both depths.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> FilterFour<TSource>(ref TSource source, int sourcePosition, int step)
        where TSource : unmanaged
    {
        GetOffsets(sourcePosition, out int sourceOffset0, out int filterOffset0);
        GetOffsets(sourcePosition + step, out int sourceOffset1, out int filterOffset1);
        GetOffsets(sourcePosition + (step * 2), out int sourceOffset2, out int filterOffset2);
        GetOffsets(sourcePosition + (step * 3), out int sourceOffset3, out int filterOffset3);

        ref short filter = ref MemoryMarshal.GetReference(Filters);
        return FilterFour(
            LoadTaps(ref source, sourceOffset0),
            LoadTaps(ref source, sourceOffset1),
            LoadTaps(ref source, sourceOffset2),
            LoadTaps(ref source, sourceOffset3),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset0),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset1),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset2),
            Vector128.LoadUnsafe(ref filter, (nuint)filterOffset3));
    }

    /// <summary>
    /// Loads the eight taps of one output position as signed sixteen-bit lanes.
    /// </summary>
    /// <typeparam name="TSource">Byte or ushort, selected by the coded depth.</typeparam>
    /// <param name="source">The first sample in the replicated-edge source row.</param>
    /// <param name="offset">The sample offset of the first tap.</param>
    /// <returns>The eight taps in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> LoadTaps<TSource>(ref TSource source, int offset)
        where TSource : unmanaged
    {
        ref TSource sample = ref Unsafe.Add(ref source, offset);
        if (Unsafe.SizeOf<TSource>() == 1)
        {
            ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<TSource, byte>(ref sample));
            return Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte()).AsInt16();
        }

        return Vector128.LoadUnsafe(ref Unsafe.As<TSource, short>(ref sample));
    }

    /// <summary>
    /// Evaluates one output position from a source row of either depth.
    /// </summary>
    /// <typeparam name="TSource">Byte or ushort, selected by the coded depth.</typeparam>
    /// <param name="source">The first sample in the replicated-edge source row.</param>
    /// <param name="sourcePosition">The fixed-point source position.</param>
    /// <param name="maximum">The largest sample the coded depth permits.</param>
    /// <returns>The rounded and clipped output sample.</returns>
    private static int FilterOne<TSource>(ref TSource source, int sourcePosition, int maximum)
        where TSource : unmanaged
    {
        GetOffsets(sourcePosition, out int sourceOffset, out int filterOffset);

        ref short filter = ref MemoryMarshal.GetReference(Filters);
        int sum = 0;
        for (int tap = 0; tap < TapCount; tap++)
        {
            sum += ReadSample(ref source, sourceOffset + tap) * Unsafe.Add(ref filter, filterOffset + tap);
        }

        return Av1Math.Clip3(0, maximum, (sum + FilterRounding) >> FilterBits);
    }

    /// <summary>
    /// Reads one sample of either depth.
    /// </summary>
    /// <typeparam name="TSource">Byte or ushort, selected by the coded depth.</typeparam>
    /// <param name="source">The first sample in the replicated-edge source row.</param>
    /// <param name="offset">The sample offset.</param>
    /// <returns>The sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadSample<TSource>(ref TSource source, int offset)
        where TSource : unmanaged
    {
        ref TSource sample = ref Unsafe.Add(ref source, offset);
        return Unsafe.SizeOf<TSource>() == 1
            ? Unsafe.As<TSource, byte>(ref sample)
            : Unsafe.As<TSource, ushort>(ref sample);
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

        // the reference decoder reduces four independent filters in two horizontal-add stages so the four complete sums occupy
        // consecutive lanes. Keeping that arrangement also allows both destination forms to use one packed store.
        Vector128<int> pairs01 = Vector128_.HorizontalAdd(products0, products1);
        Vector128<int> pairs23 = Vector128_.HorizontalAdd(products2, products3);
        return (Vector128_.HorizontalAdd(pairs01, pairs23) + Vector128.Create(FilterRounding)) >> FilterBits;
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
