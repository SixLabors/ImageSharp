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
    /// <param name="modeCosts">The mode rates that the caller read once.</param>
    /// <param name="coefficientCosts">The coefficient rates that the caller read once.</param>
    /// <param name="coefficients">The quantized coefficients in raster order.</param>
    /// <param name="endOfBlock">The one-based last nonzero scan position.</param>
    /// <param name="transformSize">The transform dimensions.</param>
    /// <param name="transformType">The transform basis and scan order.</param>
    /// <param name="context">The neighboring coefficient context.</param>
    /// <param name="useReducedTransformSet">Whether the frame restricts transform types.</param>
    /// <param name="filterMode">The selected filter-intra mode.</param>
    /// <param name="intraMode">The selected spatial prediction mode.</param>
    /// <param name="isInter">Whether inter transform syntax applies.</param>
    /// <param name="lossless">Whether the segment of the block codes losslessly, which charges no transform type rate.</param>
    /// <returns>The estimated rate in 1/512-bit units.</returns>
    public static int EstimateLumaCoefficientRate(
        Av1ModeCosts modeCosts,
        Av1CoefficientCosts coefficientCosts,
        ReadOnlySpan<int> coefficients,
        ushort endOfBlock,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        Av1TransformBlockContext context,
        bool useReducedTransformSet,
        Av1FilterIntraMode filterMode,
        Av1PredictionMode intraMode,
        bool isInter,
        bool lossless)
    {
        Av1TransformSize sizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);
        Av1CoefficientCosts allCosts = coefficientCosts;
        ReadOnlySpan<int> costs = allCosts.GetPlane((int)sizeContext, (int)Av1ComponentType.Luminance);
        int rate = Av1CoefficientCosts.GetSkip(costs, context.SkipContext, endOfBlock == 0 ? 1 : 0);
        if (endOfBlock == 0)
        {
            return rate;
        }

        rate += GetTransformTypeCost(modeCosts, transformType, transformSize, useReducedTransformSet, lossless, filterMode, intraMode, isInter);

        ReadOnlySpan<int> endOfBlockRates = allCosts.GetEndOfBlockRow(
            transformSize.GetLog2Minus4(), (int)Av1ComponentType.Luminance, transformType.ToClass() == Av1TransformClass.Class2D ? 0 : 1);

        rate += GetOptimizationEndOfBlockRate(endOfBlockRates, endOfBlock, costs);

        // Model each magnitude with its observed Laplacian entropy. The last coefficient is always nonzero, but the scan positions before it
        // include zeros. As a result, the last coefficient uses a separate cost term.
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
    /// Reduces coefficient levels and the coded end position when their combined rate and distortion decrease. It uses the rate tables that the
    /// caller read once for its search loop.
    /// </summary>
    /// <param name="tables">The rate tables and scratch storage, from <see cref="GetCoefficientTables"/>.</param>
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
    /// <param name="coefficientRate">The rate of the refined coefficients and end position.</param>
    /// <returns>The refined end position.</returns>
    public ushort OptimizeCoefficients(
        in Av1CoefficientTables tables,
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
        Av1LevelBuffer levels = this.PrepareCoefficientScratch(tables, width, height, out _);
        Span<byte> levelStorage = tables.LevelStorage;
        if (endOfBlock > 1)
        {
            levels.Initialize(levelStorage, quantized);
        }

        // Each context below reads fixed offsets from one padded index into the level plane, and each accepted
        // reduction writes its new level back through the same index.
        Span<byte> levelPlane = levels.GetActiveLevels(levelStorage);
        int widthLog2 = levels.WidthLog2;
        int levelStride = levels.Stride;
        int coefficientCount = width * height;

        Av1TransformClass transformClass = transformType.ToClass();
        Av1TransformSize sizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);

        // The loops below read the cost tables many times, so they come from the caller.
        Av1CoefficientCosts allCosts = tables.CoefficientCosts;
        ReadOnlySpan<int> costs = allCosts.GetPlane((int)sizeContext, (int)componentType);
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        int shift = transformSize.GetScale();
        int planeWeight = componentType == Av1ComponentType.Luminance ? isInter ? 16 : 17 : useChromaWeights ? isInter ? 10 : 13 : 20;

        // Coefficient errors stay at their native precision. Scale the rate multiplier into that domain once. Each comparison keeps the signed
        // error change relative to a zero coefficient. Sharpness lowers the multiplier, and the image tunes shift it further.
        int sharpness = weights.Sharpness;
        int endOfBlockCutoff = weights.EndOfBlockCutoff;
        long multiplier = (long)rateMultiplier * (8 - sharpness) * (planeWeight << (2 * (bitDepth.GetBitCount() - 8)));
        multiplier = (multiplier + (1L << (weights.RateShift - 1))) >> weights.RateShift;
        ReadOnlySpan<byte> distortionWeights = weights.DistortionWeights;
        ReadOnlySpan<byte> inverseWeights = weights.InverseWeights;

        // Every end position of the block reads the same row of end-of-block token rates.
        ReadOnlySpan<int> endOfBlockRates = allCosts.GetEndOfBlockRow(
            transformSize.GetLog2Minus4(), (int)componentType, transformClass == Av1TransformClass.Class2D ? 0 : 1);

        int accumulatedRate = GetOptimizationEndOfBlockRate(endOfBlockRates, endOfBlock, costs);
        long accumulatedDistortion = 0;

        // The scan, the coefficients and the level plane are sized for this block, so every read below goes through a
        // reference with no range check. A scan entry is a raster index, which is never negative.
        ref short scanBase = ref MemoryMarshal.GetReference(scan);
        ref int originalBase = ref MemoryMarshal.GetReference(original);
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantized);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantized);
        ref byte levelPlaneBase = ref MemoryMarshal.GetReference(levelPlane);
        ref int costBase = ref MemoryMarshal.GetReference(costs);
        int scanIndex = endOfBlock - 1;
        int coefficientIndex = Unsafe.Add(ref scanBase, (nuint)(uint)scanIndex);
        int magnitude = Math.Abs(Unsafe.Add(ref quantizedBase, (nuint)(uint)coefficientIndex));

        // The indices are taken as a span once, outside the loops, rather than through the inline array indexer.
        InlineArray3<int> nonzeroIndexStorage = default;
        Span<int> nonzeroIndices = nonzeroIndexStorage;
        nonzeroIndices[0] = coefficientIndex;
        int nonzeroCount = 1;

        // The last nonzero coefficient cannot disappear in this first step. Instead, a level-one tail starts the later end-position search.
        // That search can remove the tail together with the zeros before it.
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
            // The last coefficient uses the end-of-block context, so its level entry serves only the range rate.
            nuint lastIndex = (nuint)(uint)coefficientIndex;
            ref byte lastLevel = ref Unsafe.Add(ref levelPlaneBase, (nuint)(uint)Av1LevelBuffer.GetPaddedIndex(coefficientIndex, widthLog2));
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(scanIndex, coefficientCount);
            accumulatedRate += GetOptimizationCoefficientRate(
                true,
                coefficientIndex,
                magnitude,
                Unsafe.Add(ref quantizedBase, lastIndex) < 0 ? 1 : 0,
                coefficientContext,
                context.DcSignContext,
                costs,
                ref lastLevel,
                levelStride,
                widthLog2,
                transformClass);

            long lastOriginal = Unsafe.Add(ref originalBase, lastIndex);
            accumulatedDistortion = GetDistortionDifference(
                lastOriginal,
                Unsafe.Add(ref dequantizedBase, lastIndex),
                shift,
                distortionWeights,
                coefficientIndex,
                GetZeroError(lastOriginal, shift, distortionWeights, coefficientIndex));
        }

        scanIndex--;

        // While no more than two nonzero coefficients follow, each coefficient can also become the new last one. The class is a type parameter,
        // so each specialization folds its neighbor and position selection at compile time.
        switch (transformClass)
        {
            case Av1TransformClass.Class2D:
                ReduceEndOfBlockCoefficients<TwoDimensionalClass>(
                    original,
                    quantized,
                    dequantized,
                    levelPlane,
                    widthLog2,
                    levelStride,
                    coefficientCount,
                    scan,
                    ref scanIndex,
                    ref endOfBlock,
                    nonzeroIndices,
                    ref nonzeroCount,
                    transformSize,
                    endOfBlockRates,
                    costs,
                    context.DcSignContext,
                    dcDequantizer,
                    acDequantizer,
                    multiplier,
                    shift,
                    sharpness,
                    endOfBlockCutoff,
                    distortionWeights,
                    inverseWeights,
                    ref accumulatedRate,
                    ref accumulatedDistortion);

                break;
            case Av1TransformClass.ClassHorizontal:
                ReduceEndOfBlockCoefficients<HorizontalClass>(
                    original,
                    quantized,
                    dequantized,
                    levelPlane,
                    widthLog2,
                    levelStride,
                    coefficientCount,
                    scan,
                    ref scanIndex,
                    ref endOfBlock,
                    nonzeroIndices,
                    ref nonzeroCount,
                    transformSize,
                    endOfBlockRates,
                    costs,
                    context.DcSignContext,
                    dcDequantizer,
                    acDequantizer,
                    multiplier,
                    shift,
                    sharpness,
                    endOfBlockCutoff,
                    distortionWeights,
                    inverseWeights,
                    ref accumulatedRate,
                    ref accumulatedDistortion);

                break;
            default:
                ReduceEndOfBlockCoefficients<VerticalClass>(
                    original,
                    quantized,
                    dequantized,
                    levelPlane,
                    widthLog2,
                    levelStride,
                    coefficientCount,
                    scan,
                    ref scanIndex,
                    ref endOfBlock,
                    nonzeroIndices,
                    ref nonzeroCount,
                    transformSize,
                    endOfBlockRates,
                    costs,
                    context.DcSignContext,
                    dcDequantizer,
                    acDequantizer,
                    multiplier,
                    shift,
                    sharpness,
                    endOfBlockCutoff,
                    distortionWeights,
                    inverseWeights,
                    ref accumulatedRate,
                    ref accumulatedDistortion);

                break;
        }

        // When no more than two nonzero coefficients remain, a skip of the whole block can cost less. Sharpness never replaces the block with a skip.
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

        // When three nonzero coefficients remain, the search tries only individual level reductions. The class is a type parameter, so each
        // specialization folds its neighbor and position selection at compile time.
        switch (transformClass)
        {
            case Av1TransformClass.Class2D:
                ReduceSimpleCoefficients<TwoDimensionalClass>(
                    original,
                    quantized,
                    dequantized,
                    levelPlane,
                    widthLog2,
                    levelStride,
                    scan,
                    ref scanIndex,
                    transformSize,
                    costs,
                    acDequantizer,
                    multiplier,
                    shift,
                    sharpness,
                    distortionWeights,
                    inverseWeights,
                    ref accumulatedRate);

                break;
            case Av1TransformClass.ClassHorizontal:
                ReduceSimpleCoefficients<HorizontalClass>(
                    original,
                    quantized,
                    dequantized,
                    levelPlane,
                    widthLog2,
                    levelStride,
                    scan,
                    ref scanIndex,
                    transformSize,
                    costs,
                    acDequantizer,
                    multiplier,
                    shift,
                    sharpness,
                    distortionWeights,
                    inverseWeights,
                    ref accumulatedRate);

                break;
            default:
                ReduceSimpleCoefficients<VerticalClass>(
                    original,
                    quantized,
                    dequantized,
                    levelPlane,
                    widthLog2,
                    levelStride,
                    scan,
                    ref scanIndex,
                    transformSize,
                    costs,
                    acDequantizer,
                    multiplier,
                    shift,
                    sharpness,
                    distortionWeights,
                    inverseWeights,
                    ref accumulatedRate);

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
    /// Tries a lower level and a new end of block at each coefficient from the current scan position down. The loop continues while no more than
    /// two nonzero coefficients follow the current position.
    /// </summary>
    /// <remarks>
    /// The loop is in its own method, specialized for each transform class. As a result, its locals stay in registers and the class tests fold.
    /// </remarks>
    /// <typeparam name="TClass">The transform direction class.</typeparam>
    /// <param name="original">The unquantized transform coefficients.</param>
    /// <param name="quantized">The quantized coefficients, which accepted reductions update.</param>
    /// <param name="dequantized">The dequantized coefficients, which accepted reductions update.</param>
    /// <param name="levelPlane">The padded level plane, which accepted reductions update.</param>
    /// <param name="widthLog2">The base-two logarithm of the coded transform width.</param>
    /// <param name="levelStride">The number of bytes between rows of the level plane.</param>
    /// <param name="coefficientCount">The number of coded coefficients of the transform.</param>
    /// <param name="scan">The scan order of the transform.</param>
    /// <param name="scanIndex">The current scan position, which ends at the first position that the loop did not visit.</param>
    /// <param name="endOfBlock">The end of block, which moves when a coefficient becomes the new last one.</param>
    /// <param name="nonzeroIndices">The raster indices of the nonzero coefficients that follow the current position.</param>
    /// <param name="nonzeroCount">The number of entries in <paramref name="nonzeroIndices"/>.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="endOfBlockRates">The end-of-block token rates of the block.</param>
    /// <param name="costs">The coefficient rates of the transform size and plane.</param>
    /// <param name="dcSignContext">The sign context of the DC coefficient.</param>
    /// <param name="dcDequantizer">The DC dequantizer.</param>
    /// <param name="acDequantizer">The AC dequantizer.</param>
    /// <param name="multiplier">The rate multiplier in the coefficient error domain.</param>
    /// <param name="shift">The transform scale shift.</param>
    /// <param name="sharpness">The loop filter sharpness, which keeps low levels.</param>
    /// <param name="endOfBlockCutoff">The scan position up to which sharpness keeps level-two coefficients.</param>
    /// <param name="distortionWeights">The quantization matrix weights of the distortion, or empty.</param>
    /// <param name="inverseWeights">The inverse quantization matrix weights, or empty.</param>
    /// <param name="accumulatedRate">The rate of the coefficients after the current position.</param>
    /// <param name="accumulatedDistortion">The distortion change of the coefficients after the current position.</param>
    private static void ReduceEndOfBlockCoefficients<TClass>(
        ReadOnlySpan<int> original,
        Span<int> quantized,
        Span<int> dequantized,
        Span<byte> levelPlane,
        int widthLog2,
        int levelStride,
        int coefficientCount,
        ReadOnlySpan<short> scan,
        ref int scanIndex,
        ref ushort endOfBlock,
        Span<int> nonzeroIndices,
        ref int nonzeroCount,
        Av1TransformSize transformSize,
        ReadOnlySpan<int> endOfBlockRates,
        ReadOnlySpan<int> costs,
        int dcSignContext,
        int dcDequantizer,
        int acDequantizer,
        long multiplier,
        int shift,
        int sharpness,
        int endOfBlockCutoff,
        ReadOnlySpan<byte> distortionWeights,
        ReadOnlySpan<byte> inverseWeights,
        ref int accumulatedRate,
        ref long accumulatedDistortion)
        where TClass : struct, ITransformClass
    {
        Av1TransformClass transformClass = TClass.Class;
        ref short scanBase = ref MemoryMarshal.GetReference(scan);
        ref int originalBase = ref MemoryMarshal.GetReference(original);
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantized);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantized);
        ref byte levelPlaneBase = ref MemoryMarshal.GetReference(levelPlane);
        ref int costBase = ref MemoryMarshal.GetReference(costs);
        ref byte offsets = ref MemoryMarshal.GetReference(Av1NzMap.GetContextOffsets(transformSize));
        for (; scanIndex >= 0 && nonzeroCount <= 2; scanIndex--)
        {
            int coefficientIndex = Unsafe.Add(ref scanBase, (nuint)(uint)scanIndex);
            nuint index = (nuint)(uint)coefficientIndex;
            ref byte level = ref Unsafe.Add(ref levelPlaneBase, (nuint)(uint)Av1LevelBuffer.GetPaddedIndex(coefficientIndex, widthLog2));
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelsContext(
                ref level, levelStride, coefficientIndex, widthLog2, ref offsets, transformClass);

            int coefficient = Unsafe.Add(ref quantizedBase, index);
            if (coefficient == 0)
            {
                accumulatedRate += Unsafe.Add(ref costBase, (nuint)(uint)(Av1CoefficientCosts.BaseOffset + (coefficientContext * 8)));
                continue;
            }

            int magnitude = Math.Abs(coefficient);
            int sign = coefficient < 0 ? -1 : 1;
            int lowerMagnitude = magnitude - 1;
            int originalValue = Unsafe.Add(ref originalBase, index);
            long zeroError = GetZeroError(originalValue, shift, distortionWeights, coefficientIndex);
            long distortion = GetDistortionDifference(
                originalValue, Unsafe.Add(ref dequantizedBase, index), shift, distortionWeights, coefficientIndex, zeroError);

            // A level-one coefficient can only drop to zero, which needs no reconstruction and has no distortion change.
            int lowerReconstruction = 0;
            long lowerDistortion = 0;
            if (lowerMagnitude != 0)
            {
                int dequantizer = GetDequantizer(coefficientIndex, dcDequantizer, acDequantizer, inverseWeights);
                lowerReconstruction = sign * ((lowerMagnitude * dequantizer) >> shift);
                lowerDistortion = GetDistortionDifference(originalValue, lowerReconstruction, shift, distortionWeights, coefficientIndex, zeroError);
            }

            int rate = GetOptimizationCoefficientRate(
                false,
                coefficientIndex,
                magnitude,
                coefficient < 0 ? 1 : 0,
                coefficientContext,
                dcSignContext,
                costs,
                ref level,
                levelStride,
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
                    dcSignContext,
                    costs,
                    ref level,
                    levelStride,
                    widthLog2,
                    transformClass);

            long cost = Av1RateDistortion.GetCost(multiplier, accumulatedRate + rate, accumulatedDistortion + distortion);
            long lowerCost = Av1RateDistortion.GetCost(multiplier, accumulatedRate + lowerRate, accumulatedDistortion + lowerDistortion);
            long newDistortion = distortion;

            // Sharpness keeps the low levels of the first coefficients. For a noise pattern, it keeps them further into the scan.
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
            int endRate = GetOptimizationEndOfBlockRate(endOfBlockRates, newEnd, costs);
            int newRate = endRate + GetOptimizationCoefficientRate(
                true,
                coefficientIndex,
                magnitude,
                coefficient < 0 ? 1 : 0,
                endContext,
                dcSignContext,
                costs,
                ref level,
                levelStride,
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
                    dcSignContext,
                    costs,
                    ref level,
                    levelStride,
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
                // Only the sparse tail is a candidate for removal with the end position. The loop clears the nonzero entries of the tail. This also
                // updates the levels that earlier positions read as forward neighbors.
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
                Unsafe.Add(ref quantizedBase, index) = sign * lowerMagnitude;
                Unsafe.Add(ref dequantizedBase, index) = lowerReconstruction;
                level = (byte)Math.Min(lowerMagnitude, sbyte.MaxValue);
            }

            if (Unsafe.Add(ref quantizedBase, index) != 0)
            {
                nonzeroIndices[nonzeroCount++] = coefficientIndex;
            }
        }
    }

    /// <summary>
    /// Reduces individual coefficient levels from the current scan position down to the second coefficient.
    /// </summary>
    /// <remarks>
    /// A coefficient with a reconstruction below its original magnitude gets no benefit from a further reduction. The loop is in its own
    /// method, so its locals stay in registers.
    /// </remarks>
    /// <typeparam name="TClass">The transform direction class.</typeparam>
    /// <param name="original">The unquantized transform coefficients.</param>
    /// <param name="quantized">The quantized coefficients, which accepted reductions update.</param>
    /// <param name="dequantized">The dequantized coefficients, which accepted reductions update.</param>
    /// <param name="levelPlane">The padded level plane, which accepted reductions update.</param>
    /// <param name="widthLog2">The base-two logarithm of the coded transform width.</param>
    /// <param name="levelStride">The number of bytes between rows of the level plane.</param>
    /// <param name="scan">The scan order of the transform.</param>
    /// <param name="scanIndex">The current scan position. On return, it is the first position that the loop did not visit.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="costs">The coefficient rates of the transform size and plane.</param>
    /// <param name="acDequantizer">The AC dequantizer. The loop never visits the DC coefficient.</param>
    /// <param name="multiplier">The rate multiplier in the coefficient error domain.</param>
    /// <param name="shift">The transform scale shift.</param>
    /// <param name="sharpness">The sharpness. When it is not zero, the loop keeps every level-one coefficient.</param>
    /// <param name="distortionWeights">The quantization matrix weights of the distortion, or empty.</param>
    /// <param name="inverseWeights">The inverse quantization matrix weights, or empty.</param>
    /// <param name="accumulatedRate">The rate of the coefficients after the current position.</param>
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

        // A coefficient with a reconstruction below its original magnitude gets no benefit from a further reduction. The scan, coefficient and
        // level spans have the size of this block. Arithmetic on `ref` locals keeps the loop free of range checks. The loop state is in locals,
        // so it stays in registers. The by-reference arguments update once at the end.
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
            // A scan entry is a raster index and a context is never negative, so every offset below is a native-width
            // unsigned value.
            int coefficientIndex = Unsafe.Add(ref scanBase, (nuint)(uint)index);
            nuint position = (nuint)(uint)coefficientIndex;
            int coefficient = Unsafe.Add(ref quantizedBase, position);
            ref byte level = ref Unsafe.Add(ref levelPlaneBase, (nuint)(uint)Av1LevelBuffer.GetPaddedIndex(coefficientIndex, widthLog2));
            int coefficientContext = Av1SymbolContextHelper.GetLowerLevelsContext(
                ref level, levelStride, coefficientIndex, widthLog2, ref offsets, transformClass);

            ref int baseCosts = ref Unsafe.Add(ref costBase, (nuint)(uint)(Av1CoefficientCosts.BaseOffset + (coefficientContext * 8)));

            if (coefficient == 0)
            {
                rateSum += baseCosts;
                continue;
            }

            // Signs are unpredictable, so the magnitudes come from a sign mask without branches.
            int signMask = coefficient >> 31;
            int magnitude = (coefficient ^ signMask) - signMask;
            int originalValue = Unsafe.Add(ref originalBase, position);
            int originalSign = originalValue >> 31;
            long originalMagnitude = (originalValue ^ originalSign) - originalSign;
            int reconstructionValue = Unsafe.Add(ref dequantizedBase, position);
            int reconstructionSign = reconstructionValue >> 31;
            long reconstructionMagnitude = (reconstructionValue ^ reconstructionSign) - reconstructionSign;
            if (magnitude == 1)
            {
                // A level-one coefficient can only drop to zero. Zero has no distortion change and no range or sign rate, so this path needs no
                // dequantizer and no range context. Sharpness keeps every level one.
                int levelOneRate = Unsafe.Add(ref baseCosts, (nuint)1) + Av1ProbabilityCost.GetLiteralCost(1);
                if (reconstructionMagnitude < originalMagnitude || sharpness != 0)
                {
                    rateSum += levelOneRate;
                    continue;
                }

                long levelOneDistortion = distortionWeights.IsEmpty
                    ? (reconstructionMagnitude * (reconstructionMagnitude - (2 * originalMagnitude))) << distortionShift
                    : GetWeightedError(originalMagnitude, reconstructionMagnitude, shift, distortionWeights, coefficientIndex) -
                        GetWeightedError(originalMagnitude, 0, shift, distortionWeights, coefficientIndex);

                // The second half of the base cost row holds the rate saved by coding one level lower.
                int zeroRate = levelOneRate - Unsafe.Add(ref baseCosts, (nuint)5);
                if (Av1RateDistortion.GetCost(multiplier, zeroRate, 0) < Av1RateDistortion.GetCost(multiplier, levelOneRate, levelOneDistortion))
                {
                    Unsafe.Add(ref quantizedBase, position) = 0;
                    Unsafe.Add(ref dequantizedBase, position) = 0;
                    level = 0;
                    rateSum += zeroRate;
                }
                else
                {
                    rateSum += levelOneRate;
                }

                continue;
            }

            int rate = Unsafe.Add(ref baseCosts, (nuint)(uint)Math.Min(magnitude, 3)) + Av1ProbabilityCost.GetLiteralCost(1);
            int rateDifference = magnitude <= 3 ? Unsafe.Add(ref baseCosts, (nuint)(uint)(magnitude + 4)) : 0;

            // From here the level is at least two. Sharpness does not stop a reduction of this level.
            if (magnitude > Av1Constants.BaseLevelsCount)
            {
                int rangeContext = Av1SymbolContextHelper.GetBaseRangeContext(
                    ref level, levelStride, coefficientIndex, widthLog2, transformClass);

                int range = Math.Min(magnitude - 3, 12);
                ref int rangeCosts = ref Unsafe.Add(ref costBase, (nuint)(uint)(Av1CoefficientCosts.RangeOffset + (rangeContext * 26) + range));
                rate += rangeCosts;
                if (magnitude <= 15)
                {
                    rateDifference += Unsafe.Add(ref rangeCosts, (nuint)13);
                }

                // The second half of each cost row stores the rate differences between adjacent levels. Above the modeled range, a reduction of a
                // power-of-two remainder also removes Golomb suffix bits.
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
                // The error of a zero reconstruction is the common reference of both differences, so it is measured once.
                long zeroError = GetWeightedError(originalMagnitude, 0, shift, distortionWeights, coefficientIndex);
                distortion = GetWeightedError(originalMagnitude, reconstructionMagnitude, shift, distortionWeights, coefficientIndex) - zeroError;
                lowerDistortion = GetWeightedError(originalMagnitude, lowerReconstruction, shift, distortionWeights, coefficientIndex) - zeroError;
            }

            if (Av1RateDistortion.GetCost(multiplier, lowerRate, lowerDistortion) < Av1RateDistortion.GetCost(multiplier, rate, distortion))
            {
                Unsafe.Add(ref quantizedBase, position) = (lowerMagnitude ^ signMask) - signMask;
                Unsafe.Add(ref dequantizedBase, position) = ((int)lowerReconstruction ^ signMask) - signMask;
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

    /// <summary>
    /// Tries to lower the level of the coefficient at one scan position by one. It keeps the lower level when the cost decreases. The search
    /// uses this method for the last coefficient and for the DC coefficient.
    /// </summary>
    /// <param name="original">The unquantized transform coefficients.</param>
    /// <param name="quantized">The quantized coefficients, which an accepted reduction updates.</param>
    /// <param name="dequantized">The dequantized coefficients, which an accepted reduction updates.</param>
    /// <param name="levelPlane">The padded level plane, which an accepted reduction updates.</param>
    /// <param name="widthLog2">The base-two logarithm of the coded transform width.</param>
    /// <param name="coefficientCount">The number of coded coefficients of the transform.</param>
    /// <param name="scan">The scan order of the transform.</param>
    /// <param name="scanIndex">The scan position of the coefficient.</param>
    /// <param name="endOfBlock">The end of block. The coefficient at the position before it uses the end-of-block context.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="costs">The coefficient rates of the transform size and plane.</param>
    /// <param name="dcSignContext">The sign context of the DC coefficient.</param>
    /// <param name="dcDequantizer">The DC dequantizer.</param>
    /// <param name="acDequantizer">The AC dequantizer.</param>
    /// <param name="multiplier">The rate multiplier in the coefficient error domain.</param>
    /// <param name="shift">The transform scale shift.</param>
    /// <param name="distortionWeights">The quantization matrix weights of the distortion, or empty.</param>
    /// <param name="inverseWeights">The inverse quantization matrix weights, or empty.</param>
    /// <param name="accumulatedRate">The accumulated rate, to which the method adds the rate of the chosen level.</param>
    /// <param name="accumulatedDistortion">The accumulated distortion change, to which the method adds the change of the chosen level.</param>
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
        int levelStride = (1 << widthLog2) + Av1Constants.TransformPadHorizontal;
        bool last = scanIndex == endOfBlock - 1;
        int coefficientContext = last
            ? Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(scanIndex, coefficientCount)
            : Av1SymbolContextHelper.GetLowerLevelsContext(
                ref level,
                levelStride,
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
            ref level,
            levelStride,
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
                ref level,
                levelStride,
                widthLog2,
                transformClass);

        int originalValue = original[coefficientIndex];
        long zeroError = GetZeroError(originalValue, shift, distortionWeights, coefficientIndex);
        long distortion = GetDistortionDifference(originalValue, dequantized[coefficientIndex], shift, distortionWeights, coefficientIndex, zeroError);
        long lowerDistortion = lowerMagnitude == 0
            ? 0
            : GetDistortionDifference(originalValue, lowerReconstruction, shift, distortionWeights, coefficientIndex, zeroError);

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
    /// Gets the reconstruction step of one coefficient. When an inverse quantization matrix applies, the matrix weights the step.
    /// </summary>
    /// <param name="coefficientIndex">The raster index of the coefficient. Index zero is the DC coefficient.</param>
    /// <param name="dcDequantizer">The DC dequantizer.</param>
    /// <param name="acDequantizer">The AC dequantizer.</param>
    /// <param name="inverseWeights">The inverse quantization matrix weights, or empty.</param>
    /// <returns>The reconstruction step.</returns>
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
    /// Gets the change in squared error from a zero coefficient to a reconstruction. When the QM-PSNR metric applies, its quantization matrix
    /// weights the error.
    /// </summary>
    /// <param name="original">The unquantized coefficient.</param>
    /// <param name="reconstruction">The dequantized coefficient.</param>
    /// <param name="shift">The transform scale shift.</param>
    /// <param name="distortionWeights">The quantization matrix weights of the distortion, or empty.</param>
    /// <param name="coefficientIndex">The raster index of the coefficient.</param>
    /// <param name="zeroError">The weighted error of a zero reconstruction, from <see cref="GetZeroError"/>.</param>
    /// <returns>The error change in the coefficient error domain.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetDistortionDifference(
        long original,
        long reconstruction,
        int shift,
        ReadOnlySpan<byte> distortionWeights,
        int coefficientIndex,
        long zeroError)
        => distortionWeights.IsEmpty
            ? (reconstruction * (reconstruction - (2 * original))) << (2 * shift)
            : GetWeightedError(original, reconstruction, shift, distortionWeights, coefficientIndex) - zeroError;

    /// <summary>
    /// Gets the weighted error of a zero reconstruction. Every difference of one coefficient subtracts this error, so a coefficient with two
    /// candidate levels measures it once. It is zero when no quantization matrix applies.
    /// </summary>
    /// <param name="original">The unquantized coefficient.</param>
    /// <param name="shift">The transform scale shift.</param>
    /// <param name="distortionWeights">The quantization matrix weights of the distortion, or empty.</param>
    /// <param name="coefficientIndex">The raster index of the coefficient.</param>
    /// <returns>The weighted squared error of a zero reconstruction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetZeroError(long original, int shift, ReadOnlySpan<byte> distortionWeights, int coefficientIndex)
        => distortionWeights.IsEmpty ? 0 : GetWeightedError(original, 0, shift, distortionWeights, coefficientIndex);

    /// <summary>
    /// Gets the squared error between a coefficient and a reconstruction. The quantization matrix weights the error, and the result rounds back
    /// to the coefficient error domain.
    /// </summary>
    /// <param name="original">The unquantized coefficient.</param>
    /// <param name="reconstruction">The dequantized coefficient.</param>
    /// <param name="shift">The transform scale shift.</param>
    /// <param name="distortionWeights">The quantization matrix weights of the distortion.</param>
    /// <param name="coefficientIndex">The raster index of the coefficient.</param>
    /// <returns>The weighted squared error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetWeightedError(long original, long reconstruction, int shift, ReadOnlySpan<byte> distortionWeights, int coefficientIndex)
    {
        const int bits = Av1Constants.QuantizationMatrixElementBitCount;
        const long rounding = 1L << ((2 * bits) - 1);
        long difference = ((original - reconstruction) << shift) * distortionWeights[coefficientIndex];
        return ((difference * difference) + rounding) >> (2 * bits);
    }

    /// <summary>
    /// Gets the rate of an end position: its token, its context-coded suffix bit and its literal suffix bits.
    /// </summary>
    /// <param name="endOfBlockRates">The end-of-block token rates of the block, which the caller looks up once.</param>
    /// <param name="endOfBlock">The one-based end position.</param>
    /// <param name="costs">The coefficient rates of the transform size and plane.</param>
    /// <returns>The rate in 1/512-bit units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOptimizationEndOfBlockRate(ReadOnlySpan<int> endOfBlockRates, ushort endOfBlock, ReadOnlySpan<int> costs)
    {
        int token = Av1SymbolContextHelper.GetEndOfBlockPosition(endOfBlock, out int extra);
        int rate = endOfBlockRates[token - 1];
        int suffixBits = Av1SymbolContextHelper.EndOfBlockOffsetBits[token];
        if (suffixBits > 0)
        {
            rate += Av1CoefficientCosts.GetExtra(costs, token - 3, Av1Math.GetBit(extra, suffixBits - 1));
            rate += Av1ProbabilityCost.GetLiteralCost(suffixBits - 1);
        }

        return rate;
    }

    /// <summary>
    /// Gets the rate of one coefficient level: its base symbol, its sign, and its range symbols for a level above two.
    /// </summary>
    /// <param name="last">Whether the coefficient is the last nonzero coefficient, which uses the end-of-block base symbol.</param>
    /// <param name="coefficientIndex">The raster index of the coefficient.</param>
    /// <param name="magnitude">The level of the coefficient.</param>
    /// <param name="sign">One for a negative coefficient, else zero.</param>
    /// <param name="coefficientContext">The base symbol context of the coefficient.</param>
    /// <param name="dcSignContext">The sign context of the DC coefficient.</param>
    /// <param name="costs">The coefficient rates of the transform size and plane.</param>
    /// <param name="level">The entry of the coefficient in the padded level plane, which the caller already holds.</param>
    /// <param name="levelStride">The number of bytes between rows of the level plane.</param>
    /// <param name="widthLog2">The base-two logarithm of the coded transform width.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <returns>The rate in 1/512-bit units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOptimizationCoefficientRate(
        bool last,
        int coefficientIndex,
        int magnitude,
        int sign,
        int coefficientContext,
        int dcSignContext,
        ReadOnlySpan<int> costs,
        ref byte level,
        int levelStride,
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
                    : Av1SymbolContextHelper.GetBaseRangeContext(ref level, levelStride, coefficientIndex, widthLog2, transformClass);

                rate += GetBaseRangeCost(magnitude, costs, rangeContext);
            }
        }

        return rate;
    }

    /// <summary>
    /// Selects the two-dimensional transform class.
    /// </summary>
    private readonly struct TwoDimensionalClass : ITransformClass
    {
        /// <inheritdoc/>
        public static Av1TransformClass Class => Av1TransformClass.Class2D;
    }

    /// <summary>
    /// Selects the horizontal transform class.
    /// </summary>
    private readonly struct HorizontalClass : ITransformClass
    {
        /// <inheritdoc/>
        public static Av1TransformClass Class => Av1TransformClass.ClassHorizontal;
    }

    /// <summary>
    /// Selects the vertical transform class.
    /// </summary>
    private readonly struct VerticalClass : ITransformClass
    {
        /// <inheritdoc/>
        public static Av1TransformClass Class => Av1TransformClass.ClassVertical;
    }
}
