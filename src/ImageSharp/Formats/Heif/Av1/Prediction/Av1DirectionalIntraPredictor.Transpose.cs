// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Provides SIMD block transposition for zone 3 directional prediction. AV1 block dimensions are multiples of four,
/// allowing the traversal to use complete eight-by-eight tiles where possible and complete four-by-four tiles for the
/// remaining small blocks. Unpack stages exchange coordinate bits inside registers; exact-width loads and stores keep
/// every access within the logical block even when the destination has no writable row padding.
/// </content>
internal static partial class Av1DirectionalIntraPredictor
{
    /// <summary>
    /// Implements the directional traversal for one closed interpolation operator.
    /// </summary>
    private static partial class Predictor<TOperator>
        where TOperator : struct, IDirectionalPredictionOperator
    {
        /// <summary>
        /// Transposes a contiguous 8-bit prediction block into the reconstructed destination.
        /// </summary>
        /// <param name="source">The contiguous source block.</param>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="sourceWidth">The source row width.</param>
        /// <param name="sourceHeight">The number of source rows.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        private static void Transpose(ReadOnlySpan<byte> source, Span<byte> destination, int sourceWidth, int sourceHeight, int destinationStride)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                // Selecting one tile size for the complete block keeps both loop increments aligned with the AV1 block
                // dimensions. No partial SIMD tile reaches a neighboring prediction block.
                int tileSize = sourceWidth >= 8 && sourceHeight >= 8 ? 8 : 4;
                for (int y = 0; y < sourceHeight; y += tileSize)
                {
                    for (int x = 0; x < sourceWidth; x += tileSize)
                    {
                        if (tileSize == 8)
                        {
                            Transpose8x8(source, destination, sourceWidth, destinationStride, x, y);
                        }
                        else
                        {
                            Transpose4x4(source, destination, sourceWidth, destinationStride, x, y);
                        }
                    }
                }

                return;
            }

