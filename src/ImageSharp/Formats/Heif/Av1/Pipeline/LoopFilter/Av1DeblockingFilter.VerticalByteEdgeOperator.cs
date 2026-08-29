// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

internal static partial class Av1DeblockingFilter
{
    /// <summary>
    /// Accesses four rows across a vertical edge in eight-bit storage.
    /// </summary>
    private readonly struct VerticalByteEdgeOperator : IEdgeOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadVector(ref byte samples, int q0Offset, int stride, int distance)
            => Vector128.Create(
                (int)Unsafe.Add(ref samples, q0Offset + distance),
                Unsafe.Add(ref samples, q0Offset + stride + distance),
                Unsafe.Add(ref samples, q0Offset + (2 * stride) + distance),
                Unsafe.Add(ref samples, q0Offset + (3 * stride) + distance));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreVector(ref byte samples, int q0Offset, int stride, int distance, Vector128<int> value)
        {
            Unsafe.Add(ref samples, q0Offset + distance) = (byte)value.GetElement(0);
            Unsafe.Add(ref samples, q0Offset + stride + distance) = (byte)value.GetElement(1);
            Unsafe.Add(ref samples, q0Offset + (2 * stride) + distance) = (byte)value.GetElement(2);
            Unsafe.Add(ref samples, q0Offset + (3 * stride) + distance) = (byte)value.GetElement(3);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LoadScalar(ref byte samples, int q0Offset, int stride, int distance, int index)
            => Unsafe.Add(ref samples, q0Offset + (index * stride) + distance);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreScalar(ref byte samples, int q0Offset, int stride, int distance, int index, int value)
            => Unsafe.Add(ref samples, q0Offset + (index * stride) + distance) = (byte)value;
    }
}
