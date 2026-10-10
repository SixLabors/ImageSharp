// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Resize;

/// <content>
/// Defines the resize filter arithmetic of samples of more than eight bits.
/// </content>
internal static partial class Av1FrameResizer
{
    /// <summary>
    /// Applies the resize filter to samples of more than eight bits.
    /// </summary>
    internal readonly struct UInt16Operator : IAv1ResizeSampleOperator<ushort>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MultiplyAdd(int sum, ref ushort source, int coefficient)
            => sum + (source * coefficient);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> MultiplyAdd(Vector128<int> sum, ref ushort source, Vector128<int> coefficient)
        {
            // The read is exactly four 16-bit samples, so it stays inside the row. They widen to four 32-bit lanes.
            Vector128<ushort> words = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref source))).AsUInt16();
            return sum + (Vector128.WidenLower(words).AsInt32() * coefficient);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> MultiplyAdd(Vector256<int> sum, ref ushort source, Vector256<int> coefficient)
        {
            // Eight 16-bit samples widen to the two halves of the 32-bit lanes.
            Vector128<ushort> words = Vector128.LoadUnsafe(ref source);
            Vector256<int> samples = Vector256.Create(Vector128.WidenLower(words), Vector128.WidenUpper(words)).AsInt32();
            return sum + (samples * coefficient);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> MultiplyAdd(Vector512<int> sum, ref ushort source, Vector512<int> coefficient)
        {
            // Sixteen 16-bit samples widen to the two halves of the 32-bit lanes.
            Vector256<ushort> words = Vector256.LoadUnsafe(ref source);
            Vector512<int> samples = Vector512.Create(Vector256.WidenLower(words), Vector256.WidenUpper(words)).AsInt32();
            return sum + (samples * coefficient);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref ushort destination, int sum, int maximum)
            => destination = (ushort)Math.Clamp(sum >> FilterBits, 0, maximum);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref ushort destination, Vector128<int> sum, Vector128<int> maximum)
        {
            // The arithmetic shift equals the scalar `>>`. The clamped lanes fit in 16 bits, so the truncating narrow keeps every
            // value. The write is exactly four 16-bit samples.
            Vector128<int> clamped = Vector128.Min(Vector128.Max(Vector128.ShiftRightArithmetic(sum, FilterBits), Vector128<int>.Zero), maximum);
            Vector128<ushort> words = Vector128.Narrow(clamped.AsUInt32(), clamped.AsUInt32());
            Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref destination), words.AsUInt64().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref ushort destination, Vector256<int> sum, Vector256<int> maximum)
        {
            // The clamped lanes fit in 16 bits, so narrowing keeps every value.
            Vector256<int> clamped = Vector256.Min(Vector256.Max(Vector256.ShiftRightArithmetic(sum, FilterBits), Vector256<int>.Zero), maximum);
            Vector128.Narrow(clamped.GetLower().AsUInt32(), clamped.GetUpper().AsUInt32()).StoreUnsafe(ref destination);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(ref ushort destination, Vector512<int> sum, Vector512<int> maximum)
        {
            // The clamped lanes fit in 16 bits, so narrowing keeps every value.
            Vector512<int> clamped = Vector512.Min(Vector512.Max(Vector512.ShiftRightArithmetic(sum, FilterBits), Vector512<int>.Zero), maximum);
            Vector256.Narrow(clamped.GetLower().AsUInt32(), clamped.GetUpper().AsUInt32()).StoreUnsafe(ref destination);
        }
    }
}
