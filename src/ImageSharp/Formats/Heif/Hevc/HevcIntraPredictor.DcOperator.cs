// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Defines DC intra-prediction arithmetic.
/// </content>
internal static partial class HevcIntraPredictor
{
    /// <summary>
    /// Implements DC prediction and its optional luma boundary filter.
    /// </summary>
    private readonly struct DcOperator : IHevcIntraPredictionOperator
    {
        /// <inheritdoc/>
        public static void Predict(
            ReadOnlySpan<ushort> top,
            ReadOnlySpan<ushort> left,
            Span<ushort> destination,
            int destinationStride,
            int size,
            int mode,
            int bitDepth,
            bool filterPredictionEdges,
            Span<ushort> scratch)
        {
            uint sum = SumSamples(top.Slice(1, size)) + SumSamples(left.Slice(1, size));
            ushort dc = (ushort)((sum + (uint)size) >> (BitOperations.Log2((uint)size) + 1));
            for (int y = 0; y < size; y++)
            {
                destination.Slice(y * destinationStride, size).Fill(dc);
            }

            if (!filterPredictionEdges)
            {
                return;
            }

            destination[0] = (ushort)((top[1] + left[1] + (2 * dc) + 2) >> 2);
            for (int x = 1; x < size; x++)
            {
                destination[x] = (ushort)((top[x + 1] + (3 * dc) + 2) >> 2);
            }

            for (int y = 1; y < size; y++)
            {
                destination[y * destinationStride] = (ushort)((left[y + 1] + (3 * dc) + 2) >> 2);
            }
        }

        /// <summary>
        /// Sums reconstructed reference samples without overflowing their 16-bit storage.
        /// </summary>
        /// <param name="samples">The samples to sum.</param>
        /// <returns>The exact unsigned sum.</returns>
        private static uint SumSamples(ReadOnlySpan<ushort> samples)
        {
            ref ushort samplesBase = ref MemoryMarshal.GetReference(samples);
            uint sum = 0;
            int i = 0;

            // Widen before reduction because a complete 64-sample, 12-bit reference edge exceeds UInt16. The shared index
            // lets narrower vectors consume only the remainder from the widest available path.
            if (Vector512.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = samples.Length - Vector512<ushort>.Count;
                for (; i <= oneVectorFromEnd; i += Vector512<ushort>.Count)
                {
                    (Vector512<uint> low, Vector512<uint> high) = Vector512.Widen(Vector512.LoadUnsafe(ref samplesBase, (nuint)i));
                    sum += Vector512.Sum(low) + Vector512.Sum(high);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = samples.Length - Vector256<ushort>.Count;
                for (; i <= oneVectorFromEnd; i += Vector256<ushort>.Count)
                {
                    (Vector256<uint> low, Vector256<uint> high) = Vector256.Widen(Vector256.LoadUnsafe(ref samplesBase, (nuint)i));
                    sum += Vector256.Sum(low) + Vector256.Sum(high);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = samples.Length - Vector128<ushort>.Count;
                for (; i <= oneVectorFromEnd; i += Vector128<ushort>.Count)
                {
                    (Vector128<uint> low, Vector128<uint> high) = Vector128.Widen(Vector128.LoadUnsafe(ref samplesBase, (nuint)i));
                    sum += Vector128.Sum(low) + Vector128.Sum(high);
                }
            }

            for (; i < samples.Length; i++)
            {
                sum += Unsafe.Add(ref samplesBase, i);
            }

            return sum;
        }
    }
}
