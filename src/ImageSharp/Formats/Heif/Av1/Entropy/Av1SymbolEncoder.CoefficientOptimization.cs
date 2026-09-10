// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <content>
/// Refines quantized coefficients using their entropy rate and reconstruction error.
/// </content>
internal sealed partial class Av1SymbolEncoder
{
    /// <summary>
    /// Reduces coefficient levels and the coded end position when their combined rate and distortion decrease.
    /// </summary>
    /// <param name="original">The forward-transform coefficients.</param>
    /// <param name="quantized">The quantized coefficients to refine.</param>
    /// <param name="dequantized">The corresponding reconstruction coefficients to refine.</param>
    /// <param name="transformSize">The coded transform size.</param>
    /// <param name="transformType">The coded transform type.</param>
    /// <param name="componentType">The luminance or chroma component.</param>
    /// <param name="context">The coefficient-neighbor contexts.</param>
    /// <param name="dcDequantizer">The DC reconstruction step.</param>
    /// <param name="acDequantizer">The AC reconstruction step.</param>
    /// <param name="rateMultiplier">The block rate-distortion multiplier.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="isInter">Whether the prediction uses an inter transform set.</param>
    /// <param name="useChromaWeights">Whether to use the chroma-specific optimization weights.</param>
    /// <param name="endOfBlock">The nonzero input end position.</param>
    /// <returns>The refined end position.</returns>
    public ushort OptimizeCoefficients(
        ReadOnlySpan<int> original,
        Span<int> quantized,
        Span<int> dequantized,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        Av1ComponentType componentType,
        Av1TransformBlockContext context,
        int dcDequantizer,
        int acDequantizer,
        int rateMultiplier,
        Av1BitDepth bitDepth,
        bool isInter,
        bool useChromaWeights,
        ushort endOfBlock)
    {
        Av1TransformSize adjusted = transformSize.GetAdjusted();
        int width = adjusted.GetWidth();
        int height = adjusted.GetHeight();
        Av1LevelBuffer levels = this.PrepareCoefficientScratch(width, height, endOfBlock > 1, out _);
        if (endOfBlock > 1)
        {
            levels.Initialize(quantized);
        }

        Av1TransformClass transformClass = transformType.ToClass();
        Av1TransformSize sizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);
        ReadOnlySpan<int> costs = this.CoefficientCosts.GetPlane((int)sizeContext, (int)componentType);
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        int shift = transformSize.GetScale();
        int planeWeight = componentType == Av1ComponentType.Luminance ? isInter ? 16 : 17 : useChromaWeights ? isInter ? 10 : 13 : 20;

        // Coefficient errors remain at their native precision. Scale the rate multiplier into that
        // domain once; each comparison retains the signed error change relative to a zero coefficient.
        long multiplier = ((long)rateMultiplier * 8 * planeWeight) << (2 * (bitDepth.GetBitCount() - 8));
        multiplier = (multiplier + 16) >> 5;
        int accumulatedRate = this.GetOptimizationEndOfBlockRate(endOfBlock, transformSize, componentType, transformClass, costs);
        long accumulatedDistortion = 0;
        int scanIndex = endOfBlock - 1;
        int coefficientIndex = scan[scanIndex];
        int magnitude = Math.Abs(quantized[coefficientIndex]);
        InlineArray3<int> nonzeroIndices = default;
        nonzeroIndices[0] = coefficientIndex;
        int nonzeroCount = 1;

        // The last nonzero coefficient cannot disappear in this first step. A level-one tail instead
        // seeds the later end-position search, which can remove it together with preceding zeros.
        if (magnitude >= 2)
        {
            ReduceGeneralCoefficient(
                original,
                quantized,
                dequantized,
                levels,
                scan,
                scanIndex,
                endOfBlock,
                transformSize,
                transformClass,
                costs,
                context.DcSignContext,
                dcDequantizer,
                acDequantizer,
                multiplier,
                shift,
                ref accumulatedRate,
                ref accumulatedDistortion);
        }
        else
        {
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(levels, scanIndex);
            accumulatedRate += GetOptimizationCoefficientRate(
                true,
                coefficientIndex,
                magnitude,
                quantized[coefficientIndex] < 0 ? 1 : 0,
                coefficientContext,
                context.DcSignContext,
                costs,
                levels,
                transformClass);

            long reconstructed = dequantized[coefficientIndex];
            accumulatedDistortion = (reconstructed * (reconstructed - (2L * original[coefficientIndex]))) << (2 * shift);
        }

