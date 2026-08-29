// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

internal static partial class Av1DeblockingFilter
{
    /// <summary>
    /// Accesses four columns across a horizontal edge in 16-bit storage.
    /// </summary>
    private readonly struct HorizontalUInt16EdgeOperator : IEdgeOperator<ushort>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadVector(ref ushort samples, int q0Offset, int stride, int distance)
        {
            ref ushort source = ref Unsafe.Add(ref samples, q0Offset + (distance * stride));
            ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref source));
            return Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsUInt16()).AsInt32();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreVector(ref ushort samples, int q0Offset, int stride, int distance, Vector128<int> value)
        {
            Vector64<ushort> narrowed = Vector128.Narrow(value, Vector128<int>.Zero).AsUInt16().GetLower();
            ref byte destination = ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref samples, q0Offset + (distance * stride)));
            Unsafe.WriteUnaligned(ref destination, narrowed.AsUInt64().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LoadScalar(ref ushort samples, int q0Offset, int stride, int distance, int index)
            => Unsafe.Add(ref samples, q0Offset + (distance * stride) + index);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreScalar(ref ushort samples, int q0Offset, int stride, int distance, int index, int value)
            => Unsafe.Add(ref samples, q0Offset + (distance * stride) + index) = (ushort)value;
    }
}
