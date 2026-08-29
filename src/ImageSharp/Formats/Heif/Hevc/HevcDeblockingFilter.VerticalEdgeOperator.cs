// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

internal static partial class HevcDeblockingFilter
{
    /// <summary>
    /// Accesses four rows across a vertical edge.
    /// </summary>
    private readonly struct VerticalEdgeOperator : IEdgeOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadVector(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int count)
        {
            if (count == 4)
            {
                return Vector128.Create(
                    (int)picture.GetRowSpan(plane, y)[x + distance],
                    picture.GetRowSpan(plane, y + 1)[x + distance],
                    picture.GetRowSpan(plane, y + 2)[x + distance],
                    picture.GetRowSpan(plane, y + 3)[x + distance]);
            }

            // Subsampled chroma edges contain two samples. Zeroing the unused lanes keeps the vector path within
            // the plane while allowing the shared kernel to operate on both valid samples in one instruction stream.
            return Vector128.Create(
                (int)picture.GetRowSpan(plane, y)[x + distance],
                picture.GetRowSpan(plane, y + 1)[x + distance],
                0,
                0);
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
            for (int index = 0; index < count; index++)
            {
                picture.GetRowSpan(plane, y + index)[x + distance] = (ushort)value.GetElement(index);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LoadScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index)
            => picture.GetRowSpan(plane, y + index)[x + distance];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index, int value)
            => picture.GetRowSpan(plane, y + index)[x + distance] = (ushort)value;
    }
}
