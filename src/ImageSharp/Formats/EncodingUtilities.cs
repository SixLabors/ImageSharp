// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides utilities for encoding images.
/// </summary>
internal static class EncodingUtilities
{
    /// <summary>
    /// Packs the indices of one or more destination bytes.
    /// </summary>
    /// <remarks>
    /// Each method reads <see cref="IndicesPerByte"/> source bytes for each destination byte. Each source byte is one
    /// index of <see cref="BitsPerIndex"/> bits. The first index goes in the most significant bits.
    /// </remarks>
    private interface IIndexPacker
    {
        /// <summary>
        /// Gets the bit count of one index.
        /// </summary>
        static abstract int BitsPerIndex { get; }

        /// <summary>
        /// Gets the index count of one destination byte.
        /// </summary>
        static abstract int IndicesPerByte { get; }

        /// <summary>
        /// Packs the indices of one destination byte.
        /// </summary>
        /// <param name="source">The first index. <see cref="IndicesPerByte"/> indices are read.</param>
        /// <returns>The packed byte.</returns>
        static abstract byte Pack(ref byte source);

        /// <summary>
        /// Packs the indices of 16 destination bytes.
        /// </summary>
        /// <param name="source">The first index. 16 * <see cref="IndicesPerByte"/> indices are read.</param>
        /// <returns>The packed bytes.</returns>
        static abstract Vector128<byte> Pack128(ref byte source);

        /// <summary>
        /// Packs the indices of 32 destination bytes.
        /// </summary>
        /// <param name="source">The first index. 32 * <see cref="IndicesPerByte"/> indices are read.</param>
        /// <returns>The packed bytes.</returns>
        static abstract Vector256<byte> Pack256(ref byte source);

        /// <summary>
        /// Packs the indices of 64 destination bytes.
        /// </summary>
        /// <param name="source">The first index. 64 * <see cref="IndicesPerByte"/> indices are read.</param>
        /// <returns>The packed bytes.</returns>
        static abstract Vector512<byte> Pack512(ref byte source);
    }

    /// <summary>
    /// Packs one row of palette indices to 1, 2, 4 or 8 bits for each index.
    /// </summary>
    /// <remarks>
    /// The first index goes in the most significant bits of the first byte. When the row ends inside a byte, the low
    /// bits of that byte are zero. BMP, PNG and TIFF use this bit order:
    /// <list type="bullet">
    /// <item><description>
    /// BMP: BITMAPINFOHEADER structure, the 4-bit example of biBitCount.
    /// See https://learn.microsoft.com/en-us/previous-versions/dd183376(v=vs.85).
    /// </description></item>
    /// <item><description>
    /// PNG: PNG Specification (Third Edition), section 7.2 Scanlines. See https://www.w3.org/TR/png-3/#7Scanline.
    /// </description></item>
    /// <item><description>
    /// TIFF: TIFF 6.0 Specification, section 8, FillOrder value 1, the default.
    /// See https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf.
    /// </description></item>
    /// </list>
    /// </remarks>
    /// <param name="indices">The indices, one byte each. Each index must be less than 2^<paramref name="bitsPerIndex"/>.</param>
    /// <param name="destination">
    /// The packed row. It must hold at least ((indices.Length * bitsPerIndex) + 7) / 8 bytes. The bytes after them do not change.
    /// </param>
    /// <param name="bitsPerIndex">The bit count of one index: 1, 2, 4 or 8.</param>
    public static void PackIndices(ReadOnlySpan<byte> indices, Span<byte> destination, int bitsPerIndex)
    {
        switch (bitsPerIndex)
        {
            case 1:
                PackIndices<Index1Packer>(indices, destination);
                break;
            case 2:
                PackIndices<Index2Packer>(indices, destination);
                break;
            case 4:
                PackIndices<Index4Packer>(indices, destination);
                break;
            default:
                // An 8-bit index fills one byte, so the packed row is the source row.
                indices.CopyTo(destination);
                break;
        }
    }