        scanIndex--;
        for (; scanIndex >= 0 && nonzeroCount <= 2; scanIndex--)
        {
            coefficientIndex = scan[scanIndex];
            Point position = levels.GetPosition(coefficientIndex);
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelsContext(levels, position, transformSize, transformClass);
            int coefficient = quantized[coefficientIndex];
            if (coefficient == 0)
            {
                accumulatedRate += Av1CoefficientCosts.GetBase(costs, coefficientContext, 0);
                continue;
            }

            magnitude = Math.Abs(coefficient);
            int sign = coefficient < 0 ? -1 : 1;
            int lowerMagnitude = magnitude - 1;
            int dequantizer = coefficientIndex == 0 ? dcDequantizer : acDequantizer;
            int lowerReconstruction = sign * ((lowerMagnitude * dequantizer) >> shift);
            long reconstruction = dequantized[coefficientIndex];
            long twiceOriginal = 2L * original[coefficientIndex];
            long distortion = (reconstruction * (reconstruction - twiceOriginal)) << (2 * shift);
            long lowerDistortion = ((long)lowerReconstruction * (lowerReconstruction - twiceOriginal)) << (2 * shift);
            int rate = GetOptimizationCoefficientRate(
                false,
                coefficientIndex,
                magnitude,
                coefficient < 0 ? 1 : 0,
                coefficientContext,
                context.DcSignContext,
                costs,
                levels,
                transformClass);

            int lowerRate = lowerMagnitude == 0
                ? Av1CoefficientCosts.GetBase(costs, coefficientContext, 0)
                : GetOptimizationCoefficientRate(
                    false,
                    coefficientIndex,
                    lowerMagnitude,
                    coefficient < 0 ? 1 : 0,
                    coefficientContext,
                    context.DcSignContext,
                    costs,
                    levels,
                    transformClass);

            long cost = Av1RateDistortion.GetCost(multiplier, accumulatedRate + rate, accumulatedDistortion + distortion);
            long lowerCost = Av1RateDistortion.GetCost(multiplier, accumulatedRate + lowerRate, accumulatedDistortion + lowerDistortion);
            bool lowerLevel = lowerCost < cost;
            if (lowerLevel)
            {
                cost = lowerCost;
                rate = lowerRate;
                distortion = lowerDistortion;
            }

            ushort newEnd = (ushort)(scanIndex + 1);
            int endContext = Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(levels, scanIndex);
            int endRate = this.GetOptimizationEndOfBlockRate(newEnd, transformSize, componentType, transformClass, costs);
            int newRate = endRate + GetOptimizationCoefficientRate(
                true,
                coefficientIndex,
                magnitude,
                coefficient < 0 ? 1 : 0,
                endContext,
                context.DcSignContext,
                costs,
                levels,
                transformClass);

            long newDistortion = (reconstruction * (reconstruction - twiceOriginal)) << (2 * shift);
            long newCost = Av1RateDistortion.GetCost(multiplier, newRate, newDistortion);
            bool lowerNewEnd = false;
            if (lowerMagnitude > 0)
            {
                int newLowerRate = endRate + GetOptimizationCoefficientRate(
                    true,
                    coefficientIndex,
                    lowerMagnitude,
                    coefficient < 0 ? 1 : 0,
                    endContext,
                    context.DcSignContext,
                    costs,
                    levels,
                    transformClass);

                long newLowerCost = Av1RateDistortion.GetCost(multiplier, newLowerRate, lowerDistortion);
                if (newLowerCost < newCost)
                {
                    lowerNewEnd = true;
                    newCost = newLowerCost;
                    newRate = newLowerRate;
                    newDistortion = lowerDistortion;
                }
            }

            if (newCost < cost)
            {
                // Only the sparse tail is considered for end-position removal. Clearing its retained
                // nonzero entries also updates the forward-neighbor levels consumed by earlier positions.
                for (int i = 0; i < nonzeroCount; i++)
                {
                    int removed = nonzeroIndices[i];
                    Point removedPosition = levels.GetPosition(removed);
                    quantized[removed] = 0;
                    dequantized[removed] = 0;
                    levels.GetRow(removedPosition.Y)[removedPosition.X] = 0;
                }

                endOfBlock = newEnd;
                nonzeroCount = 0;
                accumulatedRate = newRate;
                accumulatedDistortion = newDistortion;
                lowerLevel = lowerNewEnd;
            }
            else
            {
                accumulatedRate += rate;
                accumulatedDistortion += distortion;
            }

            if (lowerLevel)
            {
                quantized[coefficientIndex] = sign * lowerMagnitude;
                dequantized[coefficientIndex] = lowerReconstruction;
                levels.GetRow(position.Y)[position.X] = (byte)Math.Min(lowerMagnitude, sbyte.MaxValue);
            }

            if (quantized[coefficientIndex] != 0)
            {
                nonzeroIndices[nonzeroCount++] = coefficientIndex;
            }
        }