            for (int y = 0; y < sourceHeight; y++)
            {
                for (int x = 0; x < sourceWidth; x++)
                {
                    destination[(x * destinationStride) + y] = source[(y * sourceWidth) + x];
                }
            }
        }

        /// <summary>
        /// Transposes a contiguous high-bit-depth prediction block into the reconstructed destination.
        /// </summary>
        /// <param name="source">The contiguous source block.</param>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="sourceWidth">The source row width.</param>
        /// <param name="sourceHeight">The number of source rows.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        private static void Transpose(ReadOnlySpan<short> source, Span<short> destination, int sourceWidth, int sourceHeight, int destinationStride)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                // The same tiling invariant applies to two-byte samples; only the register unpack granularity differs.
                int tileSize = sourceWidth >= 8 && sourceHeight >= 8 ? 8 : 4;
                for (int y = 0; y < sourceHeight; y += tileSize)
                {
                    for (int x = 0; x < sourceWidth; x += tileSize)
                    {
                        if (tileSize == 8)
                        {
                            Transpose8x8(source, destination, sourceWidth, destinationStride, x, y);
                        }
                        else
                        {
                            Transpose4x4(source, destination, sourceWidth, destinationStride, x, y);
                        }
                    }
                }

                return;
            }

            for (int y = 0; y < sourceHeight; y++)
            {
                for (int x = 0; x < sourceWidth; x++)
                {
                    destination[(x * destinationStride) + y] = source[(y * sourceWidth) + x];
                }
            }
        }

        /// <summary>
        /// Transposes one eight-by-eight tile of 8-bit prediction samples.
        /// </summary>
        /// <param name="source">The contiguous source block.</param>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="x">The tile X coordinate.</param>
        /// <param name="y">The tile Y coordinate.</param>
        private static void Transpose8x8(ReadOnlySpan<byte> source, Span<byte> destination, int sourceStride, int destinationStride, int x, int y)
        {
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            Vector128<byte> row0 = LoadEightBytes(ref sourceBase, ((y + 0) * sourceStride) + x);
            Vector128<byte> row1 = LoadEightBytes(ref sourceBase, ((y + 1) * sourceStride) + x);
            Vector128<byte> row2 = LoadEightBytes(ref sourceBase, ((y + 2) * sourceStride) + x);
            Vector128<byte> row3 = LoadEightBytes(ref sourceBase, ((y + 3) * sourceStride) + x);
            Vector128<byte> row4 = LoadEightBytes(ref sourceBase, ((y + 4) * sourceStride) + x);
            Vector128<byte> row5 = LoadEightBytes(ref sourceBase, ((y + 5) * sourceStride) + x);
            Vector128<byte> row6 = LoadEightBytes(ref sourceBase, ((y + 6) * sourceStride) + x);
            Vector128<byte> row7 = LoadEightBytes(ref sourceBase, ((y + 7) * sourceStride) + x);

            // Three unpack stages exchange one, two, then four byte coordinates. Each final vector contains two
            // complete source columns, which are written as two contiguous eight-byte destination rows.
            Vector128<byte> pair0 = Vector128_.UnpackLow(row0, row1);
            Vector128<byte> pair2 = Vector128_.UnpackLow(row2, row3);
            Vector128<byte> pair4 = Vector128_.UnpackLow(row4, row5);
            Vector128<byte> pair6 = Vector128_.UnpackLow(row6, row7);
            Vector128<short> quad0 = Vector128_.UnpackLow(pair0.AsInt16(), pair2.AsInt16());
            Vector128<short> quad1 = Vector128_.UnpackHigh(pair0.AsInt16(), pair2.AsInt16());
            Vector128<short> quad4 = Vector128_.UnpackLow(pair4.AsInt16(), pair6.AsInt16());
            Vector128<short> quad5 = Vector128_.UnpackHigh(pair4.AsInt16(), pair6.AsInt16());
            Vector128<int> columns01 = Vector128_.UnpackLow(quad0.AsInt32(), quad4.AsInt32());
            Vector128<int> columns23 = Vector128_.UnpackHigh(quad0.AsInt32(), quad4.AsInt32());
            Vector128<int> columns45 = Vector128_.UnpackLow(quad1.AsInt32(), quad5.AsInt32());
            Vector128<int> columns67 = Vector128_.UnpackHigh(quad1.AsInt32(), quad5.AsInt32());

            StoreEightBytes(columns01.AsUInt64().ToScalar(), ref destinationBase, ((x + 0) * destinationStride) + y);
            StoreEightBytes(columns01.AsUInt64().GetElement(1), ref destinationBase, ((x + 1) * destinationStride) + y);
            StoreEightBytes(columns23.AsUInt64().ToScalar(), ref destinationBase, ((x + 2) * destinationStride) + y);
            StoreEightBytes(columns23.AsUInt64().GetElement(1), ref destinationBase, ((x + 3) * destinationStride) + y);
            StoreEightBytes(columns45.AsUInt64().ToScalar(), ref destinationBase, ((x + 4) * destinationStride) + y);
            StoreEightBytes(columns45.AsUInt64().GetElement(1), ref destinationBase, ((x + 5) * destinationStride) + y);
            StoreEightBytes(columns67.AsUInt64().ToScalar(), ref destinationBase, ((x + 6) * destinationStride) + y);
            StoreEightBytes(columns67.AsUInt64().GetElement(1), ref destinationBase, ((x + 7) * destinationStride) + y);
        }

        /// <summary>
        /// Transposes one four-by-four tile of 8-bit prediction samples.
        /// </summary>
        /// <param name="source">The contiguous source block.</param>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="x">The tile X coordinate.</param>
        /// <param name="y">The tile Y coordinate.</param>
        private static void Transpose4x4(ReadOnlySpan<byte> source, Span<byte> destination, int sourceStride, int destinationStride, int x, int y)
        {
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            Vector128<byte> row0 = LoadFourBytes(ref sourceBase, ((y + 0) * sourceStride) + x);
            Vector128<byte> row1 = LoadFourBytes(ref sourceBase, ((y + 1) * sourceStride) + x);
            Vector128<byte> row2 = LoadFourBytes(ref sourceBase, ((y + 2) * sourceStride) + x);
            Vector128<byte> row3 = LoadFourBytes(ref sourceBase, ((y + 3) * sourceStride) + x);
            Vector128<byte> pair0 = Vector128_.UnpackLow(row0, row1);
            Vector128<byte> pair1 = Vector128_.UnpackLow(row2, row3);
            Vector128<short> columns = Vector128_.UnpackLow(pair0.AsInt16(), pair1.AsInt16());

            Vector128<uint> packedColumns = columns.AsUInt32();
            StoreFourBytes(packedColumns.GetElement(0), ref destinationBase, ((x + 0) * destinationStride) + y);
            StoreFourBytes(packedColumns.GetElement(1), ref destinationBase, ((x + 1) * destinationStride) + y);
            StoreFourBytes(packedColumns.GetElement(2), ref destinationBase, ((x + 2) * destinationStride) + y);
            StoreFourBytes(packedColumns.GetElement(3), ref destinationBase, ((x + 3) * destinationStride) + y);
        }

        /// <summary>
        /// Transposes one eight-by-eight tile of high-bit-depth prediction samples.
        /// </summary>
        /// <param name="source">The contiguous source block.</param>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="x">The tile X coordinate.</param>
        /// <param name="y">The tile Y coordinate.</param>
        private static void Transpose8x8(ReadOnlySpan<short> source, Span<short> destination, int sourceStride, int destinationStride, int x, int y)
        {
            ref short sourceBase = ref MemoryMarshal.GetReference(source);
            ref short destinationBase = ref MemoryMarshal.GetReference(destination);
            Vector128<short> row0 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 0) * sourceStride) + x));
            Vector128<short> row1 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 1) * sourceStride) + x));
            Vector128<short> row2 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 2) * sourceStride) + x));
            Vector128<short> row3 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 3) * sourceStride) + x));
            Vector128<short> row4 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 4) * sourceStride) + x));
            Vector128<short> row5 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 5) * sourceStride) + x));
            Vector128<short> row6 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 6) * sourceStride) + x));
            Vector128<short> row7 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 7) * sourceStride) + x));
            Vector128<short> pair0 = Vector128_.UnpackLow(row0, row1);
            Vector128<short> pair1 = Vector128_.UnpackHigh(row0, row1);
            Vector128<short> pair2 = Vector128_.UnpackLow(row2, row3);
            Vector128<short> pair3 = Vector128_.UnpackHigh(row2, row3);
            Vector128<short> pair4 = Vector128_.UnpackLow(row4, row5);
            Vector128<short> pair5 = Vector128_.UnpackHigh(row4, row5);
            Vector128<short> pair6 = Vector128_.UnpackLow(row6, row7);
            Vector128<short> pair7 = Vector128_.UnpackHigh(row6, row7);
            Vector128<int> quad0 = Vector128_.UnpackLow(pair0.AsInt32(), pair2.AsInt32());
            Vector128<int> quad1 = Vector128_.UnpackHigh(pair0.AsInt32(), pair2.AsInt32());
            Vector128<int> quad2 = Vector128_.UnpackLow(pair1.AsInt32(), pair3.AsInt32());
            Vector128<int> quad3 = Vector128_.UnpackHigh(pair1.AsInt32(), pair3.AsInt32());
            Vector128<int> quad4 = Vector128_.UnpackLow(pair4.AsInt32(), pair6.AsInt32());
            Vector128<int> quad5 = Vector128_.UnpackHigh(pair4.AsInt32(), pair6.AsInt32());
            Vector128<int> quad6 = Vector128_.UnpackLow(pair5.AsInt32(), pair7.AsInt32());
            Vector128<int> quad7 = Vector128_.UnpackHigh(pair5.AsInt32(), pair7.AsInt32());
            Vector128<short> column0 = Vector128_.UnpackLow(quad0.AsInt64(), quad4.AsInt64()).AsInt16();
            Vector128<short> column1 = Vector128_.UnpackHigh(quad0.AsInt64(), quad4.AsInt64()).AsInt16();
            Vector128<short> column2 = Vector128_.UnpackLow(quad1.AsInt64(), quad5.AsInt64()).AsInt16();
            Vector128<short> column3 = Vector128_.UnpackHigh(quad1.AsInt64(), quad5.AsInt64()).AsInt16();
            Vector128<short> column4 = Vector128_.UnpackLow(quad2.AsInt64(), quad6.AsInt64()).AsInt16();
            Vector128<short> column5 = Vector128_.UnpackHigh(quad2.AsInt64(), quad6.AsInt64()).AsInt16();
            Vector128<short> column6 = Vector128_.UnpackLow(quad3.AsInt64(), quad7.AsInt64()).AsInt16();
            Vector128<short> column7 = Vector128_.UnpackHigh(quad3.AsInt64(), quad7.AsInt64()).AsInt16();
            column0.StoreUnsafe(ref destinationBase, (nuint)(((x + 0) * destinationStride) + y));
            column1.StoreUnsafe(ref destinationBase, (nuint)(((x + 1) * destinationStride) + y));
            column2.StoreUnsafe(ref destinationBase, (nuint)(((x + 2) * destinationStride) + y));
            column3.StoreUnsafe(ref destinationBase, (nuint)(((x + 3) * destinationStride) + y));
            column4.StoreUnsafe(ref destinationBase, (nuint)(((x + 4) * destinationStride) + y));
            column5.StoreUnsafe(ref destinationBase, (nuint)(((x + 5) * destinationStride) + y));
            column6.StoreUnsafe(ref destinationBase, (nuint)(((x + 6) * destinationStride) + y));
            column7.StoreUnsafe(ref destinationBase, (nuint)(((x + 7) * destinationStride) + y));
        }

        /// <summary>
        /// Transposes one four-by-four tile of high-bit-depth prediction samples.
        /// </summary>
        /// <param name="source">The contiguous source block.</param>
        /// <param name="destination">The destination block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="x">The tile X coordinate.</param>
        /// <param name="y">The tile Y coordinate.</param>
        private static void Transpose4x4(ReadOnlySpan<short> source, Span<short> destination, int sourceStride, int destinationStride, int x, int y)
        {
            ref short sourceBase = ref MemoryMarshal.GetReference(source);
            ref short destinationBase = ref MemoryMarshal.GetReference(destination);
            Vector128<short> row0 = LoadFourShorts(ref sourceBase, ((y + 0) * sourceStride) + x);
            Vector128<short> row1 = LoadFourShorts(ref sourceBase, ((y + 1) * sourceStride) + x);
            Vector128<short> row2 = LoadFourShorts(ref sourceBase, ((y + 2) * sourceStride) + x);
            Vector128<short> row3 = LoadFourShorts(ref sourceBase, ((y + 3) * sourceStride) + x);
            Vector128<short> pair0 = Vector128_.UnpackLow(row0, row1);
            Vector128<short> pair1 = Vector128_.UnpackLow(row2, row3);
            Vector128<int> columns01 = Vector128_.UnpackLow(pair0.AsInt32(), pair1.AsInt32());
            Vector128<int> columns23 = Vector128_.UnpackHigh(pair0.AsInt32(), pair1.AsInt32());

            StoreFourShorts(columns01.AsUInt64().ToScalar(), ref destinationBase, ((x + 0) * destinationStride) + y);
            StoreFourShorts(columns01.AsUInt64().GetElement(1), ref destinationBase, ((x + 1) * destinationStride) + y);
            StoreFourShorts(columns23.AsUInt64().ToScalar(), ref destinationBase, ((x + 2) * destinationStride) + y);
            StoreFourShorts(columns23.AsUInt64().GetElement(1), ref destinationBase, ((x + 3) * destinationStride) + y);
        }

        /// <summary>
        /// Loads eight bytes into the lower half of a vector without reading past a tile row.
        /// </summary>
        /// <param name="source">The source buffer origin.</param>
        /// <param name="offset">The source offset.</param>
        /// <returns>The loaded bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> LoadEightBytes(ref byte source, int offset)
            => Vector128.Create(Unsafe.As<byte, ulong>(ref Unsafe.Add(ref source, offset)), 0UL).AsByte();

        /// <summary>
        /// Loads four bytes into the low vector lanes without reading past a tile row.
        /// </summary>
        /// <param name="source">The source buffer origin.</param>
        /// <param name="offset">The source offset.</param>
        /// <returns>The loaded bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> LoadFourBytes(ref byte source, int offset)
            => Vector128.Create(Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, offset)), 0U, 0U, 0U).AsByte();

        /// <summary>
        /// Loads four high-bit-depth samples into the lower half of a vector without reading past a tile row.
        /// </summary>
        /// <param name="source">The source buffer origin.</param>
        /// <param name="offset">The source offset.</param>
        /// <returns>The loaded samples.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<short> LoadFourShorts(ref short source, int offset)
            => Vector128.Create(Unsafe.As<short, ulong>(ref Unsafe.Add(ref source, offset)), 0UL).AsInt16();

        /// <summary>
        /// Stores the lower eight bytes of a vector.
        /// </summary>
        /// <param name="source">The packed bytes.</param>
        /// <param name="destination">The destination buffer origin.</param>
        /// <param name="offset">The destination offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreEightBytes(ulong source, ref byte destination, int offset)
            => Unsafe.As<byte, ulong>(ref Unsafe.Add(ref destination, offset)) = source;

        /// <summary>
        /// Stores four bytes from the low vector lanes.
        /// </summary>
        /// <param name="source">The packed bytes.</param>
        /// <param name="destination">The destination buffer origin.</param>
        /// <param name="offset">The destination offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreFourBytes(uint source, ref byte destination, int offset)
            => Unsafe.As<byte, uint>(ref Unsafe.Add(ref destination, offset)) = source;

        /// <summary>
        /// Stores four high-bit-depth samples from the low vector lanes.
        /// </summary>
        /// <param name="source">The packed samples.</param>
        /// <param name="destination">The destination buffer origin.</param>
        /// <param name="offset">The destination offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreFourShorts(ulong source, ref short destination, int offset)
            => Unsafe.As<short, ulong>(ref Unsafe.Add(ref destination, offset)) = source;
    }
}
