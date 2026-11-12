// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

internal static partial class SimdUtils
{
    /// <summary>
    /// Gets the byte shuffle that spreads four 3-byte pixels to four 32-bit lanes. Index 0x80 sets byte 3 of each lane to 0.
    /// </summary>
    private static Vector128<byte> Spread3To4Mask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0, 1, 2, 0x80, 3, 4, 5, 0x80, 6, 7, 8, 0x80, 9, 10, 11, 0x80);
    }

    /// <summary>
    /// Gets the byte shuffle that joins bytes 0 to 2 of four 32-bit lanes to 12 bytes. Index 0x80 sets the last 4 bytes to 0.
    /// </summary>
    private static Vector128<byte> Join4To3Mask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 0x80, 0x80, 0x80, 0x80);
    }

    /// <summary>
    /// Gets the join byte shuffle moved four bytes up: it joins bytes 0 to 2 of four 32-bit lanes to bytes 4 to 15.
    /// Index 0x80 sets the first 4 bytes to 0.
    /// </summary>
    private static Vector128<byte> Join4To3EndMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128.Create((byte)0x80, 0x80, 0x80, 0x80, 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14);
    }

    /// <summary>
    /// Shuffle 8-bit integers within 128-bit lanes in <paramref name="source"/>
    /// using the control and store the results in <paramref name="destination"/>.
    /// </summary>
    /// <typeparam name="TShuffle">The type of shuffle struct.</typeparam>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="destination">The destination span of bytes.</param>
    /// <remarks>
    /// The kernel is not inlined. Inlined into a large caller, it shares that caller's inline budget, and its
    /// operators and masks can then stay as calls and stack values inside the loops.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Shuffle4<TShuffle>(
        ReadOnlySpan<byte> source,
        Span<byte> destination)
        where TShuffle : struct, IShuffle4
    {
        VerifyShuffle4SpanInput(source, destination);

        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = source.Length;
        int i = 0;

        // The same offset flows through descending widths. This keeps a single traversal while
        // allowing a row that is not a multiple of the widest register to retain a vectorized tail.
        if (Vector512.IsHardwareAccelerated)
        {
            int fourVectorsFromEnd = length - (Vector512<byte>.Count * 4);

            for (; i <= fourVectorsFromEnd; i += Vector512<byte>.Count * 4)
            {
                // Four independent vectors amortize loop control and expose enough work for the CPU
                // to overlap loads, byte shuffles, and stores without changing pixel ordering.
                TShuffle.Invoke(Vector512.LoadUnsafe(ref sourceBase, (nuint)i)).StoreUnsafe(ref destinationBase, (nuint)i);
                TShuffle.Invoke(Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<byte>.Count)))
                    .StoreUnsafe(ref destinationBase, (nuint)(i + Vector512<byte>.Count));

                TShuffle.Invoke(Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + (Vector512<byte>.Count * 2))))
                    .StoreUnsafe(ref destinationBase, (nuint)(i + (Vector512<byte>.Count * 2)));

                TShuffle.Invoke(Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + (Vector512<byte>.Count * 3))))
                    .StoreUnsafe(ref destinationBase, (nuint)(i + (Vector512<byte>.Count * 3)));
            }

            int oneVectorFromEnd = length - Vector512<byte>.Count;

            for (; i <= oneVectorFromEnd; i += Vector512<byte>.Count)
            {
                TShuffle.Invoke(Vector512.LoadUnsafe(ref sourceBase, (nuint)i)).StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int fourVectorsFromEnd = length - (Vector256<byte>.Count * 4);

            for (; i <= fourVectorsFromEnd; i += Vector256<byte>.Count * 4)
            {
                TShuffle.Invoke(Vector256.LoadUnsafe(ref sourceBase, (nuint)i)).StoreUnsafe(ref destinationBase, (nuint)i);
                TShuffle.Invoke(Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<byte>.Count)))
                    .StoreUnsafe(ref destinationBase, (nuint)(i + Vector256<byte>.Count));

                TShuffle.Invoke(Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + (Vector256<byte>.Count * 2))))
                    .StoreUnsafe(ref destinationBase, (nuint)(i + (Vector256<byte>.Count * 2)));

                TShuffle.Invoke(Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + (Vector256<byte>.Count * 3))))
                    .StoreUnsafe(ref destinationBase, (nuint)(i + (Vector256<byte>.Count * 3)));
            }

            int oneVectorFromEnd = length - Vector256<byte>.Count;

            for (; i <= oneVectorFromEnd; i += Vector256<byte>.Count)
            {
                TShuffle.Invoke(Vector256.LoadUnsafe(ref sourceBase, (nuint)i)).StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int fourVectorsFromEnd = length - (Vector128<byte>.Count * 4);

            for (; i <= fourVectorsFromEnd; i += Vector128<byte>.Count * 4)
            {
                TShuffle.Invoke(Vector128.LoadUnsafe(ref sourceBase, (nuint)i)).StoreUnsafe(ref destinationBase, (nuint)i);
                TShuffle.Invoke(Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<byte>.Count)))
                    .StoreUnsafe(ref destinationBase, (nuint)(i + Vector128<byte>.Count));

                TShuffle.Invoke(Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + (Vector128<byte>.Count * 2))))
                    .StoreUnsafe(ref destinationBase, (nuint)(i + (Vector128<byte>.Count * 2)));

                TShuffle.Invoke(Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + (Vector128<byte>.Count * 3))))
                    .StoreUnsafe(ref destinationBase, (nuint)(i + (Vector128<byte>.Count * 3)));
            }

            int oneVectorFromEnd = length - Vector128<byte>.Count;

            for (; i <= oneVectorFromEnd; i += Vector128<byte>.Count)
            {
                TShuffle.Invoke(Vector128.LoadUnsafe(ref sourceBase, (nuint)i)).StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        // The vector cascade leaves fewer than four pixels. A full uint load keeps each pixel
        // in a register while the closed operator resolves to its rotate, reverse, or mask sequence.
        for (; i < length; i += 4)
        {
            uint packed = Unsafe.As<byte, uint>(ref Unsafe.Add(ref sourceBase, (nuint)i));
            Unsafe.As<byte, uint>(ref Unsafe.Add(ref destinationBase, (nuint)i)) = TShuffle.Invoke(packed);
        }
    }

    /// <summary>
    /// Shuffle 8-bit integer triplets within 128-bit lanes in <paramref name="source"/>
    /// using the control and store the results in <paramref name="destination"/>.
    /// </summary>
    /// <typeparam name="TShuffle">The type of shuffle struct.</typeparam>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="destination">The destination span of bytes.</param>
    /// <remarks>
    /// The kernel is not inlined. Inlined into a large caller, it shares that caller's inline budget, and its
    /// operators and masks can then stay as calls and stack values inside the loops.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Shuffle3<TShuffle>(
        ReadOnlySpan<byte> source,
        Span<byte> destination)
        where TShuffle : struct, IShuffle3
    {
        VerifyShuffle3SpanInput(source, destination);

        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        int length = source.Length;
        int i = 0;

        // Each iteration loads three full registers of 3-byte pixels, spreads them to four registers of 4-byte
        // pixels, runs the operator, and joins them back to three full registers. Loads and stores are full
        // registers at multiples of the register size from the row start. All loads come before the stores, and
        // the stores cover exactly the loaded bytes, so an in-place shuffle is safe.
        if (Vector512.IsHardwareAccelerated)
        {
            int threeVectorsFromEnd = length - (Vector512<byte>.Count * 3);

            for (; i <= threeVectorsFromEnd; i += Vector512<byte>.Count * 3)
            {
                ref byte source0 = ref Unsafe.Add(ref sourceBase, (nuint)i);
                Spread3To4(
                    Vector512.LoadUnsafe(ref source0),
                    Vector512.LoadUnsafe(ref source0, 64),
                    Vector512.LoadUnsafe(ref source0, 128),
                    out Vector512<byte> v0,
                    out Vector512<byte> v1,
                    out Vector512<byte> v2,
                    out Vector512<byte> v3);

                Join4To3(
                    TShuffle.Invoke(v0),
                    TShuffle.Invoke(v1),
                    TShuffle.Invoke(v2),
                    TShuffle.Invoke(v3),
                    out Vector512<byte> destination0,
                    out Vector512<byte> destination1,
                    out Vector512<byte> destination2);

                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, (nuint)i);
                destination0.StoreUnsafe(ref destinationRow);
                destination1.StoreUnsafe(ref destinationRow, 64);
                destination2.StoreUnsafe(ref destinationRow, 128);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int threeVectorsFromEnd = length - (Vector256<byte>.Count * 3);

            for (; i <= threeVectorsFromEnd; i += Vector256<byte>.Count * 3)
            {
                ref byte source0 = ref Unsafe.Add(ref sourceBase, (nuint)i);
                Spread3To4(
                    Vector256.LoadUnsafe(ref source0),
                    Vector256.LoadUnsafe(ref source0, 32),
                    Vector256.LoadUnsafe(ref source0, 64),
                    out Vector256<byte> v0,
                    out Vector256<byte> v1,
                    out Vector256<byte> v2,
                    out Vector256<byte> v3);

                Join4To3(
                    TShuffle.Invoke(v0),
                    TShuffle.Invoke(v1),
                    TShuffle.Invoke(v2),
                    TShuffle.Invoke(v3),
                    out Vector256<byte> destination0,
                    out Vector256<byte> destination1,
                    out Vector256<byte> destination2);

                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, (nuint)i);
                destination0.StoreUnsafe(ref destinationRow);
                destination1.StoreUnsafe(ref destinationRow, 32);
                destination2.StoreUnsafe(ref destinationRow, 64);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int threeVectorsFromEnd = length - (Vector128<byte>.Count * 3);

            for (; i <= threeVectorsFromEnd; i += Vector128<byte>.Count * 3)
            {
                ref byte source0 = ref Unsafe.Add(ref sourceBase, (nuint)i);
                Spread3To4(
                    Vector128.LoadUnsafe(ref source0),
                    Vector128.LoadUnsafe(ref source0, 16),
                    Vector128.LoadUnsafe(ref source0, 32),
                    out Vector128<byte> v0,
                    out Vector128<byte> v1,
                    out Vector128<byte> v2,
                    out Vector128<byte> v3);

                Join4To3(
                    TShuffle.Invoke(v0),
                    TShuffle.Invoke(v1),
                    TShuffle.Invoke(v2),
                    TShuffle.Invoke(v3),
                    out Vector128<byte> destination0,
                    out Vector128<byte> destination1,
                    out Vector128<byte> destination2);

                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, (nuint)i);
                destination0.StoreUnsafe(ref destinationRow);
                destination1.StoreUnsafe(ref destinationRow, 16);
                destination2.StoreUnsafe(ref destinationRow, 32);
            }

            int oneTailVectorFromEnd = length - Vector128<byte>.Count;

            for (; i <= oneTailVectorFromEnd; i += 12)
            {
                // A single readable register contains four complete triplets plus four bytes from the following
                // pixels. The spread ignores those extra bytes, and the join writes only the four results.
                Vector128<byte> pixels = TShuffle.Invoke(Spread3To4(Vector128.LoadUnsafe(ref sourceBase, (nuint)i)));
                Join4To3(pixels, ref Unsafe.Add(ref destinationBase, (nuint)i));
            }
        }

        int widenedReadEnd = length - 3;

        for (; i < widenedReadEnd; i += 3)
        {
            // The fourth byte belongs to the following pixel, but the operator only contributes the
            // low three result bytes. This unaligned read replaces three dependent byte loads safely.
            uint packed = Unsafe.As<byte, uint>(ref Unsafe.Add(ref sourceBase, (nuint)i));
            uint shuffled = TShuffle.Invoke(packed);
            StoreLowThreeBytes(ref Unsafe.Add(ref destinationBase, (nuint)i), shuffled);
        }

        if (i < length)
        {
            // The final triplet has no fourth readable byte, so construct only this terminal pixel.
            uint packed =
                Unsafe.Add(ref sourceBase, (nuint)i) |
                ((uint)Unsafe.Add(ref sourceBase, (nuint)(i + 1)) << 8) |
                ((uint)Unsafe.Add(ref sourceBase, (nuint)(i + 2)) << 16);

            uint shuffled = TShuffle.Invoke(packed);
            StoreLowThreeBytes(ref Unsafe.Add(ref destinationBase, (nuint)i), shuffled);
        }
    }

    /// <summary>
    /// Pads then shuffles 8-bit integers within 128-bit lanes in <paramref name="source"/>
    /// using the control and store the results in <paramref name="destination"/>.
    /// </summary>
    /// <typeparam name="TShuffle">The type of shuffle struct.</typeparam>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="destination">The destination span of bytes.</param>
    /// <remarks>
    /// The kernel is not inlined. Inlined into a large caller, it shares that caller's inline budget, and its
    /// operators and masks can then stay as calls and stack values inside the loops.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Pad3Shuffle4<TShuffle>(
        ReadOnlySpan<byte> source,
        Span<byte> destination)
        where TShuffle : struct, IPad3Shuffle4
    {
        VerifyPad3Shuffle4SpanInput(source, destination);

        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        int sourceLength = source.Length;
        int sourceOffset = 0;
        int destinationOffset = 0;

        // Each iteration loads three full registers of 3-byte pixels and stores four full registers of 4-byte
        // pixels, at multiples of the register size from each row start. The spread sets byte 3 of each 32-bit
        // lane to zero, and ORing 0xFF000000 into each lane fills W with opaque alpha before the operator runs.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<byte> opaqueAlpha = Vector512.Create(0xFF000000U).AsByte();
            int threeVectorsFromEnd = sourceLength - (Vector512<byte>.Count * 3);

            for (; sourceOffset <= threeVectorsFromEnd; sourceOffset += Vector512<byte>.Count * 3, destinationOffset += Vector512<byte>.Count * 4)
            {
                ref byte source0 = ref Unsafe.Add(ref sourceBase, (nuint)sourceOffset);
                Spread3To4(
                    Vector512.LoadUnsafe(ref source0),
                    Vector512.LoadUnsafe(ref source0, 64),
                    Vector512.LoadUnsafe(ref source0, 128),
                    out Vector512<byte> v0,
                    out Vector512<byte> v1,
                    out Vector512<byte> v2,
                    out Vector512<byte> v3);

                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset);
                TShuffle.Invoke(v0 | opaqueAlpha).StoreUnsafe(ref destinationRow);
                TShuffle.Invoke(v1 | opaqueAlpha).StoreUnsafe(ref destinationRow, 64);
                TShuffle.Invoke(v2 | opaqueAlpha).StoreUnsafe(ref destinationRow, 128);
                TShuffle.Invoke(v3 | opaqueAlpha).StoreUnsafe(ref destinationRow, 192);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<byte> opaqueAlpha = Vector256.Create(0xFF000000U).AsByte();
            int threeVectorsFromEnd = sourceLength - (Vector256<byte>.Count * 3);

            for (; sourceOffset <= threeVectorsFromEnd; sourceOffset += Vector256<byte>.Count * 3, destinationOffset += Vector256<byte>.Count * 4)
            {
                ref byte source0 = ref Unsafe.Add(ref sourceBase, (nuint)sourceOffset);
                Spread3To4(
                    Vector256.LoadUnsafe(ref source0),
                    Vector256.LoadUnsafe(ref source0, 32),
                    Vector256.LoadUnsafe(ref source0, 64),
                    out Vector256<byte> v0,
                    out Vector256<byte> v1,
                    out Vector256<byte> v2,
                    out Vector256<byte> v3);

                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset);
                TShuffle.Invoke(v0 | opaqueAlpha).StoreUnsafe(ref destinationRow);
                TShuffle.Invoke(v1 | opaqueAlpha).StoreUnsafe(ref destinationRow, 32);
                TShuffle.Invoke(v2 | opaqueAlpha).StoreUnsafe(ref destinationRow, 64);
                TShuffle.Invoke(v3 | opaqueAlpha).StoreUnsafe(ref destinationRow, 96);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<byte> opaqueAlpha = Vector128.Create(0xFF000000U).AsByte();
            int threeVectorsFromEnd = sourceLength - (Vector128<byte>.Count * 3);

            for (; sourceOffset <= threeVectorsFromEnd; sourceOffset += Vector128<byte>.Count * 3, destinationOffset += Vector128<byte>.Count * 4)
            {
                ref byte source0 = ref Unsafe.Add(ref sourceBase, (nuint)sourceOffset);
                Spread3To4(
                    Vector128.LoadUnsafe(ref source0),
                    Vector128.LoadUnsafe(ref source0, 16),
                    Vector128.LoadUnsafe(ref source0, 32),
                    out Vector128<byte> v0,
                    out Vector128<byte> v1,
                    out Vector128<byte> v2,
                    out Vector128<byte> v3);

                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset);
                TShuffle.Invoke(v0 | opaqueAlpha).StoreUnsafe(ref destinationRow);
                TShuffle.Invoke(v1 | opaqueAlpha).StoreUnsafe(ref destinationRow, 16);
                TShuffle.Invoke(v2 | opaqueAlpha).StoreUnsafe(ref destinationRow, 32);
                TShuffle.Invoke(v3 | opaqueAlpha).StoreUnsafe(ref destinationRow, 48);
            }

            int oneTailVectorFromEnd = sourceLength - Vector128<byte>.Count;

            for (; sourceOffset <= oneTailVectorFromEnd; sourceOffset += 12, destinationOffset += Vector128<byte>.Count)
            {
                // A single readable register contains four complete triplets plus four bytes from the following
                // pixels. The spread ignores those extra bytes.
                Vector128<byte> pixels = Spread3To4(Vector128.LoadUnsafe(ref sourceBase, (nuint)sourceOffset)) | opaqueAlpha;
                TShuffle.Invoke(pixels).StoreUnsafe(ref destinationBase, (nuint)destinationOffset);
            }
        }

        int widenedReadEnd = sourceLength - 3;

        for (; sourceOffset < widenedReadEnd; sourceOffset += 3, destinationOffset += 4)
        {
            // The widened load intentionally includes the next pixel's first byte. Replacing that
            // high byte with opaque alpha yields the complete XYZW value with one unaligned read.
            uint packed = Unsafe.As<byte, uint>(ref Unsafe.Add(ref sourceBase, (nuint)sourceOffset)) | 0xFF000000;
            Unsafe.As<byte, uint>(ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset)) = TShuffle.Invoke(packed);
        }

        if (sourceOffset < sourceLength)
        {
            // The final triplet cannot use the widened load because no following byte is in range.
            uint packed =
                Unsafe.Add(ref sourceBase, (nuint)sourceOffset) |
                ((uint)Unsafe.Add(ref sourceBase, (nuint)(sourceOffset + 1)) << 8) |
                ((uint)Unsafe.Add(ref sourceBase, (nuint)(sourceOffset + 2)) << 16) |
                0xFF000000;

            Unsafe.As<byte, uint>(ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset)) = TShuffle.Invoke(packed);
        }
    }

    /// <summary>
    /// Shuffles then slices 8-bit integers within 128-bit lanes in <paramref name="source"/>
    /// using the control and store the results in <paramref name="destination"/>.
    /// </summary>
    /// <typeparam name="TShuffle">The type of shuffle struct.</typeparam>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="destination">The destination span of bytes.</param>
    /// <remarks>
    /// The kernel is not inlined. Inlined into a large caller, it shares that caller's inline budget, and its
    /// operators and masks can then stay as calls and stack values inside the loops.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Shuffle4Slice3<TShuffle>(
        ReadOnlySpan<byte> source,
        Span<byte> destination)
        where TShuffle : struct, IShuffle4Slice3
    {
        VerifyShuffle4Slice3SpanInput(source, destination);

        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        int sourceLength = source.Length;
        int sourceOffset = 0;
        int destinationOffset = 0;

        // Each iteration loads four full registers of 4-byte pixels, runs the operator, joins them to three full
        // registers of 3-byte pixels, and stores those, at multiples of the register size from each row start. All
        // loads come before the stores, and a store ends at 3/4 of the loaded source span, so an in-place shrink
        // never overwrites a pixel that a later iteration reads.
        if (Vector512.IsHardwareAccelerated)
        {
            int fourVectorsFromEnd = sourceLength - (Vector512<byte>.Count * 4);

            for (; sourceOffset <= fourVectorsFromEnd; sourceOffset += Vector512<byte>.Count * 4, destinationOffset += Vector512<byte>.Count * 3)
            {
                ref byte source0 = ref Unsafe.Add(ref sourceBase, (nuint)sourceOffset);
                Join4To3(
                    TShuffle.Invoke(Vector512.LoadUnsafe(ref source0)),
                    TShuffle.Invoke(Vector512.LoadUnsafe(ref source0, 64)),
                    TShuffle.Invoke(Vector512.LoadUnsafe(ref source0, 128)),
                    TShuffle.Invoke(Vector512.LoadUnsafe(ref source0, 192)),
                    out Vector512<byte> destination0,
                    out Vector512<byte> destination1,
                    out Vector512<byte> destination2);

                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset);
                destination0.StoreUnsafe(ref destinationRow);
                destination1.StoreUnsafe(ref destinationRow, 64);
                destination2.StoreUnsafe(ref destinationRow, 128);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int fourVectorsFromEnd = sourceLength - (Vector256<byte>.Count * 4);

            for (; sourceOffset <= fourVectorsFromEnd; sourceOffset += Vector256<byte>.Count * 4, destinationOffset += Vector256<byte>.Count * 3)
            {
                ref byte source0 = ref Unsafe.Add(ref sourceBase, (nuint)sourceOffset);
                Join4To3(
                    TShuffle.Invoke(Vector256.LoadUnsafe(ref source0)),
                    TShuffle.Invoke(Vector256.LoadUnsafe(ref source0, 32)),
                    TShuffle.Invoke(Vector256.LoadUnsafe(ref source0, 64)),
                    TShuffle.Invoke(Vector256.LoadUnsafe(ref source0, 96)),
                    out Vector256<byte> destination0,
                    out Vector256<byte> destination1,
                    out Vector256<byte> destination2);

                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset);
                destination0.StoreUnsafe(ref destinationRow);
                destination1.StoreUnsafe(ref destinationRow, 32);
                destination2.StoreUnsafe(ref destinationRow, 64);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int fourVectorsFromEnd = sourceLength - (Vector128<byte>.Count * 4);

            for (; sourceOffset <= fourVectorsFromEnd; sourceOffset += Vector128<byte>.Count * 4, destinationOffset += Vector128<byte>.Count * 3)
            {
                ref byte source0 = ref Unsafe.Add(ref sourceBase, (nuint)sourceOffset);
                Join4To3(
                    TShuffle.Invoke(Vector128.LoadUnsafe(ref source0)),
                    TShuffle.Invoke(Vector128.LoadUnsafe(ref source0, 16)),
                    TShuffle.Invoke(Vector128.LoadUnsafe(ref source0, 32)),
                    TShuffle.Invoke(Vector128.LoadUnsafe(ref source0, 48)),
                    out Vector128<byte> destination0,
                    out Vector128<byte> destination1,
                    out Vector128<byte> destination2);

                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset);
                destination0.StoreUnsafe(ref destinationRow);
                destination1.StoreUnsafe(ref destinationRow, 16);
                destination2.StoreUnsafe(ref destinationRow, 32);
            }

            int oneTailVectorFromEnd = sourceLength - Vector128<byte>.Count;

            for (; sourceOffset <= oneTailVectorFromEnd; sourceOffset += Vector128<byte>.Count, destinationOffset += 12)
            {
                // The join writes the four triplets as exactly twelve bytes.
                Vector128<byte> pixels = TShuffle.Invoke(Vector128.LoadUnsafe(ref sourceBase, (nuint)sourceOffset));
                Join4To3(pixels, ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset));
            }
        }

        int fourPixelsFromEnd = sourceLength - 16;

        for (; sourceOffset <= fourPixelsFromEnd; sourceOffset += 16, destinationOffset += 12)
        {
            // Transform four complete pixels before the first three-byte store. Keeping the source
            // values in registers avoids reloads after an in-place shrinking destination advances.
            uint packed0 = TShuffle.Invoke(Unsafe.As<byte, uint>(ref Unsafe.Add(ref sourceBase, (nuint)sourceOffset)));
            uint packed1 = TShuffle.Invoke(Unsafe.As<byte, uint>(ref Unsafe.Add(ref sourceBase, (nuint)(sourceOffset + 4))));
            uint packed2 = TShuffle.Invoke(Unsafe.As<byte, uint>(ref Unsafe.Add(ref sourceBase, (nuint)(sourceOffset + 8))));
            uint packed3 = TShuffle.Invoke(Unsafe.As<byte, uint>(ref Unsafe.Add(ref sourceBase, (nuint)(sourceOffset + 12))));

            StoreLowThreeBytes(ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset), packed0);
            StoreLowThreeBytes(ref Unsafe.Add(ref destinationBase, (nuint)(destinationOffset + 3)), packed1);
            StoreLowThreeBytes(ref Unsafe.Add(ref destinationBase, (nuint)(destinationOffset + 6)), packed2);
            StoreLowThreeBytes(ref Unsafe.Add(ref destinationBase, (nuint)(destinationOffset + 9)), packed3);
        }

        for (; sourceOffset < sourceLength; sourceOffset += 4, destinationOffset += 3)
        {
            uint packed = TShuffle.Invoke(Unsafe.As<byte, uint>(ref Unsafe.Add(ref sourceBase, (nuint)sourceOffset)));
            StoreLowThreeBytes(ref Unsafe.Add(ref destinationBase, (nuint)destinationOffset), packed);
        }
    }

    /// <summary>
    /// Spreads four packed 3-byte pixels to four 32-bit lanes.
    /// </summary>
    /// <param name="source">The pixels in bytes 0 to 11. Bytes 12 to 15 are not used.</param>
    /// <returns>The pixels, one in each 32-bit lane, with byte 3 of each lane set to 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<byte> Spread3To4(Vector128<byte> source)
        => Vector128.ShuffleNative(source, Spread3To4Mask);

    /// <summary>
    /// Spreads sixteen packed 3-byte pixels in three registers to four registers of 32-bit lanes.
    /// </summary>
    /// <param name="source0">Source bytes 0 to 15.</param>
    /// <param name="source1">Source bytes 16 to 31.</param>
    /// <param name="source2">Source bytes 32 to 47.</param>
    /// <param name="pixels0">Pixels 0 to 3, one in each 32-bit lane, with byte 3 of each lane set to 0.</param>
    /// <param name="pixels1">Pixels 4 to 7, in the same layout.</param>
    /// <param name="pixels2">Pixels 8 to 11, in the same layout.</param>
    /// <param name="pixels3">Pixels 12 to 15, in the same layout.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Spread3To4(
        Vector128<byte> source0,
        Vector128<byte> source1,
        Vector128<byte> source2,
        out Vector128<byte> pixels0,
        out Vector128<byte> pixels1,
        out Vector128<byte> pixels2,
        out Vector128<byte> pixels3)
    {
        // Pixel group k (pixels 4k to 4k + 3) starts at byte 12k. AlignRight and a byte shift move each group to
        // bytes 0 to 11 of its own register, and the spread mask then puts each pixel in a 32-bit lane.
        Vector128<byte> mask = Spread3To4Mask;
        pixels0 = Vector128.ShuffleNative(source0, mask);
        pixels1 = Vector128.ShuffleNative(Vector128_.AlignRight(source1, source0, 12), mask);
        pixels2 = Vector128.ShuffleNative(Vector128_.AlignRight(source2, source1, 8), mask);
        pixels3 = Vector128.ShuffleNative(Vector128_.ShiftRightBytesInVector(source2, 4), mask);
    }

    /// <summary>
    /// Spreads 32 packed 3-byte pixels in three registers to four registers of 32-bit lanes.
    /// </summary>
    /// <param name="source0">Source bytes 0 to 31.</param>
    /// <param name="source1">Source bytes 32 to 63.</param>
    /// <param name="source2">Source bytes 64 to 95.</param>
    /// <param name="pixels0">Pixels 0 to 7, one in each 32-bit lane, with byte 3 of each lane set to 0.</param>
    /// <param name="pixels1">Pixels 8 to 15, in the same layout.</param>
    /// <param name="pixels2">Pixels 16 to 23, in the same layout.</param>
    /// <param name="pixels3">Pixels 24 to 31, in the same layout.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Spread3To4(
        Vector256<byte> source0,
        Vector256<byte> source1,
        Vector256<byte> source2,
        out Vector256<byte> pixels0,
        out Vector256<byte> pixels1,
        out Vector256<byte> pixels2,
        out Vector256<byte> pixels3)
    {
        // The three sources hold 32-bit elements 0 to 23. Pixel group k (pixels 8k to 8k + 7) is in elements 6k to
        // 6k + 5. A two-source permute copies elements 6k to 6k + 2 to lane 0 and 6k + 3 to 6k + 5 to lane 1, at
        // elements 0 to 2 of each lane. Element 3 of each lane repeats element 2, and the spread mask ignores it.
        // Each permute index addresses the 16 elements of its two sources: the first source is 0 to 7.
        Vector128<byte> mask = Spread3To4Mask;
        Vector256<int> s0 = source0.AsInt32();
        Vector256<int> s1 = source1.AsInt32();
        Vector256<int> s2 = source2.AsInt32();

        pixels0 = Vector256_.ShufflePerLane(Vector256_.PermuteVar8x32x2(s0, Vector256.Create(0, 1, 2, 2, 3, 4, 5, 5), s1).AsByte(), mask);
        pixels1 = Vector256_.ShufflePerLane(Vector256_.PermuteVar8x32x2(s0, Vector256.Create(6, 7, 8, 8, 9, 10, 11, 11), s1).AsByte(), mask);
        pixels2 = Vector256_.ShufflePerLane(Vector256_.PermuteVar8x32x2(s1, Vector256.Create(4, 5, 6, 6, 7, 8, 9, 9), s2).AsByte(), mask);
        pixels3 = Vector256_.ShufflePerLane(Vector256_.PermuteVar8x32x2(s1, Vector256.Create(10, 11, 12, 12, 13, 14, 15, 15), s2).AsByte(), mask);
    }

    /// <summary>
    /// Spreads 64 packed 3-byte pixels in three registers to four registers of 32-bit lanes.
    /// </summary>
    /// <param name="source0">Source bytes 0 to 63.</param>
    /// <param name="source1">Source bytes 64 to 127.</param>
    /// <param name="source2">Source bytes 128 to 191.</param>
    /// <param name="pixels0">Pixels 0 to 15, one in each 32-bit lane, with byte 3 of each lane set to 0.</param>
    /// <param name="pixels1">Pixels 16 to 31, in the same layout.</param>
    /// <param name="pixels2">Pixels 32 to 47, in the same layout.</param>
    /// <param name="pixels3">Pixels 48 to 63, in the same layout.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Spread3To4(
        Vector512<byte> source0,
        Vector512<byte> source1,
        Vector512<byte> source2,
        out Vector512<byte> pixels0,
        out Vector512<byte> pixels1,
        out Vector512<byte> pixels2,
        out Vector512<byte> pixels3)
    {
        // The three sources hold 32-bit elements 0 to 47. Pixel group k (pixels 16k to 16k + 15) is in elements
        // 12k to 12k + 11. A two-source permute copies elements 12k + 3j to 12k + 3j + 2 to elements 0 to 2 of
        // lane j. Element 3 of each lane repeats element 2, and the spread mask ignores it. Each permute index
        // addresses the 32 elements of its two sources: the first source is 0 to 15.
        Vector128<byte> mask = Spread3To4Mask;
        Vector512<int> s0 = source0.AsInt32();
        Vector512<int> s1 = source1.AsInt32();
        Vector512<int> s2 = source2.AsInt32();

        Vector512<int> order0 = Vector512.Create(0, 1, 2, 2, 3, 4, 5, 5, 6, 7, 8, 8, 9, 10, 11, 11);
        Vector512<int> order1 = Vector512.Create(12, 13, 14, 14, 15, 16, 17, 17, 18, 19, 20, 20, 21, 22, 23, 23);
        Vector512<int> order2 = Vector512.Create(8, 9, 10, 10, 11, 12, 13, 13, 14, 15, 16, 16, 17, 18, 19, 19);
        Vector512<int> order3 = Vector512.Create(20, 21, 22, 22, 23, 24, 25, 25, 26, 27, 28, 28, 29, 30, 31, 31);

        pixels0 = Vector512_.ShufflePerLane(Vector512_.PermuteVar16x32x2(s0, order0, s1).AsByte(), mask);
        pixels1 = Vector512_.ShufflePerLane(Vector512_.PermuteVar16x32x2(s0, order1, s1).AsByte(), mask);
        pixels2 = Vector512_.ShufflePerLane(Vector512_.PermuteVar16x32x2(s1, order2, s2).AsByte(), mask);
        pixels3 = Vector512_.ShufflePerLane(Vector512_.PermuteVar16x32x2(s1, order3, s2).AsByte(), mask);
    }

    /// <summary>
    /// Joins bytes 0 to 2 of four 32-bit lanes and writes them as four packed 3-byte pixels.
    /// </summary>
    /// <param name="source">The pixels, one in each 32-bit lane. Byte 3 of each lane is not used.</param>
    /// <param name="destination">The first destination byte. Exactly 12 bytes are written.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Join4To3(Vector128<byte> source, ref byte destination)
    {
        // The join puts the twelve bytes at the start of the register. One 64-bit and one 32-bit store write them.
        Vector128<byte> joined = Vector128.ShuffleNative(source, Join4To3Mask);
        Unsafe.As<byte, ulong>(ref destination) = joined.AsUInt64().ToScalar();
        Unsafe.As<byte, uint>(ref Unsafe.Add(ref destination, 8)) = joined.AsUInt32().GetElement(2);
    }

    /// <summary>
    /// Joins four registers of 32-bit lanes to sixteen packed 3-byte pixels in three registers.
    /// </summary>
    /// <param name="pixels0">Pixels 0 to 3, one in each 32-bit lane. Byte 3 of each lane is not used.</param>
    /// <param name="pixels1">Pixels 4 to 7, in the same layout.</param>
    /// <param name="pixels2">Pixels 8 to 11, in the same layout.</param>
    /// <param name="pixels3">Pixels 12 to 15, in the same layout.</param>
    /// <param name="destination0">Destination bytes 0 to 15.</param>
    /// <param name="destination1">Destination bytes 16 to 31.</param>
    /// <param name="destination2">Destination bytes 32 to 47.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Join4To3(
        Vector128<byte> pixels0,
        Vector128<byte> pixels1,
        Vector128<byte> pixels2,
        Vector128<byte> pixels3,
        out Vector128<byte> destination0,
        out Vector128<byte> destination1,
        out Vector128<byte> destination2)
    {
        // The join mask packs four pixels into bytes 0 to 11, and the end mask packs them into bytes 4 to 15.
        // Alternating the two alignments lets AlignRight stitch the four 12-byte results into three full registers.
        Vector128<byte> mask = Join4To3Mask;
        Vector128<byte> endMask = Join4To3EndMask;
        Vector128<byte> v0 = Vector128.ShuffleNative(pixels0, endMask);
        Vector128<byte> v1 = Vector128.ShuffleNative(pixels1, mask);
        Vector128<byte> v2 = Vector128.ShuffleNative(pixels2, endMask);
        Vector128<byte> v3 = Vector128.ShuffleNative(pixels3, mask);

        destination0 = Vector128_.AlignRight(v1, v0, 4);
        destination1 = Vector128_.AlignRight(Vector128_.ShiftRightBytesInVector(v2, 4), Vector128_.ShiftLeftBytesInVector(v1, 4), 8);
        destination2 = Vector128_.AlignRight(v3, v2, 12);
    }

    /// <summary>
    /// Joins four registers of 32-bit lanes to 32 packed 3-byte pixels in three registers.
    /// </summary>
    /// <param name="pixels0">Pixels 0 to 7, one in each 32-bit lane. Byte 3 of each lane is not used.</param>
    /// <param name="pixels1">Pixels 8 to 15, in the same layout.</param>
    /// <param name="pixels2">Pixels 16 to 23, in the same layout.</param>
    /// <param name="pixels3">Pixels 24 to 31, in the same layout.</param>
    /// <param name="destination0">Destination bytes 0 to 31.</param>
    /// <param name="destination1">Destination bytes 32 to 63.</param>
    /// <param name="destination2">Destination bytes 64 to 95.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Join4To3(
        Vector256<byte> pixels0,
        Vector256<byte> pixels1,
        Vector256<byte> pixels2,
        Vector256<byte> pixels3,
        out Vector256<byte> destination0,
        out Vector256<byte> destination1,
        out Vector256<byte> destination2)
    {
        // The join mask packs the four pixels of each 128-bit lane into elements 0 to 2 of that lane, so each
        // register holds its 24 bytes in elements 0, 1, 2, 4, 5 and 6. Two-source permutes then copy those
        // elements, in order, to three full registers. Each permute index addresses the 16 elements of its two
        // sources: the first source is 0 to 7.
        Vector128<byte> mask = Join4To3Mask;
        Vector256<int> v0 = Vector256_.ShufflePerLane(pixels0, mask).AsInt32();
        Vector256<int> v1 = Vector256_.ShufflePerLane(pixels1, mask).AsInt32();
        Vector256<int> v2 = Vector256_.ShufflePerLane(pixels2, mask).AsInt32();
        Vector256<int> v3 = Vector256_.ShufflePerLane(pixels3, mask).AsInt32();

        destination0 = Vector256_.PermuteVar8x32x2(v0, Vector256.Create(0, 1, 2, 4, 5, 6, 8, 9), v1).AsByte();
        destination1 = Vector256_.PermuteVar8x32x2(v1, Vector256.Create(2, 4, 5, 6, 8, 9, 10, 12), v2).AsByte();
        destination2 = Vector256_.PermuteVar8x32x2(v2, Vector256.Create(5, 6, 8, 9, 10, 12, 13, 14), v3).AsByte();
    }

    /// <summary>
    /// Joins four registers of 32-bit lanes to 64 packed 3-byte pixels in three registers.
    /// </summary>
    /// <param name="pixels0">Pixels 0 to 15, one in each 32-bit lane. Byte 3 of each lane is not used.</param>
    /// <param name="pixels1">Pixels 16 to 31, in the same layout.</param>
    /// <param name="pixels2">Pixels 32 to 47, in the same layout.</param>
    /// <param name="pixels3">Pixels 48 to 63, in the same layout.</param>
    /// <param name="destination0">Destination bytes 0 to 63.</param>
    /// <param name="destination1">Destination bytes 64 to 127.</param>
    /// <param name="destination2">Destination bytes 128 to 191.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Join4To3(
        Vector512<byte> pixels0,
        Vector512<byte> pixels1,
        Vector512<byte> pixels2,
        Vector512<byte> pixels3,
        out Vector512<byte> destination0,
        out Vector512<byte> destination1,
        out Vector512<byte> destination2)
    {
        // The join mask packs the four pixels of each 128-bit lane into elements 0 to 2 of that lane, so each
        // register holds its 48 bytes in elements 0-2, 4-6, 8-10 and 12-14. Two-source permutes then copy those
        // elements, in order, to three full registers. Each permute index addresses the 32 elements of its two
        // sources: the first source is 0 to 15.
        Vector128<byte> mask = Join4To3Mask;
        Vector512<int> v0 = Vector512_.ShufflePerLane(pixels0, mask).AsInt32();
        Vector512<int> v1 = Vector512_.ShufflePerLane(pixels1, mask).AsInt32();
        Vector512<int> v2 = Vector512_.ShufflePerLane(pixels2, mask).AsInt32();
        Vector512<int> v3 = Vector512_.ShufflePerLane(pixels3, mask).AsInt32();

        Vector512<int> order0 = Vector512.Create(0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 16, 17, 18, 20);
        Vector512<int> order1 = Vector512.Create(5, 6, 8, 9, 10, 12, 13, 14, 16, 17, 18, 20, 21, 22, 24, 25);
        Vector512<int> order2 = Vector512.Create(10, 12, 13, 14, 16, 17, 18, 20, 21, 22, 24, 25, 26, 28, 29, 30);

        destination0 = Vector512_.PermuteVar16x32x2(v0, order0, v1).AsByte();
        destination1 = Vector512_.PermuteVar16x32x2(v1, order1, v2).AsByte();
        destination2 = Vector512_.PermuteVar16x32x2(v2, order2, v3).AsByte();
    }

    /// <summary>
    /// Writes the low three bytes of a packed pixel.
    /// </summary>
    /// <remarks>
    /// The value stays in a register: one 16-bit store writes bytes 0 and 1, and one byte store writes byte 2.
    /// A three-byte struct copy reads the value through its address, so the JIT puts the value on the stack.
    /// </remarks>
    /// <param name="destination">The first destination byte.</param>
    /// <param name="value">The packed pixel. Bytes 0 to 2 are written in little-endian order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreLowThreeBytes(ref byte destination, uint value)
    {
        Unsafe.As<byte, ushort>(ref destination) = (ushort)value;
        Unsafe.Add(ref destination, 2) = (byte)(value >> 16);
    }

    [Conditional("DEBUG")]
    internal static void VerifyShuffle4SpanInput<T>(ReadOnlySpan<T> source, ReadOnlySpan<T> destination)
        where T : struct
    {
        DebugGuard.IsTrue(
            source.Length == destination.Length,
            nameof(source),
            "Input spans must be of same length!");

        DebugGuard.IsTrue(
            source.Length % 4 == 0,
            nameof(source),
            "Input spans must be divisible by 4!");
    }

    [Conditional("DEBUG")]
    private static void VerifyShuffle3SpanInput<T>(ReadOnlySpan<T> source, ReadOnlySpan<T> destination)
        where T : struct
    {
        DebugGuard.IsTrue(
            source.Length <= destination.Length,
            nameof(source),
            "Source should fit into destination!");

        DebugGuard.IsTrue(
            source.Length % 3 == 0,
            nameof(source),
            "Input spans must be divisible by 3!");
    }

    [Conditional("DEBUG")]
    private static void VerifyPad3Shuffle4SpanInput(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        DebugGuard.IsTrue(
            source.Length % 3 == 0,
            nameof(source),
            "Input span must be divisible by 3!");

        DebugGuard.IsTrue(
            destination.Length % 4 == 0,
            nameof(destination),
            "Output span must be divisible by 4!");

        DebugGuard.IsTrue(
            source.Length == destination.Length * 3 / 4,
            nameof(source),
            "Input span must be 3/4 the length of the output span!");
    }

    [Conditional("DEBUG")]
    private static void VerifyShuffle4Slice3SpanInput(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        DebugGuard.IsTrue(
            source.Length % 4 == 0,
            nameof(source),
            "Input span must be divisible by 4!");

        DebugGuard.IsTrue(
            destination.Length % 3 == 0,
            nameof(destination),
            "Output span must be divisible by 3!");

        DebugGuard.IsTrue(
            destination.Length >= source.Length * 3 / 4,
            nameof(source),
            "Output span must be at least 3/4 the length of the input span!");
    }

    public static class Shuffle
    {
        public const byte MMShuffle0000 = 0b00000000;
        public const byte MMShuffle0001 = 0b00000001;
        public const byte MMShuffle0002 = 0b00000010;
        public const byte MMShuffle0003 = 0b00000011;
        public const byte MMShuffle0010 = 0b00000100;
        public const byte MMShuffle0011 = 0b00000101;
        public const byte MMShuffle0012 = 0b00000110;
        public const byte MMShuffle0013 = 0b00000111;
        public const byte MMShuffle0020 = 0b00001000;
        public const byte MMShuffle0021 = 0b00001001;
        public const byte MMShuffle0022 = 0b00001010;
        public const byte MMShuffle0023 = 0b00001011;
        public const byte MMShuffle0030 = 0b00001100;
        public const byte MMShuffle0031 = 0b00001101;
        public const byte MMShuffle0032 = 0b00001110;
        public const byte MMShuffle0033 = 0b00001111;
        public const byte MMShuffle0100 = 0b00010000;
        public const byte MMShuffle0101 = 0b00010001;
        public const byte MMShuffle0102 = 0b00010010;
        public const byte MMShuffle0103 = 0b00010011;
        public const byte MMShuffle0110 = 0b00010100;
        public const byte MMShuffle0111 = 0b00010101;
        public const byte MMShuffle0112 = 0b00010110;
        public const byte MMShuffle0113 = 0b00010111;
        public const byte MMShuffle0120 = 0b00011000;
        public const byte MMShuffle0121 = 0b00011001;
        public const byte MMShuffle0122 = 0b00011010;
        public const byte MMShuffle0123 = 0b00011011;
        public const byte MMShuffle0130 = 0b00011100;
        public const byte MMShuffle0131 = 0b00011101;
        public const byte MMShuffle0132 = 0b00011110;
        public const byte MMShuffle0133 = 0b00011111;
        public const byte MMShuffle0200 = 0b00100000;
        public const byte MMShuffle0201 = 0b00100001;
        public const byte MMShuffle0202 = 0b00100010;
        public const byte MMShuffle0203 = 0b00100011;
        public const byte MMShuffle0210 = 0b00100100;
        public const byte MMShuffle0211 = 0b00100101;
        public const byte MMShuffle0212 = 0b00100110;
        public const byte MMShuffle0213 = 0b00100111;
        public const byte MMShuffle0220 = 0b00101000;
        public const byte MMShuffle0221 = 0b00101001;
        public const byte MMShuffle0222 = 0b00101010;
        public const byte MMShuffle0223 = 0b00101011;
        public const byte MMShuffle0230 = 0b00101100;
        public const byte MMShuffle0231 = 0b00101101;
        public const byte MMShuffle0232 = 0b00101110;
        public const byte MMShuffle0233 = 0b00101111;
        public const byte MMShuffle0300 = 0b00110000;
        public const byte MMShuffle0301 = 0b00110001;
        public const byte MMShuffle0302 = 0b00110010;
        public const byte MMShuffle0303 = 0b00110011;
        public const byte MMShuffle0310 = 0b00110100;
        public const byte MMShuffle0311 = 0b00110101;
        public const byte MMShuffle0312 = 0b00110110;
        public const byte MMShuffle0313 = 0b00110111;
        public const byte MMShuffle0320 = 0b00111000;
        public const byte MMShuffle0321 = 0b00111001;
        public const byte MMShuffle0322 = 0b00111010;
        public const byte MMShuffle0323 = 0b00111011;
        public const byte MMShuffle0330 = 0b00111100;
        public const byte MMShuffle0331 = 0b00111101;
        public const byte MMShuffle0332 = 0b00111110;
        public const byte MMShuffle0333 = 0b00111111;
        public const byte MMShuffle1000 = 0b01000000;
        public const byte MMShuffle1001 = 0b01000001;
        public const byte MMShuffle1002 = 0b01000010;
        public const byte MMShuffle1003 = 0b01000011;
        public const byte MMShuffle1010 = 0b01000100;
        public const byte MMShuffle1011 = 0b01000101;
        public const byte MMShuffle1012 = 0b01000110;
        public const byte MMShuffle1013 = 0b01000111;
        public const byte MMShuffle1020 = 0b01001000;
        public const byte MMShuffle1021 = 0b01001001;
        public const byte MMShuffle1022 = 0b01001010;
        public const byte MMShuffle1023 = 0b01001011;
        public const byte MMShuffle1030 = 0b01001100;
        public const byte MMShuffle1031 = 0b01001101;
        public const byte MMShuffle1032 = 0b01001110;
        public const byte MMShuffle1033 = 0b01001111;
        public const byte MMShuffle1100 = 0b01010000;
        public const byte MMShuffle1101 = 0b01010001;
        public const byte MMShuffle1102 = 0b01010010;
        public const byte MMShuffle1103 = 0b01010011;
        public const byte MMShuffle1110 = 0b01010100;
        public const byte MMShuffle1111 = 0b01010101;
        public const byte MMShuffle1112 = 0b01010110;
        public const byte MMShuffle1113 = 0b01010111;
        public const byte MMShuffle1120 = 0b01011000;
        public const byte MMShuffle1121 = 0b01011001;
        public const byte MMShuffle1122 = 0b01011010;
        public const byte MMShuffle1123 = 0b01011011;
        public const byte MMShuffle1130 = 0b01011100;
        public const byte MMShuffle1131 = 0b01011101;
        public const byte MMShuffle1132 = 0b01011110;
        public const byte MMShuffle1133 = 0b01011111;
        public const byte MMShuffle1200 = 0b01100000;
        public const byte MMShuffle1201 = 0b01100001;
        public const byte MMShuffle1202 = 0b01100010;
        public const byte MMShuffle1203 = 0b01100011;
        public const byte MMShuffle1210 = 0b01100100;
        public const byte MMShuffle1211 = 0b01100101;
        public const byte MMShuffle1212 = 0b01100110;
        public const byte MMShuffle1213 = 0b01100111;
        public const byte MMShuffle1220 = 0b01101000;
        public const byte MMShuffle1221 = 0b01101001;
        public const byte MMShuffle1222 = 0b01101010;
        public const byte MMShuffle1223 = 0b01101011;
        public const byte MMShuffle1230 = 0b01101100;
        public const byte MMShuffle1231 = 0b01101101;
        public const byte MMShuffle1232 = 0b01101110;
        public const byte MMShuffle1233 = 0b01101111;
        public const byte MMShuffle1300 = 0b01110000;
        public const byte MMShuffle1301 = 0b01110001;
        public const byte MMShuffle1302 = 0b01110010;
        public const byte MMShuffle1303 = 0b01110011;
        public const byte MMShuffle1310 = 0b01110100;
        public const byte MMShuffle1311 = 0b01110101;
        public const byte MMShuffle1312 = 0b01110110;
        public const byte MMShuffle1313 = 0b01110111;
        public const byte MMShuffle1320 = 0b01111000;
        public const byte MMShuffle1321 = 0b01111001;
        public const byte MMShuffle1322 = 0b01111010;
        public const byte MMShuffle1323 = 0b01111011;
        public const byte MMShuffle1330 = 0b01111100;
        public const byte MMShuffle1331 = 0b01111101;
        public const byte MMShuffle1332 = 0b01111110;
        public const byte MMShuffle1333 = 0b01111111;
        public const byte MMShuffle2000 = 0b10000000;
        public const byte MMShuffle2001 = 0b10000001;
        public const byte MMShuffle2002 = 0b10000010;
        public const byte MMShuffle2003 = 0b10000011;
        public const byte MMShuffle2010 = 0b10000100;
        public const byte MMShuffle2011 = 0b10000101;
        public const byte MMShuffle2012 = 0b10000110;
        public const byte MMShuffle2013 = 0b10000111;
        public const byte MMShuffle2020 = 0b10001000;
        public const byte MMShuffle2021 = 0b10001001;
        public const byte MMShuffle2022 = 0b10001010;
        public const byte MMShuffle2023 = 0b10001011;
        public const byte MMShuffle2030 = 0b10001100;
        public const byte MMShuffle2031 = 0b10001101;
        public const byte MMShuffle2032 = 0b10001110;
        public const byte MMShuffle2033 = 0b10001111;
        public const byte MMShuffle2100 = 0b10010000;
        public const byte MMShuffle2101 = 0b10010001;
        public const byte MMShuffle2102 = 0b10010010;
        public const byte MMShuffle2103 = 0b10010011;
        public const byte MMShuffle2110 = 0b10010100;
        public const byte MMShuffle2111 = 0b10010101;
        public const byte MMShuffle2112 = 0b10010110;
        public const byte MMShuffle2113 = 0b10010111;
        public const byte MMShuffle2120 = 0b10011000;
        public const byte MMShuffle2121 = 0b10011001;
        public const byte MMShuffle2122 = 0b10011010;
        public const byte MMShuffle2123 = 0b10011011;
        public const byte MMShuffle2130 = 0b10011100;
        public const byte MMShuffle2131 = 0b10011101;
        public const byte MMShuffle2132 = 0b10011110;
        public const byte MMShuffle2133 = 0b10011111;
        public const byte MMShuffle2200 = 0b10100000;
        public const byte MMShuffle2201 = 0b10100001;
        public const byte MMShuffle2202 = 0b10100010;
        public const byte MMShuffle2203 = 0b10100011;
        public const byte MMShuffle2210 = 0b10100100;
        public const byte MMShuffle2211 = 0b10100101;
        public const byte MMShuffle2212 = 0b10100110;
        public const byte MMShuffle2213 = 0b10100111;
        public const byte MMShuffle2220 = 0b10101000;
        public const byte MMShuffle2221 = 0b10101001;
        public const byte MMShuffle2222 = 0b10101010;
        public const byte MMShuffle2223 = 0b10101011;
        public const byte MMShuffle2230 = 0b10101100;
        public const byte MMShuffle2231 = 0b10101101;
        public const byte MMShuffle2232 = 0b10101110;
        public const byte MMShuffle2233 = 0b10101111;
        public const byte MMShuffle2300 = 0b10110000;
        public const byte MMShuffle2301 = 0b10110001;
        public const byte MMShuffle2302 = 0b10110010;
        public const byte MMShuffle2303 = 0b10110011;
        public const byte MMShuffle2310 = 0b10110100;
        public const byte MMShuffle2311 = 0b10110101;
        public const byte MMShuffle2312 = 0b10110110;
        public const byte MMShuffle2313 = 0b10110111;
        public const byte MMShuffle2320 = 0b10111000;
        public const byte MMShuffle2321 = 0b10111001;
        public const byte MMShuffle2322 = 0b10111010;
        public const byte MMShuffle2323 = 0b10111011;
        public const byte MMShuffle2330 = 0b10111100;
        public const byte MMShuffle2331 = 0b10111101;
        public const byte MMShuffle2332 = 0b10111110;
        public const byte MMShuffle2333 = 0b10111111;
        public const byte MMShuffle3000 = 0b11000000;
        public const byte MMShuffle3001 = 0b11000001;
        public const byte MMShuffle3002 = 0b11000010;
        public const byte MMShuffle3003 = 0b11000011;
        public const byte MMShuffle3010 = 0b11000100;
        public const byte MMShuffle3011 = 0b11000101;
        public const byte MMShuffle3012 = 0b11000110;
        public const byte MMShuffle3013 = 0b11000111;
        public const byte MMShuffle3020 = 0b11001000;
        public const byte MMShuffle3021 = 0b11001001;
        public const byte MMShuffle3022 = 0b11001010;
        public const byte MMShuffle3023 = 0b11001011;
        public const byte MMShuffle3030 = 0b11001100;
        public const byte MMShuffle3031 = 0b11001101;
        public const byte MMShuffle3032 = 0b11001110;
        public const byte MMShuffle3033 = 0b11001111;
        public const byte MMShuffle3100 = 0b11010000;
        public const byte MMShuffle3101 = 0b11010001;
        public const byte MMShuffle3102 = 0b11010010;
        public const byte MMShuffle3103 = 0b11010011;
        public const byte MMShuffle3110 = 0b11010100;
        public const byte MMShuffle3111 = 0b11010101;
        public const byte MMShuffle3112 = 0b11010110;
        public const byte MMShuffle3113 = 0b11010111;
        public const byte MMShuffle3120 = 0b11011000;
        public const byte MMShuffle3121 = 0b11011001;
        public const byte MMShuffle3122 = 0b11011010;
        public const byte MMShuffle3123 = 0b11011011;
        public const byte MMShuffle3130 = 0b11011100;
        public const byte MMShuffle3131 = 0b11011101;
        public const byte MMShuffle3132 = 0b11011110;
        public const byte MMShuffle3133 = 0b11011111;
        public const byte MMShuffle3200 = 0b11100000;
        public const byte MMShuffle3201 = 0b11100001;
        public const byte MMShuffle3202 = 0b11100010;
        public const byte MMShuffle3203 = 0b11100011;
        public const byte MMShuffle3210 = 0b11100100;
        public const byte MMShuffle3211 = 0b11100101;
        public const byte MMShuffle3212 = 0b11100110;
        public const byte MMShuffle3213 = 0b11100111;
        public const byte MMShuffle3220 = 0b11101000;
        public const byte MMShuffle3221 = 0b11101001;
        public const byte MMShuffle3222 = 0b11101010;
        public const byte MMShuffle3223 = 0b11101011;
        public const byte MMShuffle3230 = 0b11101100;
        public const byte MMShuffle3231 = 0b11101101;
        public const byte MMShuffle3232 = 0b11101110;
        public const byte MMShuffle3233 = 0b11101111;
        public const byte MMShuffle3300 = 0b11110000;
        public const byte MMShuffle3301 = 0b11110001;
        public const byte MMShuffle3302 = 0b11110010;
        public const byte MMShuffle3303 = 0b11110011;
        public const byte MMShuffle3310 = 0b11110100;
        public const byte MMShuffle3311 = 0b11110101;
        public const byte MMShuffle3312 = 0b11110110;
        public const byte MMShuffle3313 = 0b11110111;
        public const byte MMShuffle3320 = 0b11111000;
        public const byte MMShuffle3321 = 0b11111001;
        public const byte MMShuffle3322 = 0b11111010;
        public const byte MMShuffle3323 = 0b11111011;
        public const byte MMShuffle3330 = 0b11111100;
        public const byte MMShuffle3331 = 0b11111101;
        public const byte MMShuffle3332 = 0b11111110;
        public const byte MMShuffle3333 = 0b11111111;

        [MethodImpl(InliningOptions.ShortMethod)]
        public static byte MMShuffle(byte p3, byte p2, byte p1, byte p0)
            => (byte)((p3 << 6) | (p2 << 4) | (p1 << 2) | p0);

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void InverseMMShuffle(
            byte control,
            out uint p3,
            out uint p2,
            out uint p1,
            out uint p0)
        {
            p3 = (uint)((control >> 6) & 0x3);
            p2 = (uint)((control >> 4) & 0x3);
            p1 = (uint)((control >> 2) & 0x3);
            p0 = (uint)((control >> 0) & 0x3);
        }
    }
}
