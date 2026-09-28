// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Orders transform candidates using quantized coefficient rate and transform-domain error.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// Stably orders estimated costs and their associated candidate indices.
    /// </summary>
    /// <typeparam name="T">The candidate index type.</typeparam>
    /// <param name="costs">The populated candidate costs.</param>
    /// <param name="order">The corresponding candidate indices.</param>
    private static void SortTransformEstimates<T>(Span<long> costs, Span<T> order)
        where T : unmanaged
    {
        for (int index = 1; index < costs.Length; index++)
        {
            long cost = costs[index];
            T candidate = order[index];
            int position = index;
            while (position > 0 && costs[position - 1] > cost)
            {
                costs[position] = costs[position - 1];
                order[position] = order[position - 1];
                position--;
            }

            costs[position] = cost;
            order[position] = candidate;
        }
    }

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Estimates one luma transform without inverse reconstruction or coefficient refinement.
        /// </summary>
        private long EstimateTransformTypeCost(
            Av1SymbolEncoder writer,
            ReadOnlySpan<short> residual,
            int stride,
            Av1TransformSize size,
            Av1TransformType type,
            Av1TransformBlockContext context,
            Av1PredictionMode mode,
            Av1FilterIntraMode filterMode,
            bool isInter,
            Span<int> quantized)
        {
            int count = size.GetAdjusted().GetSize2d();
            Span<int> transformed = this.blockWorkspace.TransformCoefficients[..count];
            Span<int> dequantized = this.blockWorkspace.DequantizedCoefficients[..count];
            Av1ForwardTransformer.Transform2d(
                residual, transformed, (uint)stride, type, size, this.bitDepth.GetBitCount(), this.blockWorkspace.TransformWorkspace);

            ushort endOfBlock = Av1ForwardQuantizer.QuantizeRegular(
                transformed,
                quantized[..count],
                dequantized,
                size,
                type,
                this.superblockQIndex,
                this.quantization.DeltaQDc[0],
                this.quantization.DeltaQAc[0],
                this.bitDepth,
                this.blockWorkspace.EncoderOptions.Sharpness,
                this.blockWorkspace.GetQuantizationMatrix(Av1ComponentType.Luminance, size, type),
                this.blockWorkspace.GetInverseQuantizationMatrix(Av1ComponentType.Luminance, size, type));

            int rate = writer.EstimateLumaCoefficientRate(
                quantized, endOfBlock, size, type, context, this.picture.Parent.FrameHeader.UseReducedTransformSet, filterMode, mode, isInter);

            // The squared error is normalized to eight-bit precision with rounding, then loses the transform
            // scale, so it has the same four fractional bits as pixel-domain distortion.
            long error = Av1TransformBlockEncoder.GetTransformError(
                this.blockWorkspace, Av1ComponentType.Luminance, transformed, dequantized, size, type, this.bitDepth, out _);
            return Av1RateDistortion.GetCost(this.rateMultiplier, rate, error);
        }

        /// <summary>
        /// Prunes and orders legal luma transforms using complete or separated-axis estimates.
        /// </summary>
        private ushort PruneTransformTypesByEstimatedCost(
            Av1SymbolEncoder writer,
            ReadOnlySpan<short> residual,
            int stride,
            Av1TransformSize size,
            Av1TransformBlockContext context,
            Av1PredictionMode mode,
            Av1FilterIntraMode filterMode,
            bool isInter,
            ushort allowedMask,
            int pruningLevel,
            long bestCost,
            Span<int> quantized,
            Span<Av1TransformType> order)
        {
            ReadOnlySpan<int> pruneFactors = [200, 200, 120, 80, 40];
            ReadOnlySpan<int> candidatePercentages = [80, 80, 70, 50, 30];
            int pruneFactor = pruneFactors[pruningLevel];
            int allowedCount = System.Numerics.BitOperations.PopCount((uint)allowedMask);
            InlineArray16<long> costStorage = default;
            Span<long> costs = costStorage;
            int count = 0;
            int last = Av1TransformTypeProbabilities.TypeCount - 1;
            int factorScale = 1000;
            int selectedCount = allowedCount;
            if (allowedCount <= 7)
            {
                for (int index = 0; index < Av1TransformTypeProbabilities.TypeCount; index++)
                {
                    Av1TransformType type = (Av1TransformType)index;
                    if ((allowedMask & (1 << index)) == 0)
                    {
                        order[last--] = type;
                        continue;
                    }

                    order[count] = type;
                    costs[count++] = Math.Max(1, this.EstimateTransformTypeCost(
                        writer, residual, stride, size, type, context, mode, filterMode, isInter, quantized));
                }
            }
            else
            {
                // Probe four horizontal bases with vertical DCT, then three vertical bases with the
                // best horizontal basis. Combining those rankings avoids all sixteen full probes.
                ReadOnlySpan<Av1TransformType> typeMap =
                [
                    Av1TransformType.DctDct, Av1TransformType.DctAdst, Av1TransformType.DctFlipAdst, Av1TransformType.VerticalDct,
                    Av1TransformType.AdstDct, Av1TransformType.AdstAdst, Av1TransformType.AdstFlipAdst, Av1TransformType.VerticalAdst,
                    Av1TransformType.FlipAdstDct, Av1TransformType.FlipAdstAdst, Av1TransformType.FlipAdstFlipAdst, Av1TransformType.VerticalFlipAdst,
                    Av1TransformType.HorizontalDct, Av1TransformType.HorizontalAdst, Av1TransformType.HorizontalFlipAdst, Av1TransformType.Identity
                ];
                ReadOnlySpan<byte> verticalPattern = [0, 0, 1, 1, 0, 2, 1, 2, 2, 0, 3, 1, 3, 2, 3, 3];
                ReadOnlySpan<byte> horizontalPattern = [0, 1, 0, 1, 2, 0, 2, 1, 2, 3, 0, 3, 1, 3, 2, 3];

                InlineArray4<long> horizontalCosts = default;
                InlineArray4<long> verticalCosts = default;
                InlineArray4<int> horizontalOrder = default;
                InlineArray4<int> verticalOrder = default;
                InlineArray4<bool> skipHorizontal = default;
                InlineArray4<bool> skipVertical = default;
                for (int axis = 0; axis < 4; axis++)
                {
                    horizontalOrder[axis] = verticalOrder[axis] = axis;
                    long cost = this.EstimateTransformTypeCost(
                        writer, residual, stride, size, typeMap[axis], context, mode, filterMode, isInter, quantized);

                    horizontalCosts[axis] = cost;
                    skipHorizontal[axis] = cost - (cost >> 2) > bestCost;
                }

                SortTransformEstimates<int>(horizontalCosts, horizontalOrder);
                for (int axis = 1; axis < 4; axis++)
                {
                    if (horizontalCosts[axis] > horizontalCosts[0] * 1.2)
                    {
                        skipHorizontal[horizontalOrder[axis]] = true;
                    }
                }

                if (skipHorizontal[horizontalOrder[0]])
                {
                    return 0;
                }

                verticalCosts[0] = horizontalCosts[0];
                for (int axis = 1; axis < 4; axis++)
                {
                    long cost = this.EstimateTransformTypeCost(
                        writer,
                        residual,
                        stride,
                        size,
                        typeMap[(axis * 4) + horizontalOrder[0]],
                        context,
                        mode,
                        filterMode,
                        isInter,
                        quantized);

                    verticalCosts[axis] = cost;
                    skipVertical[axis] = cost - (cost >> 2) > bestCost;
                }

                SortTransformEstimates<int>(verticalCosts, verticalOrder);
                for (int axis = 1; axis < 4; axis++)
                {
                    if (verticalCosts[axis] > verticalCosts[0] * 1.2)
                    {
                        skipVertical[verticalOrder[axis]] = true;
                    }
                }

                for (int index = 0; index < Av1TransformTypeProbabilities.TypeCount; index++)
                {
                    int vertical = verticalPattern[index];
                    int horizontal = horizontalPattern[index];
                    Av1TransformType type = typeMap[(verticalOrder[vertical] * 4) + horizontalOrder[horizontal]];
                    if ((allowedMask & (1 << (int)type)) == 0 ||
                        skipHorizontal[horizontalOrder[horizontal]] || skipVertical[verticalOrder[vertical]])
                    {
                        order[last--] = type;
                    }
                    else
                    {
                        order[count] = type;
                        costs[count++] = Math.Max(1, verticalCosts[vertical] + horizontalCosts[horizontal]);
                    }
                }

                factorScale = 1800;
                selectedCount = Math.Min(count, ((allowedCount * candidatePercentages[pruningLevel]) + 50) / 100);
            }

            SortTransformEstimates<Av1TransformType>(costs[..count], order[..count]);
            ushort retained = (ushort)(1 << (int)order[0]);
            for (int index = 1; index < selectedCount; index++)
            {
                long factor = factorScale * (costs[index] - costs[0]) / costs[0];
                if (factor >= pruneFactor)
                {
                    break;
                }

                retained |= (ushort)(1 << (int)order[index]);
            }

            return (ushort)(retained & allowedMask);
        }
    }
}
