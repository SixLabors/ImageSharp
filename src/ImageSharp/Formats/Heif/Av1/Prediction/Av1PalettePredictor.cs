// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Reconstructs AV1 palette-predicted sample blocks from decoded color-index maps.
/// </summary>
internal static class Av1PalettePredictor
{
    /// <summary>
    /// The repeated byte offsets that select both bytes of eight 16-bit palette entries.
    /// </summary>
    private const ushort PaletteByteOffsetMultiplier = 0x0202;

    /// <summary>
    /// The high-byte increment that selects the second byte of each 16-bit palette entry.
    /// </summary>
    private const ushort PaletteHighByteOffset = 0x0100;

    /// <summary>
    /// Reconstructs an 8-bit palette-predicted block.
    /// </summary>
    /// <param name="paletteColors">The decoded palette colors in prediction-index order.</param>
    /// <param name="colorIndexMap">The color-index map beginning at the prediction block origin.</param>
    /// <param name="colorIndexMapStride">The distance, in indices, between map rows.</param>
    /// <param name="destination">The destination beginning at the prediction block origin.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    public static void Predict(
        ReadOnlySpan<ushort> paletteColors,
        ReadOnlySpan<byte> colorIndexMap,
        int colorIndexMapStride,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height)
    {
        ref byte mapBase = ref MemoryMarshal.GetReference(colorIndexMap);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);

        // An AV1 palette contains at most eight colors. Packing it once into the low 64 bits and repeating it in
        // every 128-bit lane turns reconstruction into the lane-local table lookup implemented by ShuffleNative.
        ulong packedPalette = 0;
        for (int index = 0; index < paletteColors.Length; index++)
        {
            packedPalette |= (ulong)(byte)paletteColors[index] << (index * 8);
        }

        Vector128<byte> palette128 = Vector128.Create(packedPalette, packedPalette).AsByte();

        if (Vector512.IsHardwareAccelerated && width >= Vector512<byte>.Count)
        {
            Vector256<byte> palette256 = Vector256.Create(palette128, palette128);
            Vector512<byte> palette512 = Vector512.Create(palette256, palette256);

            for (int row = 0; row < height; row++)
            {
                ref byte mapRow = ref Unsafe.Add(ref mapBase, row * colorIndexMapStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = 0; column < width; column += Vector512<byte>.Count)
                {
                    Vector512<byte> indices = Vector512.LoadUnsafe(ref mapRow, (nuint)column);
                    Vector512.ShuffleNative(palette512, indices).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            return;
        }

        if (Vector256.IsHardwareAccelerated && width >= Vector256<byte>.Count)
        {
            Vector256<byte> palette256 = Vector256.Create(palette128, palette128);
            for (int row = 0; row < height; row++)
            {
                ref byte mapRow = ref Unsafe.Add(ref mapBase, row * colorIndexMapStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = 0; column < width; column += Vector256<byte>.Count)
                {
                    Vector256<byte> indices = Vector256.LoadUnsafe(ref mapRow, (nuint)column);
                    Vector256.ShuffleNative(palette256, indices).StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (int row = 0; row < height; row++)
            {
                ref byte mapRow = ref Unsafe.Add(ref mapBase, row * colorIndexMapStride);
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                for (; column <= width - Vector128<byte>.Count; column += Vector128<byte>.Count)
                {
                    Vector128<byte> indices = Vector128.LoadUnsafe(ref mapRow, (nuint)column);
                    Vector128.ShuffleNative(palette128, indices).StoreUnsafe(ref destinationRow, (nuint)column);
                }

                if (column < width)
                {
                    // Transform widths are powers of two. After complete vector chunks, only a four- or eight-byte
                    // row tail remains, so an exact-width load and store keeps neighboring transforms untouched.
                    int remaining = width - column;
                    ulong packedIndices = remaining == 4
                        ? Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref mapRow, column))
                        : Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref mapRow, column));

                    Vector128<byte> result = Vector128.ShuffleNative(palette128, Vector128.CreateScalarUnsafe(packedIndices).AsByte());

                    if (remaining == 4)
                    {
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRow, column), result.AsUInt32().ToScalar());
                    }
                    else
                    {
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationRow, column), result.AsUInt64().ToScalar());
                    }

                    column = width;
                }
            }

            return;
        }

        for (int row = 0; row < height; row++)
        {
            ref byte mapRow = ref Unsafe.Add(ref mapBase, row * colorIndexMapStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            for (int column = 0; column < width; column++)
            {
                Unsafe.Add(ref destinationRow, column) = (byte)paletteColors[Unsafe.Add(ref mapRow, column)];
            }
        }
    }

    /// <summary>
    /// Reconstructs a high-bit-depth palette-predicted block.
    /// </summary>
    /// <param name="paletteColors">The decoded palette colors in prediction-index order.</param>
    /// <param name="colorIndexMap">The color-index map beginning at the prediction block origin.</param>
    /// <param name="colorIndexMapStride">The distance, in indices, between map rows.</param>
    /// <param name="destination">The destination beginning at the prediction block origin.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    public static void Predict(
        ReadOnlySpan<ushort> paletteColors,
        ReadOnlySpan<byte> colorIndexMap,
        int colorIndexMapStride,
        Span<short> destination,
        int destinationStride,
        int width,
        int height)
    {
        ref byte mapBase = ref MemoryMarshal.GetReference(colorIndexMap);
        ref short destinationBase = ref MemoryMarshal.GetReference(destination);
        InlineArray8<ushort> paletteStorage = default;

        paletteColors.CopyTo(paletteStorage);

        // Each index is expanded to the byte offsets 2n and 2n+1. Repeating the complete 16-byte palette in every
        // 128-bit lane then permits the same native byte-table shuffle on x86, Arm, and WebAssembly.
        Vector128<byte> palette128 = Vector128.LoadUnsafe(ref paletteStorage[0]).AsByte();

        if (Vector512.IsHardwareAccelerated && width >= Vector512<ushort>.Count)
        {
            Vector256<byte> palette256 = Vector256.Create(palette128, palette128);
            Vector512<byte> palette512 = Vector512.Create(palette256, palette256);
            Vector512<ushort> multiplier = Vector512.Create(PaletteByteOffsetMultiplier);
            Vector512<ushort> increment = Vector512.Create(PaletteHighByteOffset);

            for (int row = 0; row < height; row++)
            {
                ref byte mapRow = ref Unsafe.Add(ref mapBase, row * colorIndexMapStride);
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = 0; column < width; column += Vector512<ushort>.Count)
                {
                    (Vector256<ushort> lower, Vector256<ushort> upper) = Vector256.Widen(Vector256.LoadUnsafe(ref mapRow, (nuint)column));

                    Vector512<ushort> indices = Vector512.Create(lower, upper);
                    Vector512<byte> controls = ((indices * multiplier) + increment).AsByte();
                    Vector512.ShuffleNative(palette512, controls).AsInt16().StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            return;
        }

        if (Vector256.IsHardwareAccelerated && width >= Vector256<ushort>.Count)
        {
            Vector256<byte> palette256 = Vector256.Create(palette128, palette128);
            Vector256<ushort> multiplier = Vector256.Create(PaletteByteOffsetMultiplier);
            Vector256<ushort> increment = Vector256.Create(PaletteHighByteOffset);

            for (int row = 0; row < height; row++)
            {
                ref byte mapRow = ref Unsafe.Add(ref mapBase, row * colorIndexMapStride);
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = 0; column < width; column += Vector256<ushort>.Count)
                {
                    (Vector128<ushort> lower, Vector128<ushort> upper) = Vector128.Widen(Vector128.LoadUnsafe(ref mapRow, (nuint)column));

                    Vector256<ushort> indices = Vector256.Create(lower, upper);
                    Vector256<byte> controls = ((indices * multiplier) + increment).AsByte();
                    Vector256.ShuffleNative(palette256, controls).AsInt16().StoreUnsafe(ref destinationRow, (nuint)column);
                }
            }

            return;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<ushort> multiplier = Vector128.Create(PaletteByteOffsetMultiplier);
            Vector128<ushort> increment = Vector128.Create(PaletteHighByteOffset);

            for (int row = 0; row < height; row++)
            {
                ref byte mapRow = ref Unsafe.Add(ref mapBase, row * colorIndexMapStride);
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
                int column = 0;

                for (; column <= width - Vector128<ushort>.Count; column += Vector128<ushort>.Count)
                {
                    ulong packedIndices = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref mapRow, column));
                    Vector128<ushort> indices = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packedIndices).AsByte());
                    Vector128<byte> controls = ((indices * multiplier) + increment).AsByte();
                    Vector128.ShuffleNative(palette128, controls).AsInt16().StoreUnsafe(ref destinationRow, (nuint)column);
                }

                if (column < width)
                {
                    uint packedIndices = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref mapRow, column));
                    Vector128<ushort> indices = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packedIndices).AsByte());
                    Vector128<byte> controls = ((indices * multiplier) + increment).AsByte();
                    Vector128<short> result = Vector128.ShuffleNative(palette128, controls).AsInt16();
                    Unsafe.WriteUnaligned(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref destinationRow, column)), result.AsUInt64().ToScalar());
                    column += 4;
                }
            }

            return;
        }

        for (int row = 0; row < height; row++)
        {
            ref byte mapRow = ref Unsafe.Add(ref mapBase, row * colorIndexMapStride);
            ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            for (int column = 0; column < width; column++)
            {
                Unsafe.Add(ref destinationRow, column) = (short)paletteColors[Unsafe.Add(ref mapRow, column)];
            }
        }
    }
}