    /// <summary>
    /// Creates the quantizer that maps pixels to a decoded palette, for an encoder that writes that palette again.
    /// </summary>
    /// <remarks>
    /// Dithering is off and color matching is exact, so each pixel that has a palette color keeps that color. The default
    /// coarse matching keeps 5 bits of each color channel, so it can map two near palette colors to one entry.
    /// </remarks>
    /// <param name="palette">The decoded palette.</param>
    /// <param name="transparentColorMode">The transparent color mode of the encoder.</param>
    /// <returns>The quantizer.</returns>
    public static PaletteQuantizer CreateDecodedPaletteQuantizer(ReadOnlyMemory<Color> palette, TransparentColorMode transparentColorMode)
        => new(
            palette,
            new QuantizerOptions
            {
                Dither = null,
                ColorMatchingMode = ColorMatchingMode.Exact,
                TransparentColorMode = transparentColorMode
            });

    /// <summary>
    /// Determines if transparent pixels can be replaced based on the specified color mode and pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="mode">Indicates the color mode used to assess the ability to replace transparent pixels.</param>
    /// <returns>Returns true if transparent pixels can be replaced; otherwise, false.</returns>
    public static bool ShouldReplaceTransparentPixels<TPixel>(TransparentColorMode mode)
        where TPixel : unmanaged, IPixel<TPixel>
        => mode == TransparentColorMode.Clear && TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Unassociated;

    /// <summary>
    /// Replaces pixels with a transparent alpha component with fully transparent pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="frame">The <see cref="ImageFrame{TPixel}"/> where the transparent pixels will be changed.</param>
    public static void ReplaceTransparentPixels<TPixel>(ImageFrame<TPixel> frame)
        where TPixel : unmanaged, IPixel<TPixel>
        => ReplaceTransparentPixels(frame.Configuration, frame.PixelBuffer);

