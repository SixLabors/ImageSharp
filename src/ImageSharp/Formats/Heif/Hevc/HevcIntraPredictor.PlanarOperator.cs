// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Defines planar intra-prediction arithmetic.
/// </content>
internal static partial class HevcIntraPredictor
{
    /// <summary>
    /// Implements planar interpolation between the top, left, bottom-left, and top-right references.
    /// </summary>
    private readonly struct PlanarOperator : IHevcIntraPredictionOperator
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
            ref ushort topBase = ref MemoryMarshal.GetReference(top);
            ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);

            // Index zero is the shared corner, so the planar endpoint at coordinate N is stored at N + 1.
            uint bottomLeft = left[size + 1];
            uint topRight = top[size + 1];
            int shift = BitOperations.Log2((uint)size) + 1;
            uint rounding = (uint)size;

            for (int y = 0; y < size; y++)
            {
                uint leftSample = left[y + 1];
                uint topWeight = (uint)(size - y - 1);
                uint bottomWeight = (uint)(y + 1);
                ref ushort rowBase = ref Unsafe.Add(ref destinationBase, y * destinationStride);
                int x = 0;

                // The two widened halves carry consecutive X coordinates. Each lane evaluates the normative
                // horizontal and vertical ramps, then narrows after the common rounded power-of-two division.
                if (Vector512.IsHardwareAccelerated)
                {
                    Vector512<uint> indices = CreateIndicesVector512();
                    int oneVectorFromEnd = size - Vector512<ushort>.Count;
                    for (; x <= oneVectorFromEnd; x += Vector512<ushort>.Count)
                    {
                        Vector512<ushort> topSamples = Vector512.LoadUnsafe(ref topBase, (nuint)(x + 1));
                        (Vector512<uint> topLow, Vector512<uint> topHigh) = Vector512.Widen(topSamples);
                        Vector512<uint> lowIndices = indices + Vector512.Create((uint)x);
                        Vector512<uint> highIndices = lowIndices + Vector512.Create((uint)Vector512<uint>.Count);
                        Vector512<uint> low = CalculatePlanarVector(
                            topLow,
                            lowIndices,
                            leftSample,
                            topRight,
                            bottomLeft,
                            topWeight,
                            bottomWeight,
                            (uint)size,
                            rounding,
                            shift);

                        Vector512<uint> high = CalculatePlanarVector(
                            topHigh,
                            highIndices,
                            leftSample,
                            topRight,
                            bottomLeft,
                            topWeight,
                            bottomWeight,
                            (uint)size,
                            rounding,
                            shift);

                        Vector512.Narrow(low, high).StoreUnsafe(ref Unsafe.Add(ref rowBase, x));
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    Vector256<uint> indices = CreateIndicesVector256();
                    int oneVectorFromEnd = size - Vector256<ushort>.Count;
                    for (; x <= oneVectorFromEnd; x += Vector256<ushort>.Count)
                    {
                        Vector256<ushort> topSamples = Vector256.LoadUnsafe(ref topBase, (nuint)(x + 1));
                        (Vector256<uint> topLow, Vector256<uint> topHigh) = Vector256.Widen(topSamples);
                        Vector256<uint> lowIndices = indices + Vector256.Create((uint)x);
                        Vector256<uint> highIndices = lowIndices + Vector256.Create((uint)Vector256<uint>.Count);
                        Vector256<uint> low = CalculatePlanarVector(
                            topLow,
                            lowIndices,
                            leftSample,
                            topRight,
                            bottomLeft,
                            topWeight,
                            bottomWeight,
                            (uint)size,
                            rounding,
                            shift);

                        Vector256<uint> high = CalculatePlanarVector(
                            topHigh,
                            highIndices,
                            leftSample,
                            topRight,
                            bottomLeft,
                            topWeight,
                            bottomWeight,
                            (uint)size,
                            rounding,
                            shift);

                        Vector256.Narrow(low, high).StoreUnsafe(ref Unsafe.Add(ref rowBase, x));
                    }
                }

                if (Vector128.IsHardwareAccelerated)
                {
                    Vector128<uint> indices = CreateIndicesVector128();
                    int oneVectorFromEnd = size - Vector128<ushort>.Count;
                    for (; x <= oneVectorFromEnd; x += Vector128<ushort>.Count)
                    {
                        Vector128<ushort> topSamples = Vector128.LoadUnsafe(ref topBase, (nuint)(x + 1));
                        (Vector128<uint> topLow, Vector128<uint> topHigh) = Vector128.Widen(topSamples);
                        Vector128<uint> lowIndices = indices + Vector128.Create((uint)x);
                        Vector128<uint> highIndices = lowIndices + Vector128.Create((uint)Vector128<uint>.Count);
                        Vector128<uint> low = CalculatePlanarVector(
                            topLow,
                            lowIndices,
                            leftSample,
                            topRight,
                            bottomLeft,
                            topWeight,
                            bottomWeight,
                            (uint)size,
                            rounding,
                            shift);

                        Vector128<uint> high = CalculatePlanarVector(
                            topHigh,
                            highIndices,
                            leftSample,
                            topRight,
                            bottomLeft,
                            topWeight,
                            bottomWeight,
                            (uint)size,
                            rounding,
                            shift);

                        Vector128.Narrow(low, high).StoreUnsafe(ref Unsafe.Add(ref rowBase, x));
                    }
                }

                for (; x < size; x++)
                {
                    uint horizontal = ((uint)(size - x - 1) * leftSample) + ((uint)(x + 1) * topRight);
                    uint vertical = ((uint)(size - y - 1) * top[x + 1]) + ((uint)(y + 1) * bottomLeft);
                    Unsafe.Add(ref rowBase, x) = (ushort)((horizontal + vertical + (uint)size) >> shift);
                }
            }
        }

        /// <summary>
        /// Calculates one 512-bit half of a planar prediction row.
        /// </summary>
        /// <param name="top">The top reference samples.</param>
        /// <param name="indices">The zero-based X coordinates.</param>
        /// <param name="left">The left reference sample for the row.</param>
        /// <param name="topRight">The top-right reference sample.</param>
        /// <param name="bottomLeft">The bottom-left reference sample.</param>
        /// <param name="topWeight">The top-reference weight.</param>
        /// <param name="bottomWeight">The bottom-left-reference weight.</param>
        /// <param name="size">The square block side.</param>
        /// <param name="rounding">The division rounding constant.</param>
        /// <param name="shift">The division shift.</param>
        /// <returns>The predicted samples as widened lanes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<uint> CalculatePlanarVector(
            Vector512<uint> top,
            Vector512<uint> indices,
            uint left,
            uint topRight,
            uint bottomLeft,
            uint topWeight,
            uint bottomWeight,
            uint size,
            uint rounding,
            int shift)
        {
            Vector512<uint> horizontal = ((Vector512.Create(size - 1) - indices) * left) + ((indices + Vector512<uint>.One) * topRight);
            Vector512<uint> vertical = (top * topWeight) + Vector512.Create(bottomLeft * bottomWeight);
            return (horizontal + vertical + Vector512.Create(rounding)) >> shift;
        }

        /// <summary>
        /// Calculates one 256-bit half of a planar prediction row.
        /// </summary>
        /// <param name="top">The top reference samples.</param>
        /// <param name="indices">The zero-based X coordinates.</param>
        /// <param name="left">The left reference sample for the row.</param>
        /// <param name="topRight">The top-right reference sample.</param>
        /// <param name="bottomLeft">The bottom-left reference sample.</param>
        /// <param name="topWeight">The top-reference weight.</param>
        /// <param name="bottomWeight">The bottom-left-reference weight.</param>
        /// <param name="size">The square block side.</param>
        /// <param name="rounding">The division rounding constant.</param>
        /// <param name="shift">The division shift.</param>
        /// <returns>The predicted samples as widened lanes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> CalculatePlanarVector(
            Vector256<uint> top,
            Vector256<uint> indices,
            uint left,
            uint topRight,
            uint bottomLeft,
            uint topWeight,
            uint bottomWeight,
            uint size,
            uint rounding,
            int shift)
        {
            Vector256<uint> horizontal = ((Vector256.Create(size - 1) - indices) * left) + ((indices + Vector256<uint>.One) * topRight);
            Vector256<uint> vertical = (top * topWeight) + Vector256.Create(bottomLeft * bottomWeight);
            return (horizontal + vertical + Vector256.Create(rounding)) >> shift;
        }

        /// <summary>
        /// Calculates one 128-bit half of a planar prediction row.
        /// </summary>
        /// <param name="top">The top reference samples.</param>
        /// <param name="indices">The zero-based X coordinates.</param>
        /// <param name="left">The left reference sample for the row.</param>
        /// <param name="topRight">The top-right reference sample.</param>
        /// <param name="bottomLeft">The bottom-left reference sample.</param>
        /// <param name="topWeight">The top-reference weight.</param>
        /// <param name="bottomWeight">The bottom-left-reference weight.</param>
        /// <param name="size">The square block side.</param>
        /// <param name="rounding">The division rounding constant.</param>
        /// <param name="shift">The division shift.</param>
        /// <returns>The predicted samples as widened lanes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<uint> CalculatePlanarVector(
            Vector128<uint> top,
            Vector128<uint> indices,
            uint left,
            uint topRight,
            uint bottomLeft,
            uint topWeight,
            uint bottomWeight,
            uint size,
            uint rounding,
            int shift)
        {
            Vector128<uint> horizontal = ((Vector128.Create(size - 1) - indices) * left) + ((indices + Vector128<uint>.One) * topRight);
            Vector128<uint> vertical = (top * topWeight) + Vector128.Create(bottomLeft * bottomWeight);
            return (horizontal + vertical + Vector128.Create(rounding)) >> shift;
        }
    }
}
