// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
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
        /// <param name="modeCosts">The mode rates that the caller read once.</param>
        /// <param name="coefficientCosts">The coefficient rates that the caller read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="residual">The residual samples.</param>
        /// <param name="stride">The number of residual samples between rows.</param>
        /// <param name="size">The transform size.</param>
        /// <param name="type">The transform type to estimate.</param>
        /// <param name="context">The coefficient contexts of the transform block.</param>
        /// <param name="mode">The prediction mode that selects the transform-type context.</param>
        /// <param name="filterMode">The filter intra mode, or <see cref="Av1FilterIntraMode.AllFilterIntraModes"/>.</param>
        /// <param name="isInter">Whether the block is inter predicted.</param>
        /// <param name="quantized">The storage that the estimate quantizes into.</param>
        /// <returns>The estimated rate-distortion cost.</returns>
        private long EstimateTransformTypeCost(
            Av1ModeCosts modeCosts,
            Av1CoefficientCosts coefficientCosts,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
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
            Span<int> transformed = transformCoefficients[..count];
            Span<int> dequantized = dequantizedCoefficients[..count];
            Av1ForwardTransformer.Transform2d(
                residual, transformed, (uint)stride, type, size, this.bitDepth.GetBitCount(), transformWorkspace);

            ushort endOfBlock = Av1ForwardQuantizer.QuantizeRegular(
                transformed,
                quantized[..count],
                dequantized,
                size,
                type,
                this.blockQIndex,
                this.quantization.DeltaQDc[0],
                this.quantization.DeltaQAc[0],
                this.bitDepth,
                this.blockWorkspace.EncoderOptions.Sharpness,
                this.blockWorkspace.GetQuantizationMatrix(Av1ComponentType.Luminance, size, type),
                this.blockWorkspace.GetInverseQuantizationMatrix(Av1ComponentType.Luminance, size, type));

            int rate = Av1SymbolEncoder.EstimateLumaCoefficientRate(
                modeCosts,
                coefficientCosts,
                quantized,
                endOfBlock,
                size,
                type,
                context,
                this.picture.Parent.FrameHeader.UseReducedTransformSet,
                filterMode,
                mode,
                isInter,
                this.BlockLossless);

            // The squared error is normalized to eight-bit precision with rounding, then loses the transform
            // scale, so it has the same four fractional bits as pixel-domain distortion.
            long error = Av1TransformBlockEncoder.GetTransformError(
                this.blockWorkspace, Av1ComponentType.Luminance, transformed, dequantized, size, type, this.bitDepth, out _);

            return Av1RateDistortion.GetCost(this.rateMultiplier, rate, error);
        }

        /// <summary>
        /// Prunes and orders legal luma transforms using complete or separated-axis estimates.
        /// </summary>
        /// <param name="modeCosts">The mode rates that the caller read once.</param>
        /// <param name="coefficientCosts">The coefficient rates that the caller read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="residual">The residual samples.</param>
        /// <param name="stride">The number of residual samples between rows.</param>
        /// <param name="size">The transform size.</param>
        /// <param name="context">The coefficient contexts of the transform block.</param>
        /// <param name="mode">The prediction mode that selects the transform-type context.</param>
        /// <param name="filterMode">The filter intra mode, or <see cref="Av1FilterIntraMode.AllFilterIntraModes"/>.</param>
        /// <param name="isInter">Whether the block is inter predicted.</param>
        /// <param name="allowedMask">The transform types that the search permits, one bit for each type.</param>
        /// <param name="pruningLevel">The pruning level of the current evaluation stage.</param>
        /// <param name="bestCost">The budget left for the transform block.</param>
        /// <param name="quantized">The storage that the estimates quantize into.</param>
        /// <param name="order">The search order, which the estimates sort by cost.</param>
        /// <returns>The transform types that remain after the pruning.</returns>
        private ushort PruneTransformTypesByEstimatedCost(
            Av1ModeCosts modeCosts,
            Av1CoefficientCosts coefficientCosts,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
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
                        modeCosts,
                        coefficientCosts,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        residual,
                        stride,
                        size,
                        type,
                        context,
                        mode,
                        filterMode,
                        isInter,
                        quantized));
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

                InlineArray4<long> horizontalCostStorage = default;
                InlineArray4<long> verticalCostStorage = default;
                InlineArray4<int> horizontalOrderStorage = default;
                InlineArray4<int> verticalOrderStorage = default;
                InlineArray4<bool> skipHorizontalStorage = default;
                InlineArray4<bool> skipVerticalStorage = default;
                Span<long> horizontalCosts = horizontalCostStorage;
                Span<long> verticalCosts = verticalCostStorage;
                Span<int> horizontalOrder = horizontalOrderStorage;
                Span<int> verticalOrder = verticalOrderStorage;
                Span<bool> skipHorizontal = skipHorizontalStorage;
                Span<bool> skipVertical = skipVerticalStorage;
                for (int axis = 0; axis < 4; axis++)
                {
                    horizontalOrder[axis] = verticalOrder[axis] = axis;
                    long cost = this.EstimateTransformTypeCost(
                        modeCosts,
                        coefficientCosts,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        residual,
                        stride,
                        size,
                        typeMap[axis],
                        context,
                        mode,
                        filterMode,
                        isInter,
                        quantized);

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
                        modeCosts,
                        coefficientCosts,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
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

        /// <summary>
        /// Selects the transform types that one transform block searches. The mode evaluation can restrict intra luma to its default or
        /// derived types, and inter luma to a type that the frame statistics make likely. A chroma block keeps the type that it derives.
        /// The type statistics of the frame prune a full search first. Then the estimated costs prune it, or the separable type model
        /// for an inter block.
        /// </summary>
        /// <param name="writer">The tile symbol encoder that prices the estimates.</param>
        /// <param name="modeCosts">The mode rates that the caller read once.</param>
        /// <param name="coefficientCosts">The coefficient rates that the caller read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="plane">The plane of the transform block.</param>
        /// <param name="isInter">Whether the block is inter predicted.</param>
        /// <param name="transformSize">The transform size.</param>
        /// <param name="mode">The prediction mode of the block.</param>
        /// <param name="filterIntraMode">The filter intra mode, or <see cref="Av1FilterIntraMode.AllFilterIntraModes"/>.</param>
        /// <param name="derivedTransformType">The type that a chroma block derives.</param>
        /// <param name="blockContext">The coefficient contexts of the transform block.</param>
        /// <param name="residual">The residual samples.</param>
        /// <param name="residualStride">The number of residual samples between rows.</param>
        /// <param name="costLimit">The budget left for the transform block.</param>
        /// <param name="coefficients">The storage that the estimates quantize into.</param>
        /// <param name="transformOrder">The search order, which the estimates sort by cost.</param>
        /// <param name="singleTypeAllowed">Whether only one type is allowed.</param>
        /// <returns>The types to search.</returns>
        private ushort GetTransformMask(
            Av1SymbolEncoder writer,
            Av1ModeCosts modeCosts,
            Av1CoefficientCosts coefficientCosts,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Av1Plane plane,
            bool isInter,
            Av1TransformSize transformSize,
            Av1PredictionMode mode,
            Av1FilterIntraMode filterIntraMode,
            Av1TransformType derivedTransformType,
            Av1TransformBlockContext blockContext,
            ReadOnlySpan<short> residual,
            int residualStride,
            long costLimit,
            Span<int> coefficients,
            Span<Av1TransformType> transformOrder,
            out bool singleTypeAllowed)
        {
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            ObuFrameHeader frameHeader = this.picture.Parent.FrameHeader;
            bool candidateStage = this.blockWorkspace.EvaluationStage == Av1EncoderEvaluationStage.Candidate;
            ReadOnlySpan<int> probabilities = transformTypeProbabilities.Slice(
                ((int)this.picture.Parent.FrameUpdateType * Av1TransformTypeProbabilities.FrameLength) +
                ((int)transformSize * Av1TransformTypeProbabilities.TypeCount),
                Av1TransformTypeProbabilities.TypeCount);

            // The search tries several types while this value stays AllTransformTypes. Otherwise it tries only this type.
            Av1TransformType allowedType = Av1TransformType.AllTransformTypes;
            int intraSearchLevel = !isInter && candidateStage ? settings.IntraTransformTypeSearchLevel : 0;
            int interThreshold = isInter && candidateStage ? settings.InterTransformTypeProbabilityThreshold : int.MaxValue;
            if (intraSearchLevel == 2 || interThreshold == 0)
            {
                // The default type is DCT_DCT for inter blocks, for transforms of 32x32 and larger, and for screen content. Other intra
                // blocks use the type that the prediction mode maps to.
                allowedType = isInter || transformSize >= Av1TransformSize.Size32x32 || frameHeader.AllowScreenContentTools
                    ? Av1TransformType.DctDct
                    : mode.ToTransformType();
            }
            else if (interThreshold != int.MaxValue)
            {
                // A likely DCT_DCT, or else a clearly most likely other type, is the only type searched.
                const int alternateTypeThresholdOffset = 100;
                if (probabilities[(int)Av1TransformType.DctDct] > interThreshold)
                {
                    allowedType = Av1TransformType.DctDct;
                }
                else
                {
                    int bestType = 0;
                    int bestProbability = 0;
                    for (int type = 1; type < Av1TransformTypeProbabilities.TypeCount; type++)
                    {
                        if (probabilities[type] > bestProbability)
                        {
                            bestProbability = probabilities[type];
                            bestType = type;
                        }
                    }

                    if (bestProbability > interThreshold + alternateTypeThresholdOffset)
                    {
                        allowedType = (Av1TransformType)bestType;
                    }
                }
            }

            Av1TransformSetType transformSet = Av1SymbolContextHelper.GetExtendedTransformSetType(
                transformSize, isInter, frameHeader.UseReducedTransformSet);

            if (plane != Av1Plane.Y)
            {
                allowedType = derivedTransformType;
            }

            // A filter-intra block takes the direction that its filter mode maps to. The Paeth filter maps to the DC direction. Each bit
            // selects one type in syntax order. The reduced intra set omits one-dimensional transforms whose direction does not agree with
            // the predictor.
            Av1PredictionMode direction = filterIntraMode == Av1FilterIntraMode.AllFilterIntraModes
                ? mode
                : filterIntraMode.ToIntraDirection();

            ReadOnlySpan<ushort> setMasks = [0x0001, 0x0201, 0x020F, 0x0E0F, 0x0FFF, 0xFFFF];
            ReadOnlySpan<ushort> reducedMasks =
                [0x080F, 0x040F, 0x080F, 0x020F, 0x080F, 0x040F, 0x080F, 0x080F, 0x040F, 0x080F, 0x040F, 0x080F, 0x0C0E];

            ushort usedMask = transformSet == Av1TransformSetType.IntraSet1
                ? reducedMasks[(int)direction]
                : setMasks[(int)transformSet];

            if (this.BlockLossless || transformSize.GetSquareUpSize() > Av1TransformSize.Size32x32 || usedMask == 1)
            {
                allowedType = Av1TransformType.DctDct;
            }

            ushort allowedMask;
            if (allowedType != Av1TransformType.AllTransformTypes)
            {
                allowedMask = (ushort)((1 << (int)allowedType) & usedMask);
            }
            else if (intraSearchLevel == 1)
            {
                // The derived intra types of each prediction direction, one bit per type in syntax order.
                ReadOnlySpan<ushort> derivedMasks =
                    [0x0209, 0x0403, 0x0805, 0x020F, 0x0009, 0x0009, 0x0009, 0x0805, 0x0403, 0x0205, 0x0403, 0x0805, 0x0209];

                allowedMask = (ushort)(derivedMasks[(int)direction] & usedMask);
            }
            else
            {
                allowedMask = usedMask;
                if (settings.TransformTypeProbabilityPruning != 0)
                {
                    allowedMask = Av1TransformTypeProbabilities.Prune(
                        probabilities, allowedMask, settings.TransformTypeProbabilityPruning, this.picture.Parent.FrameUpdateType);
                }

                int allowedCount = System.Numerics.BitOperations.PopCount((uint)allowedMask);
                int pruningLevel = this.blockWorkspace.EvaluationStage switch
                {
                    Av1EncoderEvaluationStage.Candidate => settings.CandidateInterTransformTypePruning,
                    Av1EncoderEvaluationStage.Winner => settings.WinnerInterTransformTypePruning,
                    _ => settings.DefaultInterTransformTypePruning
                };

                if (allowedCount > 2 && settings.EstimateTransformTypeRateDistortion)
                {
                    allowedMask = this.PruneTransformTypesByEstimatedCost(
                        modeCosts,
                        coefficientCosts,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        residual,
                        residualStride,
                        transformSize,
                        blockContext,
                        mode,
                        filterIntraMode,
                        isInter,
                        allowedMask,
                        pruningLevel,
                        costLimit,
                        coefficients,
                        transformOrder);
                }
                else if (pruningLevel >= 1 && isInter && allowedCount > (pruningLevel >= 4 ? 1 : 5))
                {
                    allowedMask = PruneInterTransformTypes(
                        residual, residualStride, transformSize, transformSet, pruningLevel, allowedMask, transformOrder);
                }
            }

            // At least one type is searched.
            if (allowedMask == 0)
            {
                allowedType = plane == Av1Plane.Y ? Av1TransformType.DctDct : derivedTransformType;
                allowedMask = (ushort)(1 << (int)allowedType);
            }

            singleTypeAllowed = allowedType != Av1TransformType.AllTransformTypes;
            return allowedMask;
        }

        /// <summary>
        /// Searches the transform types of one transform block and keeps the best one. The block statistics decide first. A residual
        /// that cannot survive quantization is settled as empty without a search. A low-variance block searches DCT_DCT alone, with
        /// its mean as the DC coefficient. Each remaining type is quantized and priced. A type whose rate alone costs more than the
        /// winner is not measured. The distortion domain follows the speed policy of the current evaluation stage. Policy 1 measures
        /// the candidates in the transform domain and the winner again in the pixel domain. Policy 2 uses the transform domain for all.
        /// </summary>
        /// <remarks>
        /// The candidate and best buffers swap on each improvement, so the winner is never copied inside the loop.
        /// On return the best span arguments hold the winner. The best reconstruction is valid only when the winner has coefficients and
        /// either <paramref name="winnerDestination"/> is not empty or the search measured the winner in pixels, and the winner is not in
        /// <paramref name="winnerDestination"/> instead (<see cref="TransformTypeSearchResult.WinnerInDestination"/>).
        /// </remarks>
        /// <param name="writer">The tile symbol encoder that prices the syntax.</param>
        /// <param name="tables">The rate tables and level storage of the writer, which the caller read once.</param>
        /// <param name="transformCoefficients">The forward transform output buffer.</param>
        /// <param name="dequantizedCoefficients">The dequantized coefficient buffer of the type estimates.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the transforms.</param>
        /// <param name="transformTypeProbabilities">The transform type probabilities of every update type and size.</param>
        /// <param name="plane">The plane of the transform block.</param>
        /// <param name="isInter">Whether the block is inter predicted, which selects the transform set and the trellis weights.</param>
        /// <param name="blockContext">The coefficient contexts of the transform block.</param>
        /// <param name="originContext">The coefficient contexts at the origin of the coding block, which price a predicted empty block.</param>
        /// <param name="source">The source transform block, from its top-left sample.</param>
        /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
        /// <param name="transformOrigin">The transform block origin in plane samples.</param>
        /// <param name="transformSize">The transform size.</param>
        /// <param name="mode">The prediction mode that selects the transform-type context.</param>
        /// <param name="filterIntraMode">The filter intra mode, or <see cref="Av1FilterIntraMode.AllFilterIntraModes"/>.</param>
        /// <param name="derivedTransformType">The type that a chroma block derives, which it searches alone.</param>
        /// <param name="costLimit">The budget left for the transform block.</param>
        /// <param name="winnerDestination">
        /// The prediction in the frame when a later transform block of an intra block predicts from the winner, and empty otherwise.
        /// A winner with coefficients that the search did not reconstruct is then reconstructed here in place, over its prediction.
        /// </param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="predictionStride">The number of prediction samples between rows.</param>
        /// <param name="residual">The residual samples.</param>
        /// <param name="inputStride">The number of residual samples between rows.</param>
        /// <param name="candidateReconstruction">The reconstruction of the candidate being measured.</param>
        /// <param name="bestReconstruction">The reconstruction of the winner.</param>
        /// <param name="candidateCoefficients">The quantized coefficients of the candidate.</param>
        /// <param name="bestCoefficients">The quantized coefficients of the winner.</param>
        /// <param name="candidateDequantized">The dequantized coefficients of the candidate.</param>
        /// <param name="bestDequantized">The dequantized coefficients of the winner.</param>
        /// <returns>The winner.</returns>
        private TransformTypeSearchResult SearchTransformType(
            Av1SymbolEncoder writer,
            in Av1CoefficientTables tables,
            Span<int> transformCoefficients,
            Span<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> transformTypeProbabilities,
            Av1Plane plane,
            bool isInter,
            Av1TransformBlockContext blockContext,
            Av1TransformBlockContext originContext,
            ReadOnlySpan<TSample> source,
            int sourceStride,
            Point transformOrigin,
            Av1TransformSize transformSize,
            Av1PredictionMode mode,
            Av1FilterIntraMode filterIntraMode,
            Av1TransformType derivedTransformType,
            long costLimit,
            scoped Span<TSample> winnerDestination,
            scoped ReadOnlySpan<TSample> prediction,
            int predictionStride,
            scoped Span<short> residual,
            int inputStride,
            ref Span<TSample> candidateReconstruction,
            ref Span<TSample> bestReconstruction,
            ref Span<int> candidateCoefficients,
            ref Span<int> bestCoefficients,
            ref Span<int> candidateDequantized,
            ref Span<int> bestDequantized)
        {
            Av1EncoderSpeedSettings settings = this.picture.Parent.SpeedSettings;
            Av1ComponentType componentType = plane == Av1Plane.Y ? Av1ComponentType.Luminance : Av1ComponentType.Chroma;
            int planeIndex = (int)plane;
            int transformWidth = transformSize.GetWidth();
            int transformHeight = transformSize.GetHeight();
            int transformSampleCount = transformSize.GetSize2d();
            bool useReducedTransformSet = this.picture.Parent.FrameHeader.UseReducedTransformSet;

            // The residual energy of the visible samples gates coefficient refinement for every type, and selects
            // transform-domain distortion by the speed policy of the current evaluation stage.
            Size visibleSize = this.blockWorkspace.GetVisibleSize(plane, transformOrigin, transformWidth, transformHeight);
            int visibleWidth = visibleSize.Width;
            int visibleHeight = visibleSize.Height;
            bool borderBlock = this.blockWorkspace.BorderPad &&
                (visibleWidth < transformWidth || visibleHeight < transformHeight);

            // A block crossing the frame edge is padded as DCT_DCT pads it before the skip prediction and the type
            // pruning read the residual.
            if (borderBlock)
            {
                Av1TransformBlockEncoder.PadBorderResidual(
                    this.blockWorkspace, plane, transformOrigin, residual, inputStride, transformWidth, transformHeight, Av1TransformType.DctDct);
            }

            int predictDcLevel = this.blockWorkspace.EvaluationStage switch
            {
                Av1EncoderEvaluationStage.Candidate => settings.ModePredictDcLevel,
                Av1EncoderEvaluationStage.Winner => settings.WinnerPredictDcLevel,
                _ => settings.DefaultPredictDcLevel
            };

            // A 64-point transform is excluded from skip prediction, because its DC coefficient carries no scaling
            // term. Its residual is measured without mean and variance.
            bool predictDcBlock = predictDcLevel >= 1 && transformWidth != 64 && transformHeight != 64;
            long perPixelMean = 0;
            ulong blockVariance = 0;
            uint blockMseQ8;
            long blockError = predictDcBlock
                ? Av1TransformBlockEncoder.GetBlockStatistics(
                    residual, inputStride, visibleWidth, visibleHeight, this.bitDepth, out blockMseQ8, out perPixelMean, out blockVariance)
                : Av1TransformBlockEncoder.GetBlockError(
                    residual, inputStride, visibleWidth, visibleHeight, this.bitDepth, out blockMseQ8);

            int acDequantizer = Av1QuantizationLookup.GetAcQuant(
                this.blockQIndex, this.quantization.DeltaQAc[planeIndex], this.bitDepth);

            // The DC prediction settles a block whose residual cannot survive quantization. From level two, it keeps only the DC
            // coefficient of a low-variance block. The chroma of an intra block keeps its full transform. A DC-only block searches DCT_DCT
            // alone and measures its distortion in the pixel domain.
            bool dcOnlyCandidate = false;
            bool predictedSkip = predictDcBlock && Av1TransformBlockEncoder.PredictSkippedBlock(
                transformSize,
                Av1QuantizationLookup.GetDcQuant(this.blockQIndex, this.quantization.DeltaQDc[planeIndex], this.bitDepth),
                acDequantizer,
                this.bitDepth,
                perPixelMean,
                blockVariance,
                out dcOnlyCandidate);

            // A DC-only chroma block quantizes with DCT_DCT and reconstructs with the type that it derives.
            Av1TransformType derivedType = derivedTransformType;
            bool dcOnlyBlock = predictDcBlock && dcOnlyCandidate && predictDcLevel > 1 && (plane == Av1Plane.Y || isInter);

            // A predicted empty block searches no type, and a DC-only block searches DCT_DCT alone. Every other block searches the types
            // that the transform mask selects, in the order that its estimates leave. The order is taken as a span once, outside the loops,
            // and not through the inline array indexer.
            InlineArray16<Av1TransformType> transformOrderStorage = default;
            Span<Av1TransformType> transformOrder = transformOrderStorage;
            for (int type = 0; type < Av1TransformTypeProbabilities.TypeCount; type++)
            {
                transformOrder[type] = (Av1TransformType)type;
            }

            bool singleTypeAllowed = false;
            ushort candidateTransformMask = 0;
            if (!predictedSkip)
            {
                candidateTransformMask = dcOnlyBlock
                    ? (ushort)1
                    : this.GetTransformMask(
                        writer,
                        tables.ModeCosts,
                        tables.CoefficientCosts,
                        transformCoefficients,
                        dequantizedCoefficients,
                        transformWorkspace,
                        transformTypeProbabilities,
                        plane,
                        isInter,
                        transformSize,
                        mode,
                        filterIntraMode,
                        derivedTransformType,
                        blockContext,
                        residual,
                        inputStride,
                        costLimit,
                        candidateCoefficients,
                        transformOrder,
                        out singleTypeAllowed);
            }

            (uint Distortion, uint Satd) refinementThresholds = this.blockWorkspace.EvaluationStage switch
            {
                Av1EncoderEvaluationStage.Candidate => settings.ModeCoefficientOptimizationThresholds,
                Av1EncoderEvaluationStage.Winner => settings.WinnerCoefficientOptimizationThresholds,
                _ => settings.DefaultCoefficientOptimizationThresholds
            };

            (int Type, uint Threshold) distortionPolicy = Av1TransformBlockEncoder.GetDistortionPolicy(this.blockWorkspace, settings);
            int dequantShift = this.bitDepth == Av1BitDepth.EightBit ? 3 : this.bitDepth.GetBitCount() - 5;
            ulong quantizerStep = (uint)(acDequantizer >> dequantShift);
            bool skipTrellis = !settings.EnableCoefficientOptimization ||
                blockMseQ8 > refinementThresholds.Distortion * quantizerStep * quantizerStep;

            // Any 64-point transform keeps half of its coefficients, so its transform-domain error is not
            // comparable. A search with one permitted type has nothing to compare, so it measures that type in the
            // pixel domain directly instead of twice.
            bool useTransformDomainDistortion = distortionPolicy.Type > 0 &&
                blockMseQ8 >= distortionPolicy.Threshold &&
                transformSize.GetSquareUpSize() != Av1TransformSize.Size64x64 &&
                !dcOnlyBlock;

            bool measureWinnerInPixelDomain = distortionPolicy.Type == 1 && useTransformDomainDistortion;
            if (measureWinnerInPixelDomain && (singleTypeAllowed || candidateTransformMask == 1))
            {
                measureWinnerInPixelDomain = useTransformDomainDistortion = false;
            }

            int codedCoefficientCount = transformSize.GetAdjusted().GetSize2d();

            long highEnergyThreshold = 128L * 128 * transformSampleCount;
            bool isHighEnergy = !dcOnlyBlock && blockError >= highEnergyThreshold;
            int adaptiveSearchLevel = settings.InterAdaptiveTransformSearchLevel;

            TransformTypeSearchResult best = default;
            best.Cost = long.MaxValue;
            best.Sse = blockError;
            bool bestReconstructed = false;

            // A predicted empty block searches no transform type. The prediction stands as the reconstruction. The block costs the
            // all-zero flag alone, priced with the contexts at the coding block origin, not with the contexts of earlier transform blocks.
            // Only the end of block is set to zero. The coefficient buffer keeps its values, because every reader stops at the end of block.
            if (predictedSkip)
            {
                best.Type = Av1TransformType.DctDct;
                best.State.TransformType = plane == Av1Plane.Y ? Av1TransformType.DctDct : derivedType;
                best.ReconstructionState = best.State;
                best.Rate = Av1SymbolEncoder.GetTransformBlockSkipCost(
                    tables.CoefficientCosts,
                    true,
                    Av1SymbolContextHelper.GetTransformSizeContext(transformSize),
                    originContext.SkipContext,
                    componentType);

                // A block with no coefficients publishes its prediction, so the winner reconstruction is not written.
                best.Distortion = blockError;
                bestReconstructed = true;
            }

            // Every type of this transform block uses the same residual, buffers, rate tables and settings, so they
            // are described once here. Only the type and the swapped coefficient buffers change in the loop.
            Av1TransformTypeTrial trial = new()
            {
                Workspace = this.blockWorkspace,
                Writer = writer,
                Tables = tables,
                Context = blockContext,
                Residual = residual,
                ResidualStride = inputStride,
                TransformCoefficients = transformCoefficients,
                TransformWorkspace = transformWorkspace,
                TransformSize = transformSize,
                IntraDirection = mode,
                FilterIntraMode = filterIntraMode,
                UseReducedTransformSet = useReducedTransformSet,
                UsesInterTransformSet = isInter,
                QIndex = this.blockQIndex,
                Lossless = this.BlockLossless,
                DcDeltaQ = this.quantization.DeltaQDc[planeIndex],
                AcDeltaQ = this.quantization.DeltaQAc[planeIndex],
                Sharpness = this.blockWorkspace.EncoderOptions.Sharpness,
                BitDepth = this.bitDepth,
                ComponentType = componentType,
                RateMultiplier = this.rateMultiplier,
                IsInter = isInter,
                UseChromaWeights = this.picture.Parent.SpeedSettings.UseChromaTrellisRateMultiplier,
                SkipTrellis = skipTrellis,
                SatdThreshold = refinementThresholds.Satd,
                DcOnly = dcOnlyBlock,
                PerPixelMean = perPixelMean
            };

            for (int typeIndex = 0; typeIndex < Av1TransformTypeProbabilities.TypeCount; typeIndex++)
            {
                Av1TransformType transformType = transformOrder[typeIndex];
                if (transformType == Av1TransformType.Invalid || (candidateTransformMask & (1 << (int)transformType)) == 0)
                {
                    continue;
                }

                // A block crossing the frame edge fills its hidden residual for each transform type.
                if (borderBlock)
                {
                    Av1TransformBlockEncoder.PadBorderResidual(
                        this.blockWorkspace, plane, transformOrigin, residual, inputStride, transformWidth, transformHeight, transformType);
                }

                Av1EncoderTransformBlockState candidateState = default;
                int candidateRate = Av1TransformBlockEncoder.EncodeTypeSearchCandidate(
                    in trial, transformType, candidateCoefficients, candidateDequantized, ref candidateState, out bool candidateMatricesDropped);

                // Distortion is never negative. A candidate whose rate alone already costs more than the current
                // winner cannot replace it, so it needs no distortion measurement.
                if (Av1RateDistortion.GetCost(this.rateMultiplier, candidateRate, 0) > best.Cost)
                {
                    continue;
                }

                // A DC-only chroma block reconstructs with the type derived from luma.
                Av1EncoderTransformBlockState reconstructionState = candidateState;
                if (dcOnlyBlock && plane != Av1Plane.Y)
                {
                    reconstructionState.TransformType = derivedType;
                }

                long candidateDistortion;
                long candidateSse = blockError;
                bool candidateReconstructed = false;
                if (candidateState.EndOfBlock == 0)
                {
                    // An empty block reconstructs the prediction, so its error is the residual energy.
                    candidateDistortion = blockError;
                }
                else if (useTransformDomainDistortion)
                {
                    candidateDistortion = Av1TransformBlockEncoder.GetTransformError(
                        this.blockWorkspace,
                        componentType,
                        transformCoefficients[..codedCoefficientCount],
                        candidateDequantized[..codedCoefficientCount],
                        transformSize,
                        transformType,
                        this.bitDepth,
                        out candidateSse,
                        useMatrix: !candidateMatricesDropped);
                }
                else
                {
                    // A 64x64 transform drops three coefficient quadrants and a high-energy block can clamp during
                    // reconstruction. The transform-domain error then decides whether the pixel-domain measurement
                    // is trustworthy and serves as its floor.
                    bool is64x64 = transformSize == Av1TransformSize.Size64x64;
                    long transformDomainDistortion = 0;
                    long transformDomainEnergy = 0;
                    long energyDifference = long.MaxValue;
                    if (is64x64 || isHighEnergy)
                    {
                        transformDomainDistortion = Av1TransformBlockEncoder.GetTransformError(
                            this.blockWorkspace,
                            componentType,
                            transformCoefficients[..codedCoefficientCount],
                            candidateDequantized[..codedCoefficientCount],
                            transformSize,
                            transformType,
                            this.bitDepth,
                            out transformDomainEnergy,
                            useMatrix: !candidateMatricesDropped);

                        energyDifference = blockError - transformDomainEnergy;
                    }

                    if (!is64x64 || !isHighEnergy || energyDifference * 2 < transformDomainEnergy)
                    {
                        candidateDistortion = Av1TransformBlockEncoder.ReconstructPredictionLossyCandidate<TSample, TOperator>(
                            this.blockWorkspace,
                            transformWorkspace,
                            candidateDequantized,
                            source,
                            sourceStride,
                            transformOrigin,
                            prediction,
                            predictionStride,
                            candidateReconstruction,
                            transformWidth,
                            transformSize,
                            plane,
                            this.BlockLossless,
                            this.bitDepth,
                            reconstructionState);

                        candidateReconstructed = true;
                        if (isHighEnergy && candidateDistortion < transformDomainDistortion)
                        {
                            candidateDistortion = transformDomainDistortion;
                        }
                    }
                    else
                    {
                        candidateDistortion = transformDomainDistortion + energyDifference;
                    }
                }

                long candidateCost = Av1RateDistortion.GetCost(this.rateMultiplier, candidateRate, candidateDistortion);
                if (candidateCost < best.Cost)
                {
                    Span<TSample> previousReconstruction = bestReconstruction;
                    bestReconstruction = candidateReconstruction;
                    candidateReconstruction = previousReconstruction;

                    Span<int> previousCoefficients = bestCoefficients;
                    bestCoefficients = candidateCoefficients;
                    candidateCoefficients = previousCoefficients;

                    Span<int> previousDequantized = bestDequantized;
                    bestDequantized = candidateDequantized;
                    candidateDequantized = previousDequantized;

                    best.Cost = candidateCost;
                    best.Type = transformType;
                    best.Rate = candidateRate;
                    best.Distortion = candidateDistortion;
                    best.Sse = candidateSse;
                    best.State = candidateState;
                    best.ReconstructionState = reconstructionState;
                    bestReconstructed = candidateReconstructed;
                }

                // With an adaptive search level, a winner that is already far above the remaining budget ends the search. When the
                // settings ask for it, a type that quantizes the block to zero also ends the search.
                if (adaptiveSearchLevel != 0 && best.Cost - (best.Cost >> adaptiveSearchLevel) > costLimit)
                {
                    break;
                }

                if (settings.SkipTransformSearchAfterEmptyBlock && best.State.EndOfBlock == 0)
                {
                    break;
                }
            }

            // The winner is reconstructed only when it has coefficients and either a later transform block predicts from it, or policy 1
            // measures its final distortion in pixels. An empty winner reconstructs to its prediction, which the caller uses instead.
            bool reconstructWinner = !winnerDestination.IsEmpty;
            bool measureWinner = measureWinnerInPixelDomain && best.State.EndOfBlock != 0;
            if (!bestReconstructed && best.State.EndOfBlock != 0 && (reconstructWinner || measureWinner))
            {
                // A later transform block predicts from the frame, so a winner that it reads is reconstructed there in place, over
                // its prediction. The caller then copies nothing. No type is measured after this, so the prediction is no longer needed.
                long pixelDistortion = Av1TransformBlockEncoder.ReconstructPredictionLossyCandidate<TSample, TOperator>(
                    this.blockWorkspace,
                    transformWorkspace,
                    bestDequantized,
                    source,
                    sourceStride,
                    transformOrigin,
                    prediction,
                    predictionStride,
                    reconstructWinner ? winnerDestination : bestReconstruction,
                    reconstructWinner ? predictionStride : transformWidth,
                    transformSize,
                    plane,
                    this.BlockLossless,
                    this.bitDepth,
                    best.ReconstructionState);

                best.WinnerInDestination = reconstructWinner;

                if (measureWinner)
                {
                    best.Distortion = pixelDistortion;
                    best.Sse = blockError;
                }
            }

            // A block crossing the frame edge leaves the residual with the border padding of the winner, which
            // the transform split model then reads.
            if (borderBlock && !predictedSkip)
            {
                Av1TransformBlockEncoder.PadBorderResidual(
                    this.blockWorkspace, plane, transformOrigin, residual, inputStride, transformWidth, transformHeight, best.Type);
            }

            return best;
        }

        /// <summary>
        /// The winner of a transform type search.
        /// </summary>
        private struct TransformTypeSearchResult
        {
            /// <summary>
            /// The rate-distortion cost that won the search.
            /// </summary>
            public long Cost;

            /// <summary>
            /// The winning transform type.
            /// </summary>
            public Av1TransformType Type;

            /// <summary>
            /// The coefficient rate of the winner.
            /// </summary>
            public int Rate;

            /// <summary>
            /// The distortion of the winner.
            /// </summary>
            public long Distortion;

            /// <summary>
            /// The energy that the block leaves uncoded. When the search measured the winner in the transform domain, the energy is in that
            /// domain. Otherwise it is the visible residual energy.
            /// </summary>
            public long Sse;

            /// <summary>
            /// The coded state of the winner.
            /// </summary>
            public Av1EncoderTransformBlockState State;

            /// <summary>
            /// The state that reconstructs the winner, which for a DC-only chroma block carries the derived type.
            /// </summary>
            public Av1EncoderTransformBlockState ReconstructionState;

            /// <summary>
            /// Whether the search reconstructed the winner in place in the winner destination, so the frame already holds it.
            /// When this is false and a later transform block predicts from a winner with coefficients, the best reconstruction
            /// holds the winner, which the search measured in pixels, and the caller copies it into the frame.
            /// </summary>
            public bool WinnerInDestination;
        }
    }
}