    /// <summary>
    /// Replaces pixels with a transparent alpha component with fully transparent pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="buffer">The  <see cref="Buffer2D{TPixel}"/> where the transparent pixels will be changed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReplaceTransparentPixels<TPixel>(Configuration configuration, Buffer2D<TPixel> buffer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Buffer2DRegion<TPixel> region = buffer.GetRegion();
        ReplaceTransparentPixels(configuration, in region);
    }

    /// <summary>
    /// Replaces pixels with a transparent alpha component with fully transparent pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="region">The <see cref="Buffer2DRegion{T}"/> where the transparent pixels will be changed.</param>
    public static void ReplaceTransparentPixels<TPixel>(
        Configuration configuration,
        in Buffer2DRegion<TPixel> region)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using IMemoryOwner<Vector4> vectors = configuration.MemoryAllocator.Allocate<Vector4>(region.Width);
        Span<Vector4> vectorsSpan = vectors.GetSpan();
        for (int y = 0; y < region.Height; y++)
        {
            Span<TPixel> span = region.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(configuration, span, vectorsSpan);
            ReplaceTransparentPixels(vectorsSpan);
            PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(configuration, vectorsSpan, span);
        }
    }

    /// <summary>
    /// Replaces pixels with a transparent alpha component with fully transparent pixels.
    /// </summary>
    /// <param name="source">A span of color vectors that will be checked for transparency and potentially modified.</param>
    public static void ReplaceTransparentPixels(Span<Vector4> source)
    {
        if (Vector512.IsHardwareAccelerated && source.Length >= 4)
        {
            Span<Vector512<float>> source512 = MemoryMarshal.Cast<Vector4, Vector512<float>>(source);
            for (int i = 0; i < source512.Length; i++)
            {
                ref Vector512<float> v = ref source512[i];

                // Do `vector < threshold`
                Vector512<float> mask = Vector512.Equals(v, Vector512<float>.Zero);

                // Replicate the result for W to all elements (is AllBitsSet if the W was 0 and Zero otherwise)
                mask = Vector512.Shuffle(mask, Vector512.Create(3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15));

                // Use the mask to select the replacement vector
                // (replacement & mask) | (v512 & ~mask)
                v = Vector512.ConditionalSelect(mask, Vector512<float>.Zero, v);
            }

            int m = Numerics.Modulo4(source.Length);
            if (m != 0)
            {
                for (int i = source.Length - m; i < source.Length; i++)
                {
                    if (source[i].W == 0)
                    {
                        source[i] = Vector4.Zero;
                    }
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && source.Length >= 2)
        {
            Span<Vector256<float>> source256 = MemoryMarshal.Cast<Vector4, Vector256<float>>(source);
            for (int i = 0; i < source256.Length; i++)
            {
                ref Vector256<float> v = ref source256[i];

                // Do `vector < threshold`
                Vector256<float> mask = Vector256.Equals(v, Vector256<float>.Zero);

                // Replicate the result for W to all elements (is AllBitsSet if the W was 0 and Zero otherwise)
                mask = Vector256.Shuffle(mask, Vector256.Create(3, 3, 3, 3, 7, 7, 7, 7));

                // Use the mask to select the replacement vector
                // (replacement & mask) | (v256 & ~mask)
                v = Vector256.ConditionalSelect(mask, Vector256<float>.Zero, v);
            }

            int m = Numerics.Modulo2(source.Length);
            if (m != 0)
            {
                for (int i = source.Length - m; i < source.Length; i++)
                {
                    if (source[i].W == 0)
                    {
                        source[i] = Vector4.Zero;
                    }
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            for (int i = 0; i < source.Length; i++)
            {
                ref Vector4 v = ref source[i];
                Vector128<float> v128 = v.AsVector128();

                // Do `vector == 0`
                Vector128<float> mask = Vector128.Equals(v128, Vector128<float>.Zero);

                // Replicate the result for W to all elements (is AllBitsSet if the W was 0 and Zero otherwise)
                mask = Vector128.Shuffle(mask, Vector128.Create(3, 3, 3, 3));

                // Use the mask to select the replacement vector
                // (replacement & mask) | (v128 & ~mask)
                v = Vector128.ConditionalSelect(mask, Vector128<float>.Zero, v128).AsVector4();
            }
        }
        else
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].W == 0F)
                {
                    source[i] = Vector4.Zero;
                }
            }
        }
    }

    /// <summary>
    /// Packs one row of palette indices with <typeparamref name="TPacker"/>.
    /// </summary>
    /// <typeparam name="TPacker">The index packer of the bit depth.</typeparam>
    /// <param name="indices">The indices, one byte each. Each index fits in <c>TPacker.BitsPerIndex</c> bits.</param>
    /// <param name="destination">The packed row.</param>
    private static void PackIndices<TPacker>(ReadOnlySpan<byte> indices, Span<byte> destination)
        where TPacker : struct, IIndexPacker
    {
        // IndicesPerByte is a constant of the packer, so the JIT changes the divisions and multiplications to shifts.
        int wholeBytes = indices.Length / TPacker.IndicesPerByte;
        int remainder = indices.Length % TPacker.IndicesPerByte;
        DebugGuard.MustBeGreaterThanOrEqualTo(destination.Length, wholeBytes + (remainder == 0 ? 0 : 1), nameof(destination));

        ref byte sourceBase = ref MemoryMarshal.GetReference(indices);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        int x = 0;

        // Destination byte x holds source indices x * IndicesPerByte to (x + 1) * IndicesPerByte - 1. Each vector step
        // writes only whole destination bytes, so it never writes past the packed row. A step leaves fewer bytes than
        // its vector width to the next, smaller step.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x <= wholeBytes - Vector512<byte>.Count; x += Vector512<byte>.Count)
            {
                TPacker.Pack512(ref Unsafe.Add(ref sourceBase, (uint)(x * TPacker.IndicesPerByte)))
                    .StoreUnsafe(ref destinationBase, (uint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x <= wholeBytes - Vector256<byte>.Count; x += Vector256<byte>.Count)
            {
                TPacker.Pack256(ref Unsafe.Add(ref sourceBase, (uint)(x * TPacker.IndicesPerByte)))
                    .StoreUnsafe(ref destinationBase, (uint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x <= wholeBytes - Vector128<byte>.Count; x += Vector128<byte>.Count)
            {
                TPacker.Pack128(ref Unsafe.Add(ref sourceBase, (uint)(x * TPacker.IndicesPerByte)))
                    .StoreUnsafe(ref destinationBase, (uint)x);
            }
        }

        for (; x < wholeBytes; x++)
        {
            Unsafe.Add(ref destinationBase, (uint)x) = TPacker.Pack(ref Unsafe.Add(ref sourceBase, (uint)(x * TPacker.IndicesPerByte)));
        }

        // When the row ends inside the last byte, the first remaining index goes in the top bits. Each next index goes
        // below the previous one. The low bits that have no index stay zero.
        if (remainder != 0)
        {
            uint value = 0;
            int i = wholeBytes * TPacker.IndicesPerByte;
            for (int shift = 8 - TPacker.BitsPerIndex; i < indices.Length; i++, shift -= TPacker.BitsPerIndex)
            {
                value |= (uint)Unsafe.Add(ref sourceBase, (uint)i) << shift;
            }

            Unsafe.Add(ref destinationBase, (uint)wholeBytes) = (byte)value;
        }
    }

    /// <summary>
    /// Joins each pair of adjacent values into one byte, the first value of the pair in the high bits.
    /// </summary>
    /// <param name="first">The first 8 pairs.</param>
    /// <param name="second">The next 8 pairs.</param>
    /// <param name="valueBits">The bit count of one value: 1, 2 or 4.</param>
    /// <returns>The 16 joined bytes, in pair order. Each byte holds a value of 2 * <paramref name="valueBits"/> bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CombinePairs(Vector128<byte> first, Vector128<byte> second, int valueBits)
    {
        // A 16-bit lane holds one pair. The vector layout is little endian, so the first value is in the low byte and
        // the second value is in the high byte. The left shift moves the first value up by valueBits bits. The right shift
        // moves the second value to the low byte. Each value has valueBits bits, so the two values do not overlap, and
        // the low byte of the lane is (firstValue << valueBits) | secondValue. Narrow keeps the low byte of each lane.
        Vector128<ushort> a = first.AsUInt16();
        Vector128<ushort> b = second.AsUInt16();
        return Vector128.Narrow((a << valueBits) | (a >> 8), (b << valueBits) | (b >> 8));
    }

    /// <summary>
    /// Joins each pair of adjacent values into one byte, the first value of the pair in the high bits.
    /// </summary>
    /// <param name="first">The first 16 pairs.</param>
    /// <param name="second">The next 16 pairs.</param>
    /// <param name="valueBits">The bit count of one value: 1, 2 or 4.</param>
    /// <returns>The 32 joined bytes, in pair order. Each byte holds a value of 2 * <paramref name="valueBits"/> bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> CombinePairs(Vector256<byte> first, Vector256<byte> second, int valueBits)
    {
        // The lanes are the same as in the Vector128 form. Narrow keeps the element order across the 128-bit halves.
        Vector256<ushort> a = first.AsUInt16();
        Vector256<ushort> b = second.AsUInt16();
        return Vector256.Narrow((a << valueBits) | (a >> 8), (b << valueBits) | (b >> 8));
    }

    /// <summary>
    /// Joins each pair of adjacent values into one byte, the first value of the pair in the high bits.
    /// </summary>
    /// <param name="first">The first 32 pairs.</param>
    /// <param name="second">The next 32 pairs.</param>
    /// <param name="valueBits">The bit count of one value: 1, 2 or 4.</param>
    /// <returns>The 64 joined bytes, in pair order. Each byte holds a value of 2 * <paramref name="valueBits"/> bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> CombinePairs(Vector512<byte> first, Vector512<byte> second, int valueBits)
    {
        // The lanes are the same as in the Vector128 form. Narrow keeps the element order across the 128-bit blocks.
        Vector512<ushort> a = first.AsUInt16();
        Vector512<ushort> b = second.AsUInt16();
        return Vector512.Narrow((a << valueBits) | (a >> 8), (b << valueBits) | (b >> 8));
    }

    /// <summary>
    /// Packs 8 indices of 1 bit into one byte.
    /// </summary>
    private readonly struct Index1Packer : IIndexPacker
    {
        /// <inheritdoc/>
        public static int BitsPerIndex => 1;

        /// <inheritdoc/>
        public static int IndicesPerByte => 8;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Pack(ref byte source)
        {
            // Index k (0 to 7) is bit 8k of the little-endian value. The multiplier has bits 63 - 9k, so the product
            // moves index k to bit 63 - k: index 0 to bit 63, index 7 to bit 56. The other partial products go to bit
            // 64 or higher, which the multiply drops, or to separate bits below bit 56. No sum carries, so the top
            // byte is exactly [i0 i1 i2 i3 i4 i5 i6 i7].
            ulong value = Unsafe.ReadUnaligned<ulong>(ref source);
            return (byte)((value * 0x8040201008040201UL) >> 56);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Pack128(ref byte source)
        {
            // Three pair joins change 1-bit values to 2-bit values, then to 4-bit values, then to bytes.
            Vector128<byte> v0 = CombinePairs(Vector128.LoadUnsafe(ref source), Vector128.LoadUnsafe(ref source, 16), 1);
            Vector128<byte> v1 = CombinePairs(Vector128.LoadUnsafe(ref source, 32), Vector128.LoadUnsafe(ref source, 48), 1);
            Vector128<byte> v2 = CombinePairs(Vector128.LoadUnsafe(ref source, 64), Vector128.LoadUnsafe(ref source, 80), 1);
            Vector128<byte> v3 = CombinePairs(Vector128.LoadUnsafe(ref source, 96), Vector128.LoadUnsafe(ref source, 112), 1);
            return CombinePairs(CombinePairs(v0, v1, 2), CombinePairs(v2, v3, 2), 4);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Pack256(ref byte source)
        {
            // Three pair joins change 1-bit values to 2-bit values, then to 4-bit values, then to bytes.
            Vector256<byte> v0 = CombinePairs(Vector256.LoadUnsafe(ref source), Vector256.LoadUnsafe(ref source, 32), 1);
            Vector256<byte> v1 = CombinePairs(Vector256.LoadUnsafe(ref source, 64), Vector256.LoadUnsafe(ref source, 96), 1);
            Vector256<byte> v2 = CombinePairs(Vector256.LoadUnsafe(ref source, 128), Vector256.LoadUnsafe(ref source, 160), 1);
            Vector256<byte> v3 = CombinePairs(Vector256.LoadUnsafe(ref source, 192), Vector256.LoadUnsafe(ref source, 224), 1);
            return CombinePairs(CombinePairs(v0, v1, 2), CombinePairs(v2, v3, 2), 4);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Pack512(ref byte source)
        {
            // Three pair joins change 1-bit values to 2-bit values, then to 4-bit values, then to bytes.
            Vector512<byte> v0 = CombinePairs(Vector512.LoadUnsafe(ref source), Vector512.LoadUnsafe(ref source, 64), 1);
            Vector512<byte> v1 = CombinePairs(Vector512.LoadUnsafe(ref source, 128), Vector512.LoadUnsafe(ref source, 192), 1);
            Vector512<byte> v2 = CombinePairs(Vector512.LoadUnsafe(ref source, 256), Vector512.LoadUnsafe(ref source, 320), 1);
            Vector512<byte> v3 = CombinePairs(Vector512.LoadUnsafe(ref source, 384), Vector512.LoadUnsafe(ref source, 448), 1);
            return CombinePairs(CombinePairs(v0, v1, 2), CombinePairs(v2, v3, 2), 4);
        }
    }

    /// <summary>
    /// Packs 4 indices of 2 bits into one byte.
    /// </summary>
    private readonly struct Index2Packer : IIndexPacker
    {
        /// <inheritdoc/>
        public static int BitsPerIndex => 2;

        /// <inheritdoc/>
        public static int IndicesPerByte => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Pack(ref byte source)
        {
            // Index k (0 to 3) is bits 8k and 8k + 1 of the little-endian value. The multiplier has bits 30 - 10k, so
            // the product moves index k to bits 30 - 2k and 31 - 2k: index 0 to bits 30-31, index 3 to bits 24-25.
            // The other partial products go to bit 32 or higher, which the multiply drops, or to separate 2-bit fields
            // below bit 24. No sum carries, so the top byte is exactly [i0 i0 i1 i1 i2 i2 i3 i3].
            uint value = Unsafe.ReadUnaligned<uint>(ref source);
            return (byte)((value * 0x40100401U) >> 24);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Pack128(ref byte source)
        {
            // Two pair joins change 2-bit values to 4-bit values, then to bytes.
            Vector128<byte> v0 = CombinePairs(Vector128.LoadUnsafe(ref source), Vector128.LoadUnsafe(ref source, 16), 2);
            Vector128<byte> v1 = CombinePairs(Vector128.LoadUnsafe(ref source, 32), Vector128.LoadUnsafe(ref source, 48), 2);
            return CombinePairs(v0, v1, 4);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Pack256(ref byte source)
        {
            // Two pair joins change 2-bit values to 4-bit values, then to bytes.
            Vector256<byte> v0 = CombinePairs(Vector256.LoadUnsafe(ref source), Vector256.LoadUnsafe(ref source, 32), 2);
            Vector256<byte> v1 = CombinePairs(Vector256.LoadUnsafe(ref source, 64), Vector256.LoadUnsafe(ref source, 96), 2);
            return CombinePairs(v0, v1, 4);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Pack512(ref byte source)
        {
            // Two pair joins change 2-bit values to 4-bit values, then to bytes.
            Vector512<byte> v0 = CombinePairs(Vector512.LoadUnsafe(ref source), Vector512.LoadUnsafe(ref source, 64), 2);
            Vector512<byte> v1 = CombinePairs(Vector512.LoadUnsafe(ref source, 128), Vector512.LoadUnsafe(ref source, 192), 2);
            return CombinePairs(v0, v1, 4);
        }
    }

    /// <summary>
    /// Packs 2 indices of 4 bits into one byte.
    /// </summary>
    private readonly struct Index4Packer : IIndexPacker
    {
        /// <inheritdoc/>
        public static int BitsPerIndex => 4;

        /// <inheritdoc/>
        public static int IndicesPerByte => 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Pack(ref byte source)
        {
            // The little-endian value has index 0 in bits 0-3 and index 1 in bits 8-11. The low byte of
            // (value << 4) | (value >> 8) is [i0 i0 i0 i0 i1 i1 i1 i1]. The cast drops the high byte.
            ushort value = Unsafe.ReadUnaligned<ushort>(ref source);
            return (byte)((value << 4) | (value >> 8));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Pack128(ref byte source)
            => CombinePairs(Vector128.LoadUnsafe(ref source), Vector128.LoadUnsafe(ref source, 16), 4);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Pack256(ref byte source)
            => CombinePairs(Vector256.LoadUnsafe(ref source), Vector256.LoadUnsafe(ref source, 32), 4);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Pack512(ref byte source)
            => CombinePairs(Vector512.LoadUnsafe(ref source), Vector512.LoadUnsafe(ref source, 64), 4);
    }
}
