// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

internal static partial class Av1DeblockingFilter
{
    /// <summary>
    /// Accesses four columns across a horizontal edge in eight-bit storage.
    /// </summary>
    public readonly struct HorizontalByteEdgeOperator : IEdgeOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadVector(ref byte samples, int q0Offset, int stride, int distance)
        {
            ref byte source = ref Unsafe.Add(ref samples, q0Offset + (distance * stride));
            uint packed = Unsafe.ReadUnaligned<uint>(ref source);
            Vector128<ushort> widened = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
            return Vector128.WidenLower(widened).AsInt32();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreVector(ref byte samples, int q0Offset, int stride, int distance, Vector128<int> value)
        {
            Vector128<ushort> narrowed16 = Vector128.Narrow(value.AsUInt32(), Vector128<uint>.Zero);
            Vector128<byte> narrowed8 = Vector128.Narrow(narrowed16, Vector128<ushort>.Zero);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref samples, q0Offset + (distance * stride)), narrowed8.AsUInt32().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LoadScalar(ref byte samples, int q0Offset, int stride, int distance, int index)
            => Unsafe.Add(ref samples, q0Offset + (distance * stride) + index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreScalar(ref byte samples, int q0Offset, int stride, int distance, int index, int value)
            => Unsafe.Add(ref samples, q0Offset + (distance * stride) + index) = (byte)value;
    }
}
