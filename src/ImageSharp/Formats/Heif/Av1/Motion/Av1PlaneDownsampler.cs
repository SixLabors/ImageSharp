// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Halves the resolution of an eight-bit luma plane with the normative symmetric-even filter.
/// </summary>
/// <remarks>
/// The image pyramid that dense flow estimation walks is built by repeated halving, so only the
/// factor-of-two path is needed. Reference: av1_resize_plane_to_half(), av1_resize_horz_dir_c(),
/// av1_resize_vert_dir_c() and down2_symeven() in av1/common/resize.c, with the vector layout of
/// av1_resize_horz_dir_avx2() and av1_resize_vert_dir_avx2() in av1/common/x86/resize_avx2.c.
/// </remarks>
internal static partial class Av1PlaneDownsampler
{
    /// <summary>
    /// The fractional bits carried by the filter taps.
    /// </summary>
    private const int FilterBits = 7;

    /// <summary>
    /// The number of taps in one half of the symmetric kernel.
    /// </summary>
    private const int HalfTapCount = 4;

    /// <summary>
    /// Gets one half of the symmetric eight-tap downsampling kernel in Q7.
    /// </summary>
    /// <remarks>
    /// The complete kernel is this half followed by its mirror, so an output sample is the weighted
    /// sum of the four input samples on each side of the sampling position. The taps total 64, which
    /// is half of the Q7 unit, because each tap weights two input samples.
    /// Reference: av1_down2_symeven_half_filter in av1/common/resize.h.
    /// </remarks>
    private static ReadOnlySpan<short> HalfFilter => [56, 12, -3, -1];

    /// <summary>
    /// Gets whether one output extent is exactly half of its input extent.
    /// </summary>
    /// <param name="length">The input extent.</param>
    /// <param name="halvedLength">The output extent.</param>
    /// <returns>Whether the output extent is the halved input extent.</returns>
    /// <remarks>Reference: get_down2_length() and should_resize_by_half() in resize.c.</remarks>
    public static bool IsHalved(int length, int halvedLength)
        => (length + 1) >> 1 == halvedLength;

    /// <summary>
    /// Halves both axes of one eight-bit plane.
    /// </summary>
    /// <param name="allocator">The allocator of the intermediate planes.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="width">The source width, which must be even.</param>
    /// <param name="height">The source height, which must be even.</param>
    /// <param name="destination">The halved samples.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <remarks>
    /// The two passes are separable, and the reference runs them through one intermediate plane of
    /// halved width and full height. Both passes here are shaped for whole vectors of columns: the
    /// horizontal pass separates a row into its even and odd samples once, after which every tap is a
    /// plain shifted load, and the vertical pass combines whole rows without gathering a column.
    /// </remarks>
    public static void Halve(
        MemoryAllocator allocator,
        ReadOnlySpan<byte> source,
        int sourceStride,
        int width,
        int height,
        Span<byte> destination,
        int destinationStride)
    {
        int halvedWidth = width >> 1;
        int halvedHeight = height >> 1;

        // The intermediate plane is padded on the right so that the vertical pass can always load a
        // whole vector of columns, and the separated streams of the horizontal pass are padded on both
        // sides so that the kernel's reach past a row end becomes an ordinary load of clamped samples.
        int intermediateStride = halvedWidth + VectorPadding;
        int separatedStride = halvedWidth + (2 * SeparatedPadding);
        using IMemoryOwner<byte> scratchOwner = allocator.Allocate<byte>(
            (intermediateStride * height) + (2 * separatedStride));

        Span<byte> scratch = scratchOwner.Memory.Span;
        Span<byte> intermediate = scratch[..(intermediateStride * height)];
        Span<byte> even = scratch.Slice(intermediateStride * height, separatedStride);
        Span<byte> odd = scratch.Slice((intermediateStride * height) + separatedStride, separatedStride);

        HalveRows(source, sourceStride, width, height, intermediate, intermediateStride, even, odd);
        HalveColumns(intermediate, intermediateStride, height, halvedWidth, halvedHeight, destination, destinationStride);
    }