        if (scanIndex == -1 && nonzeroCount <= 2)
        {
            int nonSkipRate = Av1CoefficientCosts.GetSkip(costs, context.SkipContext, 0);
            int skipRate = Av1CoefficientCosts.GetSkip(costs, context.SkipContext, 1);
            if (Av1RateDistortion.GetCost(multiplier, skipRate, 0) <
                Av1RateDistortion.GetCost(multiplier, accumulatedRate + nonSkipRate, accumulatedDistortion))
            {
                for (int i = 0; i < nonzeroCount; i++)
                {
                    quantized[nonzeroIndices[i]] = 0;
                    dequantized[nonzeroIndices[i]] = 0;
                }

                endOfBlock = 0;
            }
        }

        // Once three nonzero coefficients remain, only individual level reductions are considered.
        // A coefficient reconstructed below its original magnitude cannot benefit from a further reduction.
        for (; scanIndex >= 1; scanIndex--)
        {
            coefficientIndex = scan[scanIndex];
            int coefficient = quantized[coefficientIndex];
            Point position = levels.GetPosition(coefficientIndex);
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelsContext(levels, position, transformSize, transformClass);
            if (coefficient == 0)
            {
                accumulatedRate += Av1CoefficientCosts.GetBase(costs, coefficientContext, 0);
                continue;
            }

            magnitude = Math.Abs(coefficient);
            long originalMagnitude = Math.Abs(original[coefficientIndex]);
            long reconstructionMagnitude = Math.Abs(dequantized[coefficientIndex]);
            int rate = Av1CoefficientCosts.GetBase(costs, coefficientContext, Math.Min(magnitude, 3)) +
                Av1ProbabilityCost.GetLiteralCost(1);
            int rateDifference = magnitude <= 3 ? Av1CoefficientCosts.GetBase(costs, coefficientContext, magnitude + 4) : 0;
            if (magnitude > Av1Constants.BaseLevelsCount)
            {
                int rangeContext = Av1SymbolContextHelper.GetBaseRangeContext(levels, position, transformClass);
                int range = Math.Min(magnitude - 3, 12);
                rate += Av1CoefficientCosts.GetRange(costs, rangeContext, range);
                if (magnitude <= 15)
                {
                    rateDifference += Av1CoefficientCosts.GetRange(costs, rangeContext, range + 13);
                }

                // The second half of each cost row stores adjacent-level differences. Beyond the
                // modeled range, reducing a power-of-two residual also removes Golomb suffix bits.
                if (magnitude >= 15)
                {
                    uint remainder = (uint)(magnitude - 14);
                    rate += Av1ProbabilityCost.GetLiteralCost((2 * System.Numerics.BitOperations.Log2(remainder)) + 1);
                    if (System.Numerics.BitOperations.IsPow2(remainder))
                    {
                        rateDifference += Av1ProbabilityCost.GetLiteralCost(remainder == 1 ? 1 : 2);
                    }
                }
            }

            if (reconstructionMagnitude < originalMagnitude)
            {
                accumulatedRate += rate;
                continue;
            }

            int lowerMagnitude = magnitude - 1;
            int lowerRate = rate - rateDifference;
            long lowerReconstruction = ((long)lowerMagnitude * acDequantizer) >> shift;
            long distortion = (reconstructionMagnitude * (reconstructionMagnitude - (2 * originalMagnitude))) << (2 * shift);
            long lowerDistortion = (lowerReconstruction * (lowerReconstruction - (2 * originalMagnitude))) << (2 * shift);
            if (Av1RateDistortion.GetCost(multiplier, lowerRate, lowerDistortion) < Av1RateDistortion.GetCost(multiplier, rate, distortion))
            {
                int sign = coefficient < 0 ? -1 : 1;
                quantized[coefficientIndex] = sign * lowerMagnitude;
                dequantized[coefficientIndex] = (int)(sign * lowerReconstruction);
                levels.GetRow(position.Y)[position.X] = (byte)Math.Min(lowerMagnitude, sbyte.MaxValue);
                accumulatedRate += lowerRate;
            }
            else
            {
                accumulatedRate += rate;
            }
        }

        if (scanIndex == 0)
        {
            ReduceGeneralCoefficient(
                original,
                quantized,
                dequantized,
                levels,
                scan,
                scanIndex,
                endOfBlock,
                transformSize,
                transformClass,
                costs,
                context.DcSignContext,
                dcDequantizer,
                acDequantizer,
                multiplier,
                shift,
                ref accumulatedRate,
                ref accumulatedDistortion);
        }

