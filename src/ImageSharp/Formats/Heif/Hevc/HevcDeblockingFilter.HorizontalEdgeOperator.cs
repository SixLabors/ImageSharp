// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

internal static partial class HevcDeblockingFilter
{
    /// <summary>
    /// Accesses four columns across a horizontal edge.
    /// </summary>
    private readonly struct HorizontalEdgeOperator : IEdgeOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadVector(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int count)
        {
            ref ushort source = ref picture.GetRowSpan(plane, y + distance)[x];
            if (count == 2)
            {
                // The packed load used by full-width segments would read two samples beyond a subsampled edge.
                return Vector128.Create((int)source, Unsafe.Add(ref source, 1), 0, 0);
            }

            Vector64<ushort> packed = Unsafe.As<ushort, Vector64<ushort>>(ref source);
            return Vector128.WidenLower(Vector128.Create(packed, Vector64<ushort>.Zero)).AsInt32();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreVector(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int distance,
            Vector128<int> value,
            int count)
        {
            ref ushort destination = ref picture.GetRowSpan(plane, y + distance)[x];
            if (count == 4)
            {
                Vector64<ushort> packed = Vector128.Narrow(value, Vector128<int>.Zero).AsUInt16().GetLower();
                Unsafe.As<ushort, Vector64<ushort>>(ref destination) = packed;
                return;
            }

            destination = (ushort)value.GetElement(0);
            Unsafe.Add(ref destination, 1) = (ushort)value.GetElement(1);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LoadScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index)
            => picture.GetRowSpan(plane, y + distance)[x + index];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index, int value)
            => picture.GetRowSpan(plane, y + distance)[x + index] = (ushort)value;
    }
}