    /// <summary>
    /// Halves the width of every source row.
    /// </summary>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="intermediate">The halved-width samples.</param>
    /// <param name="intermediateStride">The intermediate row stride.</param>
    /// <param name="even">The scratch buffer of even-position samples.</param>
    /// <param name="odd">The scratch buffer of odd-position samples.</param>
    /// <remarks>
    /// An output sample at halved position k is the weighted sum of the source samples at
    /// 2k-3 to 2k+4. Separating the row into its even samples E[k] = source[2k] and its odd samples
    /// O[k] = source[2k+1] turns each of the four taps into a pair of shifted reads of those two
    /// streams, so the accumulation itself needs no shuffle at all:
    /// tap 0 takes E[k] and O[k], tap 1 takes O[k-1] and E[k+1], tap 2 takes E[k-1] and O[k+1], and
    /// tap 3 takes O[k-2] and E[k+2]. Reference: the even and odd separation of
    /// av1_resize_horz_dir_avx2() in resize_avx2.c.
    /// </remarks>
    private static void HalveRows(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int width,
        int height,
        Span<byte> intermediate,
        int intermediateStride,
        Span<byte> even,
        Span<byte> odd)
    {
        int halvedWidth = width >> 1;
        for (int row = 0; row < height; row++)
        {
            ReadOnlySpan<byte> sourceRow = source.Slice(row * sourceStride, width);
            Separate(sourceRow, halvedWidth, even, odd);
            FilterSeparated(even, odd, halvedWidth, intermediate.Slice(row * intermediateStride, intermediateStride));
        }
    }

    /// <summary>
    /// Splits one row into its even-position and odd-position samples, with clamped padding.
    /// </summary>
    /// <param name="source">The source row.</param>
    /// <param name="halvedWidth">The number of samples in each separated stream.</param>
    /// <param name="even">Receives the even-position samples at <see cref="SeparatedPadding"/>.</param>
    /// <param name="odd">Receives the odd-position samples at <see cref="SeparatedPadding"/>.</param>
    /// <remarks>
    /// The padding on each side repeats the first and last source sample, which is what the reference
    /// reads when its kernel reaches past a row end. Writing it once removes every boundary test from
    /// the accumulation loop.
    /// </remarks>
    private static void Separate(ReadOnlySpan<byte> source, int halvedWidth, Span<byte> even, Span<byte> odd)
    {
        SeparateCore(source, halvedWidth, even, odd);

        byte first = source[0];
        byte last = source[source.Length - 1];
        for (int index = 0; index < SeparatedPadding; index++)
        {
            even[index] = first;
            odd[index] = first;
            even[SeparatedPadding + halvedWidth + index] = last;
            odd[SeparatedPadding + halvedWidth + index] = last;
        }
    }

    /// <summary>
    /// Halves the height of the intermediate plane.
    /// </summary>
    /// <param name="intermediate">The halved-width samples.</param>
    /// <param name="intermediateStride">The intermediate row stride.</param>
    /// <param name="height">The intermediate height.</param>
    /// <param name="halvedWidth">The destination width.</param>
    /// <param name="halvedHeight">The destination height.</param>
    /// <param name="destination">The halved samples.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <remarks>
    /// The kernel runs along a column, and the reference's vector form never gathers one: it walks a
    /// band of whole vectors of columns and combines eight source rows of that band at a time. The
    /// rows above the plane and below it are the first and last row repeated, which is the clamping
    /// the scalar reference performs per sample. Reference: av1_resize_vert_dir_avx2() in
    /// resize_avx2.c.
    /// </remarks>
    private static void HalveColumns(
        ReadOnlySpan<byte> intermediate,
        int intermediateStride,
        int height,
        int halvedWidth,
        int halvedHeight,
        Span<byte> destination,
        int destinationStride)
    {
        for (int row = 0; row < halvedHeight; row++)
        {
            // The sampling position sits between source rows 2r and 2r+1, so the kernel reads the four
            // rows above and the four rows below, clamped to the plane.
            int position = row * 2;
            FilterColumnBand(
                intermediate,
                intermediateStride,
                height,
                position,
                halvedWidth,
                destination.Slice(row * destinationStride, destinationStride));
        }
    }
}