        return endOfBlock;
    }

    private static void ReduceGeneralCoefficient(
        ReadOnlySpan<int> original,
        Span<int> quantized,
        Span<int> dequantized,
        Av1LevelBuffer levels,
        ReadOnlySpan<short> scan,
        int scanIndex,
        ushort endOfBlock,
        Av1TransformSize transformSize,
        Av1TransformClass transformClass,
        ReadOnlySpan<int> costs,
        int dcSignContext,
        int dcDequantizer,
        int acDequantizer,
        long multiplier,
        int shift,
        ref int accumulatedRate,
        ref long accumulatedDistortion)
    {
        int coefficientIndex = scan[scanIndex];
        int coefficient = quantized[coefficientIndex];
        Point position = levels.GetPosition(coefficientIndex);
        bool last = scanIndex == endOfBlock - 1;
        int coefficientContext = last
            ? Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(levels, scanIndex)
            : Av1SymbolContextHelper.GetLowerLevelsContext(levels, position, transformSize, transformClass);

        if (coefficient == 0)
        {
            accumulatedRate += Av1CoefficientCosts.GetBase(costs, coefficientContext, 0);
            return;
        }

        int magnitude = Math.Abs(coefficient);
        int sign = coefficient < 0 ? -1 : 1;
        int lowerMagnitude = magnitude - 1;
        int dequantizer = coefficientIndex == 0 ? dcDequantizer : acDequantizer;
        int lowerReconstruction = sign * ((lowerMagnitude * dequantizer) >> shift);
        int rate = GetOptimizationCoefficientRate(
            last,
            coefficientIndex,
            magnitude,
            coefficient < 0 ? 1 : 0,
            coefficientContext,
            dcSignContext,
            costs,
            levels,
            transformClass);

        int lowerRate = lowerMagnitude == 0
            ? Av1CoefficientCosts.GetBase(costs, coefficientContext, 0)
            : GetOptimizationCoefficientRate(
                last,
                coefficientIndex,
                lowerMagnitude,
                coefficient < 0 ? 1 : 0,
                coefficientContext,
                dcSignContext,
                costs,
                levels,
                transformClass);

        long reconstruction = dequantized[coefficientIndex];
        long twiceOriginal = 2L * original[coefficientIndex];
        long distortion = (reconstruction * (reconstruction - twiceOriginal)) << (2 * shift);
        long lowerDistortion = ((long)lowerReconstruction * (lowerReconstruction - twiceOriginal)) << (2 * shift);
        if (Av1RateDistortion.GetCost(multiplier, lowerRate, lowerDistortion) < Av1RateDistortion.GetCost(multiplier, rate, distortion))
        {
            quantized[coefficientIndex] = sign * lowerMagnitude;
            dequantized[coefficientIndex] = lowerReconstruction;
            levels.GetRow(position.Y)[position.X] = (byte)Math.Min(lowerMagnitude, sbyte.MaxValue);
            accumulatedRate += lowerRate;
            accumulatedDistortion += lowerDistortion;
        }
        else
        {
            accumulatedRate += rate;
            accumulatedDistortion += distortion;
        }
    }

    private int GetOptimizationEndOfBlockRate(
        ushort endOfBlock,
        Av1TransformSize transformSize,
        Av1ComponentType componentType,
        Av1TransformClass transformClass,
        ReadOnlySpan<int> costs)
    {
        int token = Av1SymbolContextHelper.GetEndOfBlockPosition(endOfBlock, out int extra);
        int rate = this.CoefficientCosts.GetEndOfBlock(
            transformSize.GetLog2Minus4(),
            (int)componentType,
            transformClass == Av1TransformClass.Class2D ? 0 : 1,
            token - 1);

        int suffixBits = Av1SymbolContextHelper.EndOfBlockOffsetBits[token];
        if (suffixBits > 0)
        {
            rate += Av1CoefficientCosts.GetExtra(costs, token - 3, Av1Math.GetBit(extra, suffixBits - 1));
            rate += Av1ProbabilityCost.GetLiteralCost(suffixBits - 1);
        }

        return rate;
    }

    private static int GetOptimizationCoefficientRate(
        bool last,
        int coefficientIndex,
        int magnitude,
        int sign,
        int coefficientContext,
        int dcSignContext,
        ReadOnlySpan<int> costs,
        Av1LevelBuffer levels,
        Av1TransformClass transformClass)
    {
        int rate = last
            ? Av1CoefficientCosts.GetBaseEndOfBlock(costs, coefficientContext, Math.Min(magnitude, 3) - 1)
            : Av1CoefficientCosts.GetBase(costs, coefficientContext, Math.Min(magnitude, 3));

        if (magnitude != 0)
        {
            rate += coefficientIndex == 0
                ? Av1CoefficientCosts.GetSign(costs, dcSignContext, sign)
                : Av1ProbabilityCost.GetLiteralCost(1);

            if (magnitude > Av1Constants.BaseLevelsCount)
            {
                Point position = levels.GetPosition(coefficientIndex);
                int rangeContext = last
                    ? Av1SymbolContextHelper.GetBaseRangeContextEndOfBlock(position, transformClass)
                    : Av1SymbolContextHelper.GetBaseRangeContext(levels, position, transformClass);

                rate += GetBaseRangeCost(magnitude, costs, rangeContext);
            }
        }

        return rate;
    }
}
