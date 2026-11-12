// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Expands rows of palette indices to pixels through a palette that is already converted to the pixel type.
/// </summary>
/// <remarks>
/// <para>
/// A palette bitmap packs 1, 2, 4 or 8 bits of index for each pixel. The first pixel is in the most significant
/// bits of a byte. Each pixel is one palette entry, so the decoder converts the palette to <c>TPixel</c> one time.
/// Then each pixel is one table load and one store, with no color conversion.
/// </para>
/// <para>
/// A table load at a per-pixel index is a gather, so the expansion is scalar. Each index operator unpacks one
/// whole source byte with constant shifts.
/// </para>
/// <para>
/// The palette has one entry for each value that an index can have: 2, 4, 16 or 256 entries. The decoder fills
/// the entries that the file palette does not define. Thus each index selects an entry, and the loads need no
/// bounds checks.
/// </para>
/// </remarks>
internal static class BmpPaletteExpander
{
    /// <summary>
    /// Unpacks the indices of one source byte to pixels.
    /// </summary>
    internal interface IIndexOperator
    {
        /// <summary>
        /// Gets the bit count of one index.
        /// </summary>
        static abstract int BitsPerIndex { get; }

        /// <summary>
        /// Gets the index count of one byte.
        /// </summary>
        static abstract int IndicesPerByte { get; }

        /// <summary>
        /// Writes the pixels of all indices in one byte, first index first.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="value">The source byte.</param>
        /// <param name="palette">The first palette entry. The palette has an entry for each possible index.</param>
        /// <param name="destination">The first destination pixel. <see cref="IndicesPerByte"/> pixels are written.</param>
        static abstract void Expand<TPixel>(uint value, ref TPixel palette, ref TPixel destination)
            where TPixel : unmanaged;
    }

    /// <summary>
    /// Expands one row of palette indices.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="indices">The packed indices. It holds at least the bytes of <paramref name="destination"/>.</param>
    /// <param name="bitsPerIndex">The bit count of one index: 1, 2, 4 or 8.</param>
    /// <param name="palette">The palette, converted to <typeparamref name="TPixel"/>. It has an entry for each possible index.</param>
    /// <param name="destination">The destination pixels.</param>
    public static void Expand<TPixel>(ReadOnlySpan<byte> indices, int bitsPerIndex, ReadOnlySpan<TPixel> palette, Span<TPixel> destination)
        where TPixel : unmanaged
    {
        switch (bitsPerIndex)
        {
            case 1:
                Expand<TPixel, Index1Operator>(indices, palette, destination);
                break;
            case 2:
                Expand<TPixel, Index2Operator>(indices, palette, destination);
                break;
            case 4:
                Expand<TPixel, Index4Operator>(indices, palette, destination);
                break;
            default:
                Expand<TPixel, Index8Operator>(indices, palette, destination);
                break;
        }
    }

    /// <summary>
    /// Expands one row of palette indices with <typeparamref name="TOperator"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <typeparam name="TOperator">The index operator.</typeparam>
    /// <param name="indices">The packed indices. It holds at least the bytes of <paramref name="destination"/>.</param>
    /// <param name="palette">The palette, converted to <typeparamref name="TPixel"/>. It has an entry for each possible index.</param>
    /// <param name="destination">The destination pixels.</param>
    public static void Expand<TPixel, TOperator>(ReadOnlySpan<byte> indices, ReadOnlySpan<TPixel> palette, Span<TPixel> destination)
        where TPixel : unmanaged
        where TOperator : struct, IIndexOperator
    {
        int width = destination.Length;
        int wholeBytes = width / TOperator.IndicesPerByte;
        DebugGuard.MustBeGreaterThanOrEqualTo(
            indices.Length,
            wholeBytes + (width % TOperator.IndicesPerByte == 0 ? 0 : 1),
            nameof(indices));

        // Each index has BitsPerIndex bits, so it is less than 2^BitsPerIndex. A palette with that many entries
        // has an entry for each index, so the loads below need no bounds check.
        DebugGuard.MustBeGreaterThanOrEqualTo(palette.Length, 1 << TOperator.BitsPerIndex, nameof(palette));

        ref byte indexBase = ref MemoryMarshal.GetReference(indices);
        ref TPixel paletteBase = ref MemoryMarshal.GetReference(palette);
        ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

        // Byte i holds the indices of pixels i * IndicesPerByte to (i + 1) * IndicesPerByte - 1. IndicesPerByte is a
        // constant of the operator, so the JIT folds the multiply to a shift, and the operator body is inlined.
        for (int i = 0; i < wholeBytes; i++)
        {
            TOperator.Expand(Unsafe.Add(ref indexBase, (uint)i), ref paletteBase, ref Unsafe.Add(ref destinationBase, (uint)(i * TOperator.IndicesPerByte)));
        }

        // The last byte can hold fewer indices than the row needs. The row ends inside this byte, and its low bits
        // are padding. The first index is in the top bits, so the shift starts at 8 - BitsPerIndex and decreases by
        // BitsPerIndex for each pixel. The mask has the low BitsPerIndex bits set, for example 0b11 for 2 bits.
        // This loop does the same shifts as the operator, but stops at the row width.
        int x = wholeBytes * TOperator.IndicesPerByte;
        if (x < width)
        {
            uint value = Unsafe.Add(ref indexBase, (uint)wholeBytes);
            uint mask = (1U << TOperator.BitsPerIndex) - 1;
            for (int shift = 8 - TOperator.BitsPerIndex; x < width; x++, shift -= TOperator.BitsPerIndex)
            {
                Unsafe.Add(ref destinationBase, (uint)x) = Unsafe.Add(ref paletteBase, (nuint)((value >> shift) & mask));
            }
        }
    }

