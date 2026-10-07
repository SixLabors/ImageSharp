// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <content>
/// Refines quantized coefficients using their entropy rate and reconstruction error.
/// </content>
internal sealed partial class Av1SymbolEncoder
{
    /// <summary>
    /// Carries a transform class as a type so that the class-specific context arithmetic folds at compile time.
    /// </summary>
    private interface ITransformClass
    {
        /// <summary>
        /// Gets the transform class.
        /// </summary>
        public static abstract Av1TransformClass Class { get; }
    }

    /// <summary>
    /// Estimates luma coefficient rates from quantized magnitudes and the transform's entropy context.
    /// </summary>
    /// <param name="coefficients">The quantized coefficients in raster order.</param>
    /// <param name="endOfBlock">The one-based last nonzero scan position.</param>
    /// <param name="transformSize">The transform dimensions.</param>
    /// <param name="transformType">The transform basis and scan order.</param>
    /// <param name="context">The neighboring coefficient context.</param>
    /// <param name="useReducedTransformSet">Whether the frame restricts transform types.</param>
    /// <param name="filterMode">The selected filter-intra mode.</param>
    /// <param name="intraMode">The selected spatial prediction mode.</param>
    /// <param name="isInter">Whether inter transform syntax applies.</param>
    /// <returns>The estimated rate in 1/512-bit units.</returns>
    public int EstimateLumaCoefficientRate(
        ReadOnlySpan<int> coefficients,
        ushort endOfBlock,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        Av1TransformBlockContext context,
        bool useReducedTransformSet,
        Av1FilterIntraMode filterMode,
        Av1PredictionMode intraMode,
        bool isInter)
    {
        Av1TransformSize sizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);
        Av1CoefficientCosts allCosts = this.CoefficientCosts;
        ReadOnlySpan<int> costs = allCosts.GetPlane((int)sizeContext, (int)Av1ComponentType.Luminance);
        int rate = Av1CoefficientCosts.GetSkip(costs, context.SkipContext, endOfBlock == 0 ? 1 : 0);
        if (endOfBlock == 0)
        {
            return rate;
        }

        rate += this.GetTransformTypeCost(
            transformType, transformSize, useReducedTransformSet, this.baseQIndex, filterMode, intraMode, isInter);

        rate += GetOptimizationEndOfBlockRate(
            allCosts, endOfBlock, transformSize, Av1ComponentType.Luminance, transformType.ToClass(), costs);

        // Model each magnitude with its observed Laplacian entropy. The last coefficient is known
        // nonzero, while preceding scan positions include zeros; its cost therefore uses a separate term.
        ReadOnlySpan<int> magnitudeCosts = [-1143, 53, 545, 825, 1031, 1209, 1393, 1577, 1763, 1947, 2132, 2317, 2501, 2686, 2871];
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        rate += (Math.Abs(coefficients[scan[endOfBlock - 1]]) - 1) << 11;
        for (int index = endOfBlock - 2; index >= 0; index--)
        {
            rate += magnitudeCosts[Math.Min(Math.Abs(coefficients[scan[index]]), magnitudeCosts.Length - 1)];
        }

