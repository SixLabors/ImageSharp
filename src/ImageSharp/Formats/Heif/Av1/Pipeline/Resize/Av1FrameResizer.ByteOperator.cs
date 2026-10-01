// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Resize;

/// <content>
/// Defines the resize filter arithmetic of eight-bit samples.
/// </content>
internal static partial class Av1FrameResizer
{
    /// <summary>
    /// Applies the resize filter to eight-bit samples.
    /// </summary>
    internal readonly struct ByteOperator : IAv1ResizeSampleOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MultiplyAdd(int sum, ref byte source, int coefficient)
            => sum + (source * coefficient);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> MultiplyAdd(Vector128<int> sum, ref byte source, Vector128<int> coefficient)
        {
            // Four bytes widen through 16-bit lanes to four 32-bit lanes.
            Vector128<byte> bytes = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<uint>(ref source)).AsByte();
            Vector128<int> samples = Vector128.WidenLower(Vector128.WidenLower(bytes)).AsInt32();
            return sum + (samples * coefficient);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> MultiplyAdd(Vector256<int> sum, ref byte source, Vector256<int> coefficient)
        {
            // Eight bytes widen to eight 16-bit lanes, whose halves widen to the two halves of the 32-bit lanes.
            Vector128<byte> bytes = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref source)).AsByte();
            Vector128<ushort> words = Vector128.WidenLower(bytes);
            Vector256<int> samples = Vector256.Create(Vector128.WidenLower(words), Vector128.WidenUpper(words)).AsInt32();
            return sum + (samples * coefficient);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> MultiplyAdd(Vector512<int> sum, ref byte source, Vector512<int> coefficient)
        {
            // Sixteen bytes widen to sixteen 16-bit lanes, and each half widens to a quarter of the 32-bit lanes.
            Vector128<byte> bytes = Vector128.LoadUnsafe(ref source);
            Vector128<ushort> low = Vector128.WidenLower(bytes);
            Vector128<ushort> high = Vector128.WidenUpper(bytes);
            Vector512<int> samples = Vector512.Create(
                Vector256.Create(Vector128.WidenLower(low), Vector128.WidenUpper(low)),
                Vector256.Create(Vector128.WidenLower(high), Vector128.WidenUpper(high))).AsInt32();

            return sum + (samples * coefficient);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref byte destination, int sum, int maximum)
            => destination = (byte)Math.Clamp(sum >> FilterBits, 0, maximum);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref byte destination, Vector128<int> sum, Vector128<int> maximum)
        {
            // The clamped lanes fit in a byte, so the two narrowing steps keep every value.
            Vector128<int> clamped = Vector128.Min(Vector128.Max(Vector128.ShiftRightArithmetic(sum, FilterBits), Vector128<int>.Zero), maximum);
            Vector128<ushort> words = Vector128.Narrow(clamped.AsUInt32(), clamped.AsUInt32());
            Vector128<byte> bytes = Vector128.Narrow(words, words);
            Unsafe.WriteUnaligned(ref destination, bytes.AsUInt32().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref byte destination, Vector256<int> sum, Vector256<int> maximum)
        {
            // The clamped lanes fit in a byte, so the two narrowing steps keep every value.
            Vector256<int> clamped = Vector256.Min(Vector256.Max(Vector256.ShiftRightArithmetic(sum, FilterBits), Vector256<int>.Zero), maximum);
            Vector128<ushort> words = Vector128.Narrow(clamped.GetLower().AsUInt32(), clamped.GetUpper().AsUInt32());
            Vector128<byte> bytes = Vector128.Narrow(words, words);
            Unsafe.WriteUnaligned(ref destination, bytes.AsUInt64().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref byte destination, Vector512<int> sum, Vector512<int> maximum)
        {
            // The clamped lanes fit in a byte, so the two narrowing steps keep every value.
            Vector512<int> clamped = Vector512.Min(Vector512.Max(Vector512.ShiftRightArithmetic(sum, FilterBits), Vector512<int>.Zero), maximum);
            Vector256<int> low = clamped.GetLower();
            Vector256<int> high = clamped.GetUpper();
            Vector128<ushort> lowWords = Vector128.Narrow(low.GetLower().AsUInt32(), low.GetUpper().AsUInt32());
            Vector128<ushort> highWords = Vector128.Narrow(high.GetLower().AsUInt32(), high.GetUpper().AsUInt32());
            Vector128.Narrow(lowWords, highWords).StoreUnsafe(ref destination);
        }
    }
}