    /// <summary>
    /// Unpacks 8 indices of 1 bit from one byte.
    /// </summary>
    internal readonly struct Index1Operator : IIndexOperator
    {
        /// <inheritdoc/>
        public static int BitsPerIndex => 1;

        /// <inheritdoc/>
        public static int IndicesPerByte => 8;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Expand<TPixel>(uint value, ref TPixel palette, ref TPixel destination)
            where TPixel : unmanaged
        {
            // Byte layout, most significant bit first: [p0 p1 p2 p3 p4 p5 p6 p7], one bit for each pixel.
            // Pixel i is bit 7 - i: shift it down to bit 0, then AND 1 removes the bits of the pixels before it.
            // Pixel 0 needs no AND because the shift by 7 leaves only one bit of the byte.
            destination = Unsafe.Add(ref palette, (nuint)(value >> 7));
            Unsafe.Add(ref destination, 1) = Unsafe.Add(ref palette, (nuint)((value >> 6) & 1));
            Unsafe.Add(ref destination, 2) = Unsafe.Add(ref palette, (nuint)((value >> 5) & 1));
            Unsafe.Add(ref destination, 3) = Unsafe.Add(ref palette, (nuint)((value >> 4) & 1));
            Unsafe.Add(ref destination, 4) = Unsafe.Add(ref palette, (nuint)((value >> 3) & 1));
            Unsafe.Add(ref destination, 5) = Unsafe.Add(ref palette, (nuint)((value >> 2) & 1));
            Unsafe.Add(ref destination, 6) = Unsafe.Add(ref palette, (nuint)((value >> 1) & 1));
            Unsafe.Add(ref destination, 7) = Unsafe.Add(ref palette, (nuint)(value & 1));
        }
    }

    /// <summary>
    /// Unpacks 4 indices of 2 bits from one byte.
    /// </summary>
    internal readonly struct Index2Operator : IIndexOperator
    {
        /// <inheritdoc/>
        public static int BitsPerIndex => 2;

        /// <inheritdoc/>
        public static int IndicesPerByte => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Expand<TPixel>(uint value, ref TPixel palette, ref TPixel destination)
            where TPixel : unmanaged
        {
            // Byte layout, most significant bits first: [p0 p0 p1 p1 p2 p2 p3 p3], two bits for each pixel.
            // Pixel i is bits 7 - 2i and 6 - 2i: shift them down by 6 - 2i, then AND 3 (0b11) keeps two bits.
            // Pixel 0 needs no AND because the shift by 6 leaves only two bits of the byte.
            destination = Unsafe.Add(ref palette, (nuint)(value >> 6));
            Unsafe.Add(ref destination, 1) = Unsafe.Add(ref palette, (nuint)((value >> 4) & 3));
            Unsafe.Add(ref destination, 2) = Unsafe.Add(ref palette, (nuint)((value >> 2) & 3));
            Unsafe.Add(ref destination, 3) = Unsafe.Add(ref palette, (nuint)(value & 3));
        }
    }

    /// <summary>
    /// Unpacks 2 indices of 4 bits from one byte.
    /// </summary>
    internal readonly struct Index4Operator : IIndexOperator
    {
        /// <inheritdoc/>
        public static int BitsPerIndex => 4;

        /// <inheritdoc/>
        public static int IndicesPerByte => 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Expand<TPixel>(uint value, ref TPixel palette, ref TPixel destination)
            where TPixel : unmanaged
        {
            // Byte layout: [p0 p0 p0 p0 p1 p1 p1 p1]. Pixel 0 is the high nibble: shift it down by 4.
            // Pixel 1 is the low nibble: AND 15 (0b1111) removes the high nibble.
            destination = Unsafe.Add(ref palette, (nuint)(value >> 4));
            Unsafe.Add(ref destination, 1) = Unsafe.Add(ref palette, (nuint)(value & 15));
        }
    }

    /// <summary>
    /// Takes one index of 8 bits from one byte.
    /// </summary>
    internal readonly struct Index8Operator : IIndexOperator
    {
        /// <inheritdoc/>
        public static int BitsPerIndex => 8;

        /// <inheritdoc/>
        public static int IndicesPerByte => 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Expand<TPixel>(uint value, ref TPixel palette, ref TPixel destination)
            where TPixel : unmanaged
            => destination = Unsafe.Add(ref palette, (nuint)value);
    }
}