        const int coefficientConstant = 512;
        const int log2E = ((14427 * 512) + 5000) / 10000;
        return rate + ((coefficientConstant + log2E) * (endOfBlock - 1));
    }

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
    /// <param name="weights">The sharpness, rate shift and quantization matrices of the trellis.</param>
    /// <param name="coefficientRate">
    /// The rate of the refined coefficients and end position, excluding the skip flag and the transform type.
    /// It is the rate that <c>av1_optimize_txb</c> accumulates, so the caller needs no second cost pass.
    /// </param>
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
        ushort endOfBlock,
        in Av1CoefficientOptimizationWeights weights,
        out int coefficientRate)
    {
        long workStart = Av1WorkCounters.Start();
        ushort workResult = this.OptimizeCoefficientsCore(original, quantized, dequantized, transformSize, transformType, componentType, context, dcDequantizer, acDequantizer, rateMultiplier, bitDepth, isInter, useChromaWeights, endOfBlock, in weights, out coefficientRate);
        Av1WorkCounters.Stop(Av1WorkCounters.OptimizeB, workStart);
        return workResult;
    }

    public ushort OptimizeCoefficientsCore(
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
        ushort endOfBlock,
        in Av1CoefficientOptimizationWeights weights,
        out int coefficientRate)
    {
        Av1TransformSize adjusted = transformSize.GetAdjusted();
        int width = adjusted.GetWidth();
        int height = adjusted.GetHeight();
        Av1LevelBuffer levels = this.PrepareCoefficientScratch(width, height, out _);
        if (endOfBlock > 1)
        {
            levels.Initialize(quantized);
        }

        // Resolve the level plane once. Each context below reads fixed offsets from one padded index,
        // and each accepted reduction writes its new level back through the same index.
        Span<byte> levelPlane = levels.GetActiveLevels();
        int widthLog2 = levels.WidthLog2;
        int levelStride = levels.Stride;
        int coefficientCount = width * height;

        Av1TransformClass transformClass = transformType.ToClass();
        Av1TransformSize sizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);

        // The cost workspace resolves its memory once for the block; the loops below read it many times.
        Av1CoefficientCosts allCosts = this.CoefficientCosts;
        ReadOnlySpan<int> costs = allCosts.GetPlane((int)sizeContext, (int)componentType);
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        int shift = transformSize.GetScale();
        int planeWeight = componentType == Av1ComponentType.Luminance ? isInter ? 16 : 17 : useChromaWeights ? isInter ? 10 : 13 : 20;

        // Coefficient errors remain at their native precision. Scale the rate multiplier into that
        // domain once; each comparison retains the signed error change relative to a zero coefficient.
        // Sharpness lowers the multiplier, and the image tunes shift it further. Reference: av1_optimize_txb().
        int sharpness = weights.Sharpness;
        int endOfBlockCutoff = weights.EndOfBlockCutoff;
        long multiplier = (long)rateMultiplier * (8 - sharpness) * (planeWeight << (2 * (bitDepth.GetBitCount() - 8)));
        multiplier = (multiplier + (1L << (weights.RateShift - 1))) >> weights.RateShift;
        ReadOnlySpan<byte> distortionWeights = weights.DistortionWeights;
        ReadOnlySpan<byte> inverseWeights = weights.InverseWeights;
        int accumulatedRate = GetOptimizationEndOfBlockRate(allCosts, endOfBlock, transformSize, componentType, transformClass, costs);
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
                levelPlane,
                widthLog2,
                coefficientCount,
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
                distortionWeights,
                inverseWeights,
                ref accumulatedRate,
                ref accumulatedDistortion);
        }
        else
        {
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(scanIndex, coefficientCount);
            accumulatedRate += GetOptimizationCoefficientRate(
                true,
                coefficientIndex,
                magnitude,
                quantized[coefficientIndex] < 0 ? 1 : 0,
                coefficientContext,
                context.DcSignContext,
                costs,
                levelPlane,
                widthLog2,
                transformClass);

            accumulatedDistortion = GetDistortionDifference(
                original[coefficientIndex], dequantized[coefficientIndex], shift, distortionWeights, coefficientIndex);
        }

        scanIndex--;
        ref byte offsets = ref MemoryMarshal.GetReference(Av1NzMap.GetContextOffsets(transformSize));
        ref short scanBase = ref MemoryMarshal.GetReference(scan);
        ref byte levelPlaneBase = ref MemoryMarshal.GetReference(levelPlane);
        ref int costBase = ref MemoryMarshal.GetReference(costs);
        for (; scanIndex >= 0 && nonzeroCount <= 2; scanIndex--)
        {
            coefficientIndex = Unsafe.Add(ref scanBase, scanIndex);
            ref byte level = ref Unsafe.Add(ref levelPlaneBase, Av1LevelBuffer.GetPaddedIndex(coefficientIndex, widthLog2));
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelsContext(
                ref level, levelStride, coefficientIndex, widthLog2, ref offsets, transformClass);

            int coefficient = quantized[coefficientIndex];
            if (coefficient == 0)
            {
                accumulatedRate += Unsafe.Add(ref costBase, Av1CoefficientCosts.BaseOffset + (coefficientContext * 8));
                continue;
            }

            magnitude = Math.Abs(coefficient);
            int sign = coefficient < 0 ? -1 : 1;
            int lowerMagnitude = magnitude - 1;
            int dequantizer = GetDequantizer(coefficientIndex, dcDequantizer, acDequantizer, inverseWeights);
            int lowerReconstruction = sign * ((lowerMagnitude * dequantizer) >> shift);
            int originalValue = original[coefficientIndex];
            long distortion = GetDistortionDifference(originalValue, dequantized[coefficientIndex], shift, distortionWeights, coefficientIndex);
            long lowerDistortion = lowerMagnitude == 0
                ? 0
                : GetDistortionDifference(originalValue, lowerReconstruction, shift, distortionWeights, coefficientIndex);

            int rate = GetOptimizationCoefficientRate(
                false,
                coefficientIndex,
                magnitude,
                coefficient < 0 ? 1 : 0,
                coefficientContext,
                context.DcSignContext,
                costs,
                levelPlane,
                widthLog2,
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
                    levelPlane,
                    widthLog2,
                    transformClass);

            long cost = Av1RateDistortion.GetCost(multiplier, accumulatedRate + rate, accumulatedDistortion + distortion);
            long lowerCost = Av1RateDistortion.GetCost(multiplier, accumulatedRate + lowerRate, accumulatedDistortion + lowerDistortion);
            long newDistortion = distortion;

            // Sharpness keeps the low levels of the first coefficients. For a noise pattern, it keeps them further into the scan.
            // Reference: min_eob_cutoff and qc_threshold in update_coeff_eob().
            bool allowLower = sharpness == 0 || magnitude > (scanIndex <= endOfBlockCutoff ? 2 : 1);
            bool lowerLevel = allowLower && lowerCost < cost;
            if (lowerLevel)
            {
                cost = lowerCost;
                rate = lowerRate;
                distortion = lowerDistortion;
            }

            ushort newEnd = (ushort)(scanIndex + 1);
            int endContext = Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(scanIndex, coefficientCount);
            int endRate = GetOptimizationEndOfBlockRate(allCosts, newEnd, transformSize, componentType, transformClass, costs);
            int newRate = endRate + GetOptimizationCoefficientRate(
                true,
                coefficientIndex,
                magnitude,
                coefficient < 0 ? 1 : 0,
                endContext,
                context.DcSignContext,
                costs,
                levelPlane,
                widthLog2,
                transformClass);

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
                    levelPlane,
                    widthLog2,
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

            if ((sharpness == 0 || newEnd >= endOfBlockCutoff) && newCost < cost)
            {
                // Only the sparse tail is considered for end-position removal. Clearing its retained
                // nonzero entries also updates the forward-neighbor levels consumed by earlier positions.
                for (int i = 0; i < nonzeroCount; i++)
                {
                    int removed = nonzeroIndices[i];
                    quantized[removed] = 0;
                    dequantized[removed] = 0;
                    levelPlane[Av1LevelBuffer.GetPaddedIndex(removed, widthLog2)] = 0;
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
                level = (byte)Math.Min(lowerMagnitude, sbyte.MaxValue);
            }

            if (quantized[coefficientIndex] != 0)
            {
                nonzeroIndices[nonzeroCount++] = coefficientIndex;
            }
        }

        // Sharpness never replaces the block with a skip. Reference: the update_skip() call of av1_optimize_txb().
        if (scanIndex == -1 && nonzeroCount <= 2 && sharpness == 0)
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

        // Once three nonzero coefficients remain, only individual level reductions are considered. The class
        // is a type parameter so that each specialization folds its neighbor and position selection.
        switch (transformClass)
        {
            case Av1TransformClass.Class2D:
                ReduceSimpleCoefficients<TwoDimensionalClass>(
                    original, quantized, dequantized, levelPlane, widthLog2, levelStride, scan, ref scanIndex, transformSize, costs, acDequantizer, multiplier, shift, sharpness, distortionWeights, inverseWeights, ref accumulatedRate);

                break;
            case Av1TransformClass.ClassHorizontal:
                ReduceSimpleCoefficients<HorizontalClass>(
                    original, quantized, dequantized, levelPlane, widthLog2, levelStride, scan, ref scanIndex, transformSize, costs, acDequantizer, multiplier, shift, sharpness, distortionWeights, inverseWeights, ref accumulatedRate);

                break;
            default:
                ReduceSimpleCoefficients<VerticalClass>(
                    original, quantized, dequantized, levelPlane, widthLog2, levelStride, scan, ref scanIndex, transformSize, costs, acDequantizer, multiplier, shift, sharpness, distortionWeights, inverseWeights, ref accumulatedRate);

                break;
        }

        if (scanIndex == 0)
        {
            ReduceGeneralCoefficient(
                original,
                quantized,
                dequantized,
                levelPlane,
                widthLog2,
                coefficientCount,
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
                distortionWeights,
                inverseWeights,
                ref accumulatedRate,
                ref accumulatedDistortion);
        }

        coefficientRate = endOfBlock == 0 ? 0 : accumulatedRate;
        return endOfBlock;
    }

    /// <summary>
    /// Reduces individual coefficient levels from the current scan position down to the second coefficient.
    /// </summary>
    /// <remarks>
    /// This is <c>update_coeff_simple</c>. A coefficient reconstructed below its original magnitude cannot
    /// benefit from a further reduction. The loop lives in its own method so that its locals stay in registers.
    /// </remarks>
    private static void ReduceSimpleCoefficients<TClass>(
        ReadOnlySpan<int> original,
        Span<int> quantized,
        Span<int> dequantized,
        Span<byte> levelPlane,
        int widthLog2,
        int levelStride,
        ReadOnlySpan<short> scan,
        ref int scanIndex,
        Av1TransformSize transformSize,
        ReadOnlySpan<int> costs,
        int acDequantizer,
        long multiplier,
        int shift,
        int sharpness,
        ReadOnlySpan<byte> distortionWeights,
        ReadOnlySpan<byte> inverseWeights,
        ref int accumulatedRate)
        where TClass : struct, ITransformClass
    {
        Av1TransformClass transformClass = TClass.Class;
        ref byte offsets = ref MemoryMarshal.GetReference(Av1NzMap.GetContextOffsets(transformSize));

        // A coefficient reconstructed below its original magnitude cannot benefit from a further reduction.
        // The scan, coefficient, and level spans are sized for this block; reference arithmetic keeps the
        // per-coefficient loop free of range checks, as update_coeff_simple is. The loop state lives in
        // locals so that it stays in registers; the by-reference arguments update once at the end.
        ref short scanBase = ref MemoryMarshal.GetReference(scan);
        ref int originalBase = ref MemoryMarshal.GetReference(original);
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantized);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantized);
        ref byte levelPlaneBase = ref MemoryMarshal.GetReference(levelPlane);
        ref int costBase = ref MemoryMarshal.GetReference(costs);
        int index = scanIndex;
        int rateSum = accumulatedRate;
        int distortionShift = 2 * shift;
        for (; index >= 1; index--)
        {
            int coefficientIndex = Unsafe.Add(ref scanBase, index);
            int coefficient = Unsafe.Add(ref quantizedBase, coefficientIndex);
            ref byte level = ref Unsafe.Add(ref levelPlaneBase, Av1LevelBuffer.GetPaddedIndex(coefficientIndex, widthLog2));
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelsContext(
                ref level, levelStride, coefficientIndex, widthLog2, ref offsets, transformClass);

            ref int baseCosts = ref Unsafe.Add(ref costBase, Av1CoefficientCosts.BaseOffset + (coefficientContext * 8));

            if (coefficient == 0)
            {
                rateSum += baseCosts;
                continue;
            }

            // Signs are unpredictable, so the magnitudes form without branches, as the compiled abs() of the reference does.
            int signMask = coefficient >> 31;
            int magnitude = (coefficient ^ signMask) - signMask;
            int originalValue = Unsafe.Add(ref originalBase, coefficientIndex);
            int originalSign = originalValue >> 31;
            long originalMagnitude = (originalValue ^ originalSign) - originalSign;
            int reconstructionValue = Unsafe.Add(ref dequantizedBase, coefficientIndex);
            int reconstructionSign = reconstructionValue >> 31;
            long reconstructionMagnitude = (reconstructionValue ^ reconstructionSign) - reconstructionSign;
            int rate = Unsafe.Add(ref baseCosts, Math.Min(magnitude, 3)) + Av1ProbabilityCost.GetLiteralCost(1);
            int rateDifference = magnitude <= 3 ? Unsafe.Add(ref baseCosts, magnitude + 4) : 0;
            if (magnitude > Av1Constants.BaseLevelsCount)
            {
                int rangeContext = Av1SymbolContextHelper.GetBaseRangeContext(
                    ref level, levelStride, coefficientIndex, widthLog2, transformClass);

                int range = Math.Min(magnitude - 3, 12);
                ref int rangeCosts = ref Unsafe.Add(ref costBase, Av1CoefficientCosts.RangeOffset + (rangeContext * 26) + range);
                rate += rangeCosts;
                if (magnitude <= 15)
                {
                    rateDifference += Unsafe.Add(ref rangeCosts, 13);
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

            // Sharpness keeps every level-one coefficient. Reference: the allow_lower_qc test of update_coeff_simple().
            if (reconstructionMagnitude < originalMagnitude || (sharpness != 0 && magnitude == 1))
            {
                rateSum += rate;
                continue;
            }

            int lowerMagnitude = magnitude - 1;
            int lowerRate = rate - rateDifference;
            int dequantizer = GetDequantizer(coefficientIndex, acDequantizer, acDequantizer, inverseWeights);
            long lowerReconstruction = ((long)lowerMagnitude * dequantizer) >> shift;
            long distortion;
            long lowerDistortion;
            if (distortionWeights.IsEmpty)
            {
                distortion = (reconstructionMagnitude * (reconstructionMagnitude - (2 * originalMagnitude))) << distortionShift;
                lowerDistortion = (lowerReconstruction * (lowerReconstruction - (2 * originalMagnitude))) << distortionShift;
            }
            else
            {
                distortion = GetDistortionDifference(originalMagnitude, reconstructionMagnitude, shift, distortionWeights, coefficientIndex);
                lowerDistortion = GetDistortionDifference(originalMagnitude, lowerReconstruction, shift, distortionWeights, coefficientIndex);
            }

            if (Av1RateDistortion.GetCost(multiplier, lowerRate, lowerDistortion) < Av1RateDistortion.GetCost(multiplier, rate, distortion))
            {
                Unsafe.Add(ref quantizedBase, coefficientIndex) = (lowerMagnitude ^ signMask) - signMask;
                Unsafe.Add(ref dequantizedBase, coefficientIndex) = ((int)lowerReconstruction ^ signMask) - signMask;
                level = (byte)Math.Min(lowerMagnitude, sbyte.MaxValue);
                rateSum += lowerRate;
            }
            else
            {
                rateSum += rate;
            }
        }

        scanIndex = index;
        accumulatedRate = rateSum;
    }

    private static void ReduceGeneralCoefficient(
        ReadOnlySpan<int> original,
        Span<int> quantized,
        Span<int> dequantized,
        Span<byte> levelPlane,
        int widthLog2,
        int coefficientCount,
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
        ReadOnlySpan<byte> distortionWeights,
        ReadOnlySpan<byte> inverseWeights,
        ref int accumulatedRate,
        ref long accumulatedDistortion)
    {
        int coefficientIndex = scan[scanIndex];
        int coefficient = quantized[coefficientIndex];
        ref byte level = ref levelPlane[Av1LevelBuffer.GetPaddedIndex(coefficientIndex, widthLog2)];
        bool last = scanIndex == endOfBlock - 1;
        int coefficientContext = last
            ? Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(scanIndex, coefficientCount)
            : Av1SymbolContextHelper.GetLowerLevelsContext(
                ref level,
                (1 << widthLog2) + Av1Constants.TransformPadHorizontal,
                coefficientIndex,
                widthLog2,
                transformSize,
                transformClass);

        if (coefficient == 0)
        {
            accumulatedRate += Av1CoefficientCosts.GetBase(costs, coefficientContext, 0);
            return;
        }

        int magnitude = Math.Abs(coefficient);
        int sign = coefficient < 0 ? -1 : 1;
        int lowerMagnitude = magnitude - 1;
        int dequantizer = GetDequantizer(coefficientIndex, dcDequantizer, acDequantizer, inverseWeights);
        int lowerReconstruction = sign * ((lowerMagnitude * dequantizer) >> shift);
        int rate = GetOptimizationCoefficientRate(
            last,
            coefficientIndex,
            magnitude,
            coefficient < 0 ? 1 : 0,
            coefficientContext,
            dcSignContext,
            costs,
            levelPlane,
            widthLog2,
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
                levelPlane,
                widthLog2,
                transformClass);

        int originalValue = original[coefficientIndex];
        long distortion = GetDistortionDifference(originalValue, dequantized[coefficientIndex], shift, distortionWeights, coefficientIndex);
        long lowerDistortion = lowerMagnitude == 0
            ? 0
            : GetDistortionDifference(originalValue, lowerReconstruction, shift, distortionWeights, coefficientIndex);

        if (Av1RateDistortion.GetCost(multiplier, lowerRate, lowerDistortion) < Av1RateDistortion.GetCost(multiplier, rate, distortion))
        {
            quantized[coefficientIndex] = sign * lowerMagnitude;
            dequantized[coefficientIndex] = lowerReconstruction;
            level = (byte)Math.Min(lowerMagnitude, sbyte.MaxValue);
            accumulatedRate += lowerRate;
            accumulatedDistortion += lowerDistortion;
        }
        else
        {
            accumulatedRate += rate;
            accumulatedDistortion += distortion;
        }
    }

    /// <summary>
    /// Gets the reconstruction step of one coefficient, weighted by the inverse quantization matrix when one applies.
    /// Reference: get_dqv().
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetDequantizer(int coefficientIndex, int dcDequantizer, int acDequantizer, ReadOnlySpan<byte> inverseWeights)
    {
        int dequantizer = coefficientIndex == 0 ? dcDequantizer : acDequantizer;
        if (!inverseWeights.IsEmpty)
        {
            const int bits = Av1Constants.QuantizationMatrixElementBitCount;
            dequantizer = ((inverseWeights[coefficientIndex] * dequantizer) + (1 << (bits - 1))) >> bits;
        }

        return dequantizer;
    }

    /// <summary>
    /// Gets the change in squared error from a zero coefficient to a reconstruction, weighted by the quantization
    /// matrix of the QM-PSNR metric when one applies. Reference: get_coeff_dist().
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetDistortionDifference(long original, long reconstruction, int shift, ReadOnlySpan<byte> distortionWeights, int coefficientIndex)
    {
        if (distortionWeights.IsEmpty)
        {
            return (reconstruction * (reconstruction - (2 * original))) << (2 * shift);
        }

        const int bits = Av1Constants.QuantizationMatrixElementBitCount;
        const long rounding = 1L << ((2 * bits) - 1);
        long weight = distortionWeights[coefficientIndex];
        long difference = ((original - reconstruction) << shift) * weight;
        long zero = (original << shift) * weight;
        return (((difference * difference) + rounding) >> (2 * bits)) - (((zero * zero) + rounding) >> (2 * bits));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOptimizationEndOfBlockRate(
        Av1CoefficientCosts allCosts,
        ushort endOfBlock,
        Av1TransformSize transformSize,
        Av1ComponentType componentType,
        Av1TransformClass transformClass,
        ReadOnlySpan<int> costs)
    {
        int token = Av1SymbolContextHelper.GetEndOfBlockPosition(endOfBlock, out int extra);
        int rate = allCosts.GetEndOfBlock(
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOptimizationCoefficientRate(
        bool last,
        int coefficientIndex,
        int magnitude,
        int sign,
        int coefficientContext,
        int dcSignContext,
        ReadOnlySpan<int> costs,
        Span<byte> levelPlane,
        int widthLog2,
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
                int rangeContext = last
                    ? Av1SymbolContextHelper.GetBaseRangeContextEndOfBlock(coefficientIndex, widthLog2, transformClass)
                    : Av1SymbolContextHelper.GetBaseRangeContext(
                        ref levelPlane[Av1LevelBuffer.GetPaddedIndex(coefficientIndex, widthLog2)],
                        (1 << widthLog2) + Av1Constants.TransformPadHorizontal,
                        coefficientIndex,
                        widthLog2,
                        transformClass);

                rate += GetBaseRangeCost(magnitude, costs, rangeContext);
            }
        }

        return rate;
    }

    private readonly struct TwoDimensionalClass : ITransformClass
    {
        /// <inheritdoc/>
        public static Av1TransformClass Class => Av1TransformClass.Class2D;
    }

    private readonly struct HorizontalClass : ITransformClass
    {
        /// <inheritdoc/>
        public static Av1TransformClass Class => Av1TransformClass.ClassHorizontal;
    }

    private readonly struct VerticalClass : ITransformClass
    {
        /// <inheritdoc/>
        public static Av1TransformClass Class => Av1TransformClass.ClassVertical;
    }
}
