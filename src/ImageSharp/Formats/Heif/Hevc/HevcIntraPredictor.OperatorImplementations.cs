// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Provides the closed planar, DC, and angular prediction operators.
/// </content>
internal static partial class HevcIntraPredictor
{
    /// <summary>
    /// Implements planar interpolation between the top, left, bottom-left, and top-right references.
    /// </summary>
    private readonly struct PlanarPredictionOperator : IHevcIntraPredictionOperator<PlanarPredictionOperator>
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
            uint bottomLeft = left[size];
            uint topRight = top[size];
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
    }

    /// <summary>
    /// Implements DC prediction and its optional luma boundary filter.
    /// </summary>
    private readonly struct DcPredictionOperator : IHevcIntraPredictionOperator<DcPredictionOperator>
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
    }

    /// <summary>
    /// Implements the thirty-three directional intra-prediction modes.
    /// </summary>
    private readonly struct AngularPredictionOperator : IHevcIntraPredictionOperator<AngularPredictionOperator>
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
            if (mode == VerticalMode)
            {
                PredictVertical(top, left, destination, destinationStride, size, bitDepth, filterPredictionEdges);
                return;
            }

            if (mode == HorizontalMode)
            {
                PredictHorizontal(top, left, destination, destinationStride, size, bitDepth, filterPredictionEdges);
                return;
            }

            bool vertical = mode >= FirstVerticalMode;
            int angleMode = vertical ? mode - VerticalMode : HorizontalMode - mode;
            int absoluteAngleMode = Math.Abs(angleMode);
            int angle = PredictionAngles[absoluteAngleMode] * Math.Sign(angleMode);
            ReadOnlySpan<ushort> main = vertical ? top : left;
            ReadOnlySpan<ushort> side = vertical ? left : top;
            Span<ushort> temporaryBlock = scratch[..(size * size)];
            Span<ushort> extendedReference = scratch.Slice(size * size, (4 * size) + 1);
            int mainOrigin = 0;

            if (angle < 0)
            {
                mainOrigin = size * 2;
                main[..(size + 1)].CopyTo(extendedReference[mainOrigin..]);
                int inverseAngle = InversePredictionAngles[absoluteAngleMode];
                int inverseAngleSum = 128;
                int minimumIndex = (size * angle) >> 5;
                for (int index = -1; index > minimumIndex; index--)
                {
                    inverseAngleSum += inverseAngle;
                    extendedReference[mainOrigin + index] = side[inverseAngleSum >> 8];
                }

                main = extendedReference;
            }

            Span<ushort> prediction = vertical ? destination : temporaryBlock;
            int predictionStride = vertical ? destinationStride : size;
            PredictAngularRows(main, mainOrigin, prediction, predictionStride, size, angle);
            if (!vertical)
            {
                TransposeBlock(temporaryBlock, destination, destinationStride, size);
            }
        }
    }
}
