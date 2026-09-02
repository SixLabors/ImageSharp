// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1EntropyTests
{
    private const int BaseQIndex = 23;

    [Fact]
    public void ProbabilityCostTableMatchesDefinition()
    {
        for (int normalizedProbability = 128; normalizedProbability < 256; normalizedProbability++)
        {
            int expected = (int)Math.Round(
                -Math.Log2(normalizedProbability / 256D) * (1 << Av1ProbabilityCost.CostShift),
                MidpointRounding.AwayFromZero);

            int actual = Av1ProbabilityCost.GetProbabilityCost(normalizedProbability << 7);

            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData(0, 7680)]
    [InlineData(1, 7680)]
    [InlineData(4, 6656)]
    [InlineData(8192, 1024)]
    [InlineData(10000, 878)]
    [InlineData(16384, 512)]
    [InlineData(24576, 212)]
    [InlineData(32767, 3)]
    [InlineData(32768, 3)]
    public void ProbabilityCostMatchesCurrentLibaom(int probability, int expected)
        => Assert.Equal(expected, Av1ProbabilityCost.GetProbabilityCost(probability));

    [Fact]
    public void SymbolCostUsesDistributionIntervals()
    {
        Av1Distribution distribution = new(8192, 24576);

        Assert.Equal(1024, Av1ProbabilityCost.GetSymbolCost(distribution, 0));
        Assert.Equal(512, Av1ProbabilityCost.GetSymbolCost(distribution, 1));
        Assert.Equal(1024, Av1ProbabilityCost.GetSymbolCost(distribution, 2));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 512)]
    [InlineData(7, 3584)]
    public void LiteralCostUsesProbabilityCostPrecision(int bitCount, int expected)
        => Assert.Equal(expected, Av1ProbabilityCost.GetLiteralCost(bitCount));

    [Fact]
    public void ProbabilityCostDoesNotAllocate()
    {
        Av1Distribution distribution = new(8192, 24576);
        _ = Av1ProbabilityCost.GetSymbolCost(distribution, 0);
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 1000; i++)
        {
            _ = Av1ProbabilityCost.GetSymbolCost(distribution, i % distribution.NumberOfSymbols);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    [Fact]
    public void SymbolEncoderSyntaxCostsMatchCurrentDistributions()
    {
        const byte TopContext = 0;
        const byte LeftContext = 0;
        const int SkipContext = 0;
        const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
        const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;
        const Av1PredictionMode LumaMode = Av1PredictionMode.DC;
        const Av1ChromaPredictionMode ChromaMode = Av1ChromaPredictionMode.DC;
        const Av1FilterIntraMode FilterMode = Av1FilterIntraMode.DC;

        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, BaseQIndex, updateCdf: false);

        Av1Distribution luma = Av1DefaultDistributions.KeyFrameYMode[TopContext][LeftContext];
        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(luma, (int)LumaMode),
            encoder.GetLumaModeCost(LumaMode, TopContext, LeftContext));

        Av1Distribution angle = Av1DefaultDistributions.AngleDelta[0];
        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(angle, Av1Constants.MaxAngleDelta),
            encoder.GetAngleDeltaCost(Av1Constants.MaxAngleDelta, Av1PredictionMode.Vertical));

        Av1Distribution chroma = Av1DefaultDistributions.UvMode[0][(int)LumaMode];
        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(chroma, (int)ChromaMode),
            encoder.GetChromaModeCost(ChromaMode, false, LumaMode));

        Av1Distribution filterEnable = Av1DefaultDistributions.FilterIntra[(int)BlockSize];
        Av1Distribution filterMode = Av1DefaultDistributions.FilterIntraMode;
        int expectedFilterCost = Av1ProbabilityCost.GetSymbolCost(filterEnable, 1)
            + Av1ProbabilityCost.GetSymbolCost(filterMode, (int)FilterMode);

        Assert.Equal(expectedFilterCost, encoder.GetFilterIntraModeCost(FilterMode, BlockSize));
        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(filterEnable, 0),
            encoder.GetFilterIntraModeCost(Av1FilterIntraMode.AllFilterIntraModes, BlockSize));

        Av1Distribution skip = Av1DefaultDistributions.Skip[SkipContext];
        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(skip, 1),
            encoder.GetSkipCost(true, SkipContext));

        Av1Distribution transformSize = Av1DefaultDistributions.TransformSize[0][SkipContext];
        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(transformSize, 0),
            encoder.GetTransformSizeCost(BlockSize, TransformSize, SkipContext));

        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(transformSize, 1),
            encoder.GetTransformSizeCost(BlockSize, Av1TransformSize.Size4x4, SkipContext));

        Av1Distribution transformSkip = Av1DefaultDistributions
            .GetTransformBlockSkip(BaseQIndex)[(int)TransformSize][SkipContext];

        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(transformSkip, 1),
            encoder.GetTransformBlockSkipCost(true, TransformSize, SkipContext));
    }

    [Fact]
    public void BlockSkipDecisionUsesAdaptedRatesForEmptyTransforms()
    {
        const int QIndex = 73;
        const int BlockSkipContext = 2;
        const int TransformSkipContext = 0;
        const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;
        using Av1SymbolEncoder encoder = new(Configuration.Default, 256, QIndex);
        int emptyTransformRate = encoder.GetTransformBlockSkipCost(
            true,
            TransformSize,
            TransformSkipContext);

        Assert.True(
            Av1TileWriter.ShouldSkipCoefficients(
                encoder,
                BlockSkipContext,
                emptyTransformRate));

        // Repeated non-skip symbols make another block skip more expensive while the empty-transform
        // rate remains unchanged, proving the decision reads the adapted live distribution.
        for (int index = 0; index < 256; index++)
        {
            encoder.WriteSkip(false, BlockSkipContext);
        }

        Assert.False(
            Av1TileWriter.ShouldSkipCoefficients(
                encoder,
                BlockSkipContext,
                emptyTransformRate));
    }

    [Theory]
    [InlineData(-16, 16)]
    [InlineData(0, 8)]
    [InlineData(-4, 0)]
    public void ChromaFromLumaCostMatchesCurrentDistributions(int alphaU, int alphaV)
    {
        int signU = Av1ChromaFromLumaMath.AlphaToSign(alphaU);
        int signV = Av1ChromaFromLumaMath.AlphaToSign(alphaV);
        int jointSign = Av1ChromaFromLumaMath.JointSign(signU, signV);
        int indexU = Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(alphaU);
        int indexV = Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(alphaV);
        int packedIndex = Av1ChromaFromLumaMath.PackIndices(indexU, indexV);
        int expected = Av1ProbabilityCost.GetSymbolCost(Av1DefaultDistributions.ChromaFromLumaSign, jointSign);
        if (signU != Av1ChromaFromLumaMath.SignZero)
        {
            expected += Av1ProbabilityCost.GetSymbolCost(
                Av1DefaultDistributions.ChromaFromLumaAlpha[Av1ChromaFromLumaMath.ContextU(jointSign)],
                indexU);
        }

        if (signV != Av1ChromaFromLumaMath.SignZero)
        {
            expected += Av1ProbabilityCost.GetSymbolCost(
                Av1DefaultDistributions.ChromaFromLumaAlpha[Av1ChromaFromLumaMath.ContextV(jointSign)],
                indexV);
        }

        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, BaseQIndex, updateCdf: false);
        Assert.Equal(expected, encoder.GetChromaFromLumaCost(packedIndex, jointSign));
    }

    /// <summary>
    /// Verifies that live luma rate accounting includes the selected signed directional adjustment.
    /// </summary>
    /// <param name="angleDelta">The signed AV1 directional adjustment.</param>
    [Theory]
    [InlineData(-3)]
    [InlineData(0)]
    [InlineData(3)]
    public void LumaModeCostIncludesSelectedAngleDelta(int angleDelta)
    {
        const Av1PredictionMode Mode = Av1PredictionMode.Directional135Degrees;
        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, BaseQIndex, updateCdf: false);
        Av1MacroBlockD macroBlock = new()
        {
            Tile = new Av1TileInfo(0, 0, new ObuFrameHeader())
        };

        int expected = encoder.GetLumaModeCost(Mode, 0, 0)
            + encoder.GetAngleDeltaCost(angleDelta + Av1Constants.MaxAngleDelta, Mode);

        Assert.Equal(
            expected,
            Av1TileWriter.GetLumaModeCost(
                encoder,
                macroBlock,
                Av1BlockSize.Block8x8,
                Mode,
                angleDelta));
    }

    /// <summary>
    /// Verifies that live chroma rate accounting includes the selected signed directional adjustment.
    /// </summary>
    /// <param name="angleDelta">The signed AV1 directional adjustment.</param>
    [Theory]
    [InlineData(-3)]
    [InlineData(0)]
    [InlineData(3)]
    public void ChromaModeCostIncludesSelectedAngleDelta(int angleDelta)
    {
        const Av1PredictionMode LumaMode = Av1PredictionMode.DC;
        const Av1ChromaPredictionMode ChromaMode = Av1ChromaPredictionMode.Directional135Degrees;
        const Av1BlockSize BlockSize = Av1BlockSize.Block8x8;
        ObuFrameHeader frameHeader = new();
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        Av1MacroBlockModeInfo modeInfo = default;
        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, BaseQIndex, updateCdf: false);
        bool isChromaFromLumaAllowed = BlockSize.AllowsChromaFromLuma(
            frameHeader.LosslessArray[0],
            colorConfig.SubSamplingX,
            colorConfig.SubSamplingY);

        int expected = encoder.GetChromaModeCost(ChromaMode, isChromaFromLumaAllowed, LumaMode)
            + encoder.GetAngleDeltaCost(angleDelta + Av1Constants.MaxAngleDelta, ChromaMode.ToLumaMode());

        Assert.Equal(
            expected,
            Av1TileWriter.GetChromaModeCost(
                encoder,
                frameHeader,
                colorConfig,
                modeInfo,
                BlockSize,
                LumaMode,
                ChromaMode,
                angleDelta));
    }

    [Fact]
    public void SymbolEncoderCostTracksWrittenLumaMode()
    {
        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, BaseQIndex);
        Av1Distribution expected = Av1DefaultDistributions.KeyFrameYMode[0][0];

        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(expected, (int)Av1PredictionMode.DC),
            encoder.GetLumaModeCost(Av1PredictionMode.DC, 0, 0));

        encoder.WriteLumaMode(Av1PredictionMode.DC, 0, 0);
        expected.Update((int)Av1PredictionMode.DC);

        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(expected, (int)Av1PredictionMode.DC),
            encoder.GetLumaModeCost(Av1PredictionMode.DC, 0, 0));
    }

    [Fact]
    public void CoefficientCostMatchesCurrentLibaomForEmptyAndDcBlocks()
    {
        const int qIndex = 0;
        const Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        Av1TransformBlockContext transformBlockContext = default;
        Span<int> coefficients = stackalloc int[16];
        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, qIndex, updateCdf: false);
        Av1TransformSize transformSizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);
        Av1Distribution transformSkip = Av1DefaultDistributions
            .GetTransformBlockSkip(qIndex)[(int)transformSizeContext][transformBlockContext.SkipContext];

        int emptyCost = encoder.GetCoefficientCost(
            transformSize,
            Av1TransformType.DctDct,
            Av1PredictionMode.DC,
            coefficients,
            componentType,
            transformBlockContext,
            0,
            false,
            Av1FilterIntraMode.AllFilterIntraModes);

        Assert.Equal(Av1ProbabilityCost.GetSymbolCost(transformSkip, 1), emptyCost);

        // Prime every active level with nonzero data so the one-coefficient path proves its EOB-only
        // context derivation does not depend on clearing or rebuilding the forward-neighbor map.
        coefficients.Fill(7);
        _ = encoder.GetCoefficientCost(
            transformSize,
            Av1TransformType.DctDct,
            Av1PredictionMode.DC,
            coefficients,
            componentType,
            transformBlockContext,
            16,
            false,
            Av1FilterIntraMode.AllFilterIntraModes);

        coefficients.Clear();
        coefficients[0] = 1;
        Av1Distribution endOfBlock = Av1DefaultDistributions
            .GetEndOfBlockFlag(qIndex)[transformSize.GetLog2Minus4()][(int)componentType][0];
        Av1Distribution coefficientBaseEnd = Av1DefaultDistributions
            .GetBaseEndOfBlock(qIndex)[(int)transformSizeContext][(int)componentType][0];
        Av1Distribution dcSign = Av1DefaultDistributions
            .GetDcSign(qIndex)[(int)componentType][transformBlockContext.DcSignContext];
        int expectedDcCost =
            Av1ProbabilityCost.GetSymbolCost(transformSkip, 0) +
            Av1ProbabilityCost.GetSymbolCost(endOfBlock, 0) +
            Av1ProbabilityCost.GetSymbolCost(coefficientBaseEnd, 0) +
            Av1ProbabilityCost.GetSymbolCost(dcSign, 0);

        int dcCost = encoder.GetCoefficientCost(
            transformSize,
            Av1TransformType.DctDct,
            Av1PredictionMode.DC,
            coefficients,
            componentType,
            transformBlockContext,
            1,
            false,
            Av1FilterIntraMode.AllFilterIntraModes);

        Assert.Equal(expectedDcCost, dcCost);
    }

    [Fact]
    public void CoefficientCostMatchesCurrentLibaomBaseRangeAndGolomb()
    {
        const int qIndex = 0;
        const int level = 25;
        const Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        Av1TransformBlockContext transformBlockContext = default;
        Span<int> coefficients = stackalloc int[16];
        coefficients[0] = -level;
        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, qIndex, updateCdf: false);
        Av1TransformSize transformSizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);
        Av1Distribution transformSkip = Av1DefaultDistributions
            .GetTransformBlockSkip(qIndex)[(int)transformSizeContext][transformBlockContext.SkipContext];
        Av1Distribution endOfBlock = Av1DefaultDistributions
            .GetEndOfBlockFlag(qIndex)[transformSize.GetLog2Minus4()][(int)componentType][0];
        Av1Distribution coefficientBaseEnd = Av1DefaultDistributions
            .GetBaseEndOfBlock(qIndex)[(int)transformSizeContext][(int)componentType][0];
        Av1Distribution coefficientBaseRange = Av1DefaultDistributions
            .GetCoefficientsBaseRange(qIndex)[(int)transformSizeContext][(int)componentType][0];
        Av1Distribution dcSign = Av1DefaultDistributions
            .GetDcSign(qIndex)[(int)componentType][transformBlockContext.DcSignContext];

        // Level 25 consumes all four three-symbol base-range chunks, followed by the seven-bit code for Golomb value 10.
        int expected =
            Av1ProbabilityCost.GetSymbolCost(transformSkip, 0) +
            Av1ProbabilityCost.GetSymbolCost(endOfBlock, 0) +
            Av1ProbabilityCost.GetSymbolCost(coefficientBaseEnd, 2) +
            (4 * Av1ProbabilityCost.GetSymbolCost(coefficientBaseRange, 3)) +
            Av1ProbabilityCost.GetSymbolCost(dcSign, 1) +
            Av1ProbabilityCost.GetLiteralCost(7);

        int actual = encoder.GetCoefficientCost(
            transformSize,
            Av1TransformType.DctDct,
            Av1PredictionMode.DC,
            coefficients,
            componentType,
            transformBlockContext,
            1,
            false,
            Av1FilterIntraMode.AllFilterIntraModes);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CoefficientCostMatchesCurrentLibaomCoefficientTraversal()
    {
        const ushort endOfBlock = 4;
        const Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        const Av1TransformType transformType = Av1TransformType.DctDct;
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        const Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        const Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.AllFilterIntraModes;
        Av1TransformBlockContext transformBlockContext = default;
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        Span<int> coefficients = stackalloc int[16];
        coefficients[scan[0]] = -25;
        coefficients[scan[2]] = 3;
        coefficients[scan[3]] = -4;
        using Av1LevelBuffer levels = new(Configuration.Default, new Size(4, 4));
        levels.Initialize(coefficients);
        Span<sbyte> coefficientContexts = stackalloc sbyte[16];
        Av1TransformClass transformClass = transformType.ToClass();
        Av1SymbolContextHelper.GetNzMapContexts(
            levels,
            scan,
            endOfBlock,
            transformSize,
            transformClass,
            coefficientContexts);

        Av1TransformSize transformSizeContext = Av1SymbolContextHelper.GetTransformSizeContext(transformSize);
        Av1Distribution transformSkip = Av1DefaultDistributions
            .GetTransformBlockSkip(BaseQIndex)[(int)transformSizeContext][transformBlockContext.SkipContext];
        Av1Distribution endOfBlockFlag = Av1DefaultDistributions
            .GetEndOfBlockFlag(BaseQIndex)[transformSize.GetLog2Minus4()][(int)componentType][0];
        Av1Distribution[][][] coefficientBase = Av1DefaultDistributions.GetCoefficientsBase(BaseQIndex);
        Av1Distribution[][][] coefficientBaseEnd = Av1DefaultDistributions.GetBaseEndOfBlock(BaseQIndex);
        Av1Distribution[][][] coefficientBaseRange = Av1DefaultDistributions.GetCoefficientsBaseRange(BaseQIndex);
        Av1Distribution dcSign = Av1DefaultDistributions
            .GetDcSign(BaseQIndex)[(int)componentType][transformBlockContext.DcSignContext];
        int expected = Av1ProbabilityCost.GetSymbolCost(transformSkip, 0);

        Av1TransformSetType transformSetType = Av1SymbolContextHelper.GetExtendedTransformSetType(
            transformSize,
            false);

        int extendedSet = Av1SymbolContextHelper.GetExtendedTransformSet(transformSetType);
        int transformTypeIndex = Av1SymbolContextHelper.GetExtendedTransformIndex(transformSetType, transformType);
        expected += Av1ProbabilityCost.GetSymbolCost(
            Av1DefaultDistributions.IntraExtendedTransform[extendedSet][(int)transformSize.GetSquareSize()][(int)intraDirection],
            transformTypeIndex);

        short endOfBlockPosition = Av1SymbolContextHelper.GetEndOfBlockPosition(endOfBlock, out int endOfBlockExtra);
        expected += Av1ProbabilityCost.GetSymbolCost(endOfBlockFlag, endOfBlockPosition - 1);
        int endOfBlockOffsetBitCount = Av1SymbolContextHelper.EndOfBlockOffsetBits[endOfBlockPosition];
        int endOfBlockBit = Av1Math.GetBit(endOfBlockExtra, endOfBlockOffsetBitCount - 1);
        expected += Av1ProbabilityCost.GetSymbolCost(
            Av1DefaultDistributions.GetEndOfBlockExtra(BaseQIndex)[(int)transformSizeContext][(int)componentType][endOfBlockPosition],
            endOfBlockBit);
        expected += Av1ProbabilityCost.GetLiteralCost(endOfBlockOffsetBitCount - 1);

        int eobPosition = scan[3];
        int eobContext = coefficientContexts[eobPosition];
        int eobBaseRangeContext = Av1SymbolContextHelper.GetBaseRangeContextEndOfBlock(
            levels.GetPosition(eobPosition),
            transformClass);

        expected += Av1ProbabilityCost.GetSymbolCost(
            coefficientBaseEnd[(int)transformSizeContext][(int)componentType][eobContext],
            2);
        expected += Av1ProbabilityCost.GetSymbolCost(
            coefficientBaseRange[(int)transformSizeContext][(int)componentType][eobBaseRangeContext],
            1);
        expected += Av1ProbabilityCost.GetLiteralCost(1);

        int acPosition = scan[2];
        int acContext = coefficientContexts[acPosition];
        int acBaseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext(
            levels,
            levels.GetPosition(acPosition),
            transformClass);

        expected += Av1ProbabilityCost.GetSymbolCost(
            coefficientBase[(int)transformSizeContext][(int)componentType][acContext],
            3);
        expected += Av1ProbabilityCost.GetSymbolCost(
            coefficientBaseRange[(int)transformSizeContext][(int)componentType][acBaseRangeContext],
            0);
        expected += Av1ProbabilityCost.GetLiteralCost(1);

        int zeroPosition = scan[1];
        expected += Av1ProbabilityCost.GetSymbolCost(
            coefficientBase[(int)transformSizeContext][(int)componentType][coefficientContexts[zeroPosition]],
            0);

        int dcPosition = scan[0];
        int dcContext = coefficientContexts[dcPosition];
        int dcBaseRangeContext = Av1SymbolContextHelper.GetBaseRangeContext(
            levels,
            levels.GetPosition(dcPosition),
            transformClass);

        expected += Av1ProbabilityCost.GetSymbolCost(
            coefficientBase[(int)transformSizeContext][(int)componentType][dcContext],
            3);
        expected += 4 * Av1ProbabilityCost.GetSymbolCost(
            coefficientBaseRange[(int)transformSizeContext][(int)componentType][dcBaseRangeContext],
            3);
        expected += Av1ProbabilityCost.GetLiteralCost(7);
        expected += Av1ProbabilityCost.GetSymbolCost(dcSign, 1);

        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, BaseQIndex, updateCdf: false);
        int actual = encoder.GetCoefficientCost(
            transformSize,
            transformType,
            intraDirection,
            coefficients,
            componentType,
            transformBlockContext,
            endOfBlock,
            false,
            filterIntraMode);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CoefficientCostDoesNotChangeWriterOrLiveDistributions()
    {
        const Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        const Av1TransformType transformType = Av1TransformType.DctDct;
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        const ushort endOfBlock = 4;
        Av1TransformBlockContext transformBlockContext = default;
        Span<int> coefficients = stackalloc int[16];
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        coefficients[scan[0]] = -25;
        coefficients[scan[2]] = 3;
        coefficients[scan[3]] = 1;
        using Av1SymbolEncoder actualEncoder = new(Configuration.Default, 64, BaseQIndex);
        using Av1SymbolEncoder expectedEncoder = new(Configuration.Default, 64, BaseQIndex);

        int initialCost = actualEncoder.GetCoefficientCost(
            transformSize,
            transformType,
            Av1PredictionMode.DC,
            coefficients,
            componentType,
            transformBlockContext,
            endOfBlock,
            false,
            Av1FilterIntraMode.AllFilterIntraModes);

        Assert.Equal(
            initialCost,
            actualEncoder.GetCoefficientCost(
                transformSize,
                transformType,
                Av1PredictionMode.DC,
                coefficients,
                componentType,
                transformBlockContext,
                endOfBlock,
                false,
                Av1FilterIntraMode.AllFilterIntraModes));

        int actualContext = actualEncoder.WriteCoefficients(
            transformSize,
            transformType,
            Av1PredictionMode.DC,
            coefficients,
            componentType,
            transformBlockContext,
            endOfBlock,
            false,
            Av1FilterIntraMode.AllFilterIntraModes);

        int adaptedCost = actualEncoder.GetCoefficientCost(
            transformSize,
            transformType,
            Av1PredictionMode.DC,
            coefficients,
            componentType,
            transformBlockContext,
            endOfBlock,
            false,
            Av1FilterIntraMode.AllFilterIntraModes);

        int expectedContext = expectedEncoder.WriteCoefficients(
            transformSize,
            transformType,
            Av1PredictionMode.DC,
            coefficients,
            componentType,
            transformBlockContext,
            endOfBlock,
            false,
            Av1FilterIntraMode.AllFilterIntraModes);

        using IMemoryOwner<byte> actual = actualEncoder.Exit();
        using IMemoryOwner<byte> expected = expectedEncoder.Exit();

        Assert.NotEqual(initialCost, adaptedCost);
        Assert.Equal(expectedContext, actualContext);
        Assert.True(expected.GetSpan().SequenceEqual(actual.GetSpan()));
    }

    [Fact]
    public void CoefficientCostDoesNotAllocateAfterScratchInitialization()
    {
        const Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        const Av1TransformType transformType = Av1TransformType.DctDct;
        const ushort endOfBlock = 4;
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        Span<int> coefficients = stackalloc int[16];
        coefficients[scan[0]] = -25;
        coefficients[scan[2]] = 3;
        coefficients[scan[3]] = 1;
        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, BaseQIndex, updateCdf: false);

        _ = encoder.GetCoefficientCost(
            transformSize,
            transformType,
            Av1PredictionMode.DC,
            coefficients,
            Av1ComponentType.Luminance,
            default,
            endOfBlock,
            false,
            Av1FilterIntraMode.AllFilterIntraModes);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            _ = encoder.GetCoefficientCost(
                transformSize,
                transformType,
                Av1PredictionMode.DC,
                coefficients,
                Av1ComponentType.Luminance,
                default,
                endOfBlock,
                false,
                Av1FilterIntraMode.AllFilterIntraModes);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData(1, 255, 0L, 0L)]
    [InlineData(1, 256, 0L, 1L)]
    [InlineData(128, 512, 1000L, 128_128L)]
    [InlineData(512, 512, 1000L, 128_512L)]
    [InlineData(64, 1024, 4_000_000_000L, 512_000_000_128L)]
    public void RateDistortionCostMatchesCurrentLibaom(
        int rateMultiplier,
        int rate,
        long distortion,
        long expected)
        => Assert.Equal(expected, Av1RateDistortion.GetCost(rateMultiplier, rate, distortion));

    [Theory]
    [InlineData(0, 0, 52)]
    [InlineData(0, 1, 3)]
    [InlineData(0, 2, 1)]
    [InlineData(255, 0, 9_467_088)]
    [InlineData(255, 1, 20_228_608)]
    [InlineData(255, 2, 63_215_524)]
    public void KeyFrameRateMultiplierMatchesCurrentLibaom(
        int qIndex,
        int bitDepth,
        int expected)
        => Assert.Equal(expected, Av1RateDistortion.GetKeyFrameRateMultiplier(qIndex, (Av1BitDepth)bitDepth));

    [Fact]
    public void SymbolWriterMatchesCurrentLibaomCarryRegression()
    {
        using Av1SymbolWriter writer = new(Configuration.Default, 1, updateCdf: false);
        writer.WriteBoolean(false, 16_384);
        writer.WriteBoolean(false, 16_384);
        writer.WriteBoolean(true, 512);
        writer.WriteBoolean(false, 8_192);
        using IMemoryOwner<byte> encoded = writer.Exit();

        Assert.Equal(2, encoded.Memory.Length);
        Assert.Equal(63, encoded.Memory.Span[0]);
    }

    [Fact]
    public void SymbolWriterUsesOneByteOfScratchPerEstimatedOutputByte()
    {
        const int initialSize = 257;
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        TestMemoryAllocator.AllocationRequest allocation;

        using (Av1SymbolWriter writer = new(configuration, initialSize, updateCdf: false))
        {
            writer.WriteLiteral(false);
            allocation = Assert.Single(allocator.AllocationLog);

            Assert.Equal(typeof(byte), allocation.ElementType);
            Assert.Equal(initialSize, allocation.Length);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.HashCodeOfBuffer, returned.HashCodeOfBuffer);
    }

    [Fact]
    public void SymbolWriterTransfersExistingOutputAllocationWithoutCopy()
    {
        const int initialSize = 257;
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        TestMemoryAllocator.AllocationRequest allocation;
        IMemoryOwner<byte> encoded;
        int length;

        using (Av1SymbolWriter writer = new(configuration, initialSize, updateCdf: false))
        {
            writer.WriteBoolean(false, 16_384);
            writer.WriteBoolean(false, 16_384);
            writer.WriteBoolean(true, 512);
            writer.WriteBoolean(false, 8_192);
            allocation = Assert.Single(allocator.AllocationLog);
            encoded = writer.Exit(out length);

            Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);
        }

        Assert.Empty(allocator.ReturnLog);
        using (encoded)
        {
            Assert.Equal(2, length);
            Assert.Equal(initialSize, encoded.Memory.Length);
            Assert.Equal(63, encoded.Memory.Span[0]);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.AllocationId, returned.AllocationId);
    }

    [Fact]
    public void SymbolEncoderRentsCoefficientScratchOnlyForNonzeroBlocks()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        Span<int> coefficients = stackalloc int[16];

        using (Av1SymbolEncoder encoder = new(configuration, 64, BaseQIndex))
        {
            TestMemoryAllocator.AllocationRequest outputScratch = Assert.Single(allocator.AllocationLog);
            Assert.Equal(typeof(byte), outputScratch.ElementType);

            int emptyContext = encoder.WriteCoefficients(
                Av1TransformSize.Size4x4,
                Av1TransformType.DctDct,
                Av1PredictionMode.DC,
                coefficients,
                Av1ComponentType.Luminance,
                default,
                0,
                false,
                Av1FilterIntraMode.DC);

            Assert.Equal(0, emptyContext);
            Assert.Single(allocator.AllocationLog);

            coefficients[0] = 1;
            _ = encoder.GetCoefficientCost(
                Av1TransformSize.Size4x4,
                Av1TransformType.DctDct,
                Av1PredictionMode.DC,
                coefficients,
                Av1ComponentType.Luminance,
                default,
                1,
                false,
                Av1FilterIntraMode.DC);

            Assert.Equal(3, allocator.AllocationLog.Count);

            encoder.WriteCoefficients(
                Av1TransformSize.Size4x4,
                Av1TransformType.DctDct,
                Av1PredictionMode.DC,
                coefficients,
                Av1ComponentType.Luminance,
                default,
                1,
                false,
                Av1FilterIntraMode.DC);

            Assert.Equal(3, allocator.AllocationLog.Count);
            TestMemoryAllocator.AllocationRequest levelScratch = allocator.AllocationLog[1];
            TestMemoryAllocator.AllocationRequest contextScratch = allocator.AllocationLog[2];
            int maximumTransformDimension = Av1Constants.MaxTransformSize / 2;
            int expectedLevelLength =
                (Av1Constants.TransformPadHorizontal + maximumTransformDimension) *
                (Av1Constants.TransformPadTop + maximumTransformDimension + Av1Constants.TransformPadBottom);

            Assert.Equal(typeof(byte), levelScratch.ElementType);
            Assert.Equal(expectedLevelLength, levelScratch.Length);
            Assert.Equal(AllocationOptions.Clean, levelScratch.AllocationOptions);
            Assert.Equal(typeof(sbyte), contextScratch.ElementType);
            Assert.Equal(maximumTransformDimension * maximumTransformDimension, contextScratch.Length);
        }

        Assert.Equal(3, allocator.ReturnLog.Count);
        Assert.Equal(
            allocator.AllocationLog.Select(x => x.AllocationId).Order(),
            allocator.ReturnLog.Select(x => x.AllocationId).Order());
    }

    [Fact]
    public void ReadRandomLiteral()
    {
        // Assign
        const int bitCount = 4;
        Random rand = new(bitCount);
        byte[] values = Enumerable.Range(0, 100).Select(x => (byte)rand.Next(1 << bitCount)).ToArray();
        Av1SymbolReader reader = new(values);
        List<int> actuals = [];

        // Act
        for (int i = 0; i < values.Length; i++)
        {
            actuals.Add(reader.ReadLiteral(bitCount));
        }

        // Assert
        Assert.True(values.Length > bitCount);
    }

    [Theory]
    [InlineData(0, 0, 128)]
    [InlineData(1, 255, 128)]
    public void RawBytesFromWriteLiteral1Bit(uint value, byte exp0, byte exp1)
    {
        byte[] expected = [exp0, exp1];
        AssertRawBytesWritten(1, value, expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 128)]
    [InlineData(1, 85, 118, 192)]
    [InlineData(2, 170, 165, 128)]
    [InlineData(3, 255, 255, 128)]
    public void RawBytesFromWriteLiteral2Bits(uint value, byte exp0, byte exp1, byte exp2)
    {
        byte[] expected = [exp0, exp1, exp2];
        AssertRawBytesWritten(2, value, expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 128)]
    [InlineData(1, 36, 198, 146, 128)]
    [InlineData(2, 73, 81, 182, 192)]
    [InlineData(3, 109, 192, 146, 64)]
    [InlineData(4, 146, 66, 73, 128)]
    [InlineData(5, 182, 214, 219, 128)]
    [InlineData(6, 219, 107, 109, 128)]
    [InlineData(7, 255, 255, 255, 128)]
    public void RawBytesFromWriteLiteral3Bits(uint value, byte exp0, byte exp1, byte exp2, byte exp3)
    {
        byte[] expected = [exp0, exp1, exp2, exp3];
        AssertRawBytesWritten(3, value, expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 128)]
    [InlineData(1, 17, 68, 34, 34, 128)]
    [InlineData(2, 34, 86, 68, 68, 128)]
    [InlineData(3, 51, 104, 102, 102, 128)]
    [InlineData(4, 68, 118, 34, 34, 64)]
    [InlineData(5, 85, 118, 170, 170, 192)]
    [InlineData(6, 102, 119, 51, 51, 64)]
    [InlineData(7, 119, 119, 187, 187, 192)]
    [InlineData(8, 136, 129, 17, 17, 128)]
    [InlineData(9, 153, 147, 51, 51, 128)]
    [InlineData(10, 170, 165, 85, 85, 128)]
    [InlineData(11, 187, 183, 119, 119, 128)]
    [InlineData(12, 204, 201, 153, 153, 128)]
    [InlineData(13, 221, 219, 187, 187, 128)]
    [InlineData(14, 238, 237, 221, 221, 128)]
    [InlineData(15, 255, 255, 255, 255, 128)]
    public void RawBytesFromWriteLiteral4Bits(uint value, byte exp0, byte exp1, byte exp2, byte exp3, byte exp4)
    {
        byte[] expected = [exp0, exp1, exp2, exp3, exp4];
        AssertRawBytesWritten(4, value, expected);
    }

    private static void AssertRawBytesWritten(int bitCount, uint value, byte[] expected)
    {
        // Assign
        const int writeCount = 8;
        uint[] values = new uint[writeCount];
        Array.Fill(values, value);
        Configuration configuration = Configuration.Default;
        using Av1SymbolWriter writer = new(configuration, (writeCount * bitCount) >> 3);

        // Act
        for (int i = 0; i < writeCount; i++)
        {
            writer.WriteLiteral(value, bitCount);
        }

        using IMemoryOwner<byte> actual = writer.Exit();

        // Assert
        Assert.Equal(expected, actual.GetSpan().ToArray());
    }

    [Theory]
    [InlineData(0, 0, 128)]
    [InlineData(1, 255, 128)]
    public void RawBytesReadLiteral1Bit(int value, byte exp0, byte exp1)
    {
        byte[] buffer = [exp0, exp1];
        AssertRawBytesRead(1, buffer, value);
    }

    [Theory]
    [InlineData(0, 0, 0, 128)]
    [InlineData(1, 85, 118, 192)]
    [InlineData(2, 170, 165, 128)]
    [InlineData(3, 255, 255, 128)]
    public void RawBytesReadLiteral2Bits(int value, byte exp0, byte exp1, byte exp2)
    {
        byte[] buffer = [exp0, exp1, exp2];
        AssertRawBytesRead(2, buffer, value);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 128)]
    [InlineData(1, 36, 198, 146, 128)]
    [InlineData(2, 73, 81, 182, 192)]
    [InlineData(3, 109, 192, 146, 64)]
    [InlineData(4, 146, 66, 73, 128)]
    [InlineData(5, 182, 214, 219, 128)]
    [InlineData(6, 219, 107, 109, 128)]
    [InlineData(7, 255, 255, 255, 128)]
    public void RawBytesReadLiteral3Bits(int value, byte exp0, byte exp1, byte exp2, byte exp3)
    {
        byte[] buffer = [exp0, exp1, exp2, exp3];
        AssertRawBytesRead(3, buffer, value);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 128)]
    [InlineData(1, 17, 68, 34, 34, 128)]
    [InlineData(2, 34, 86, 68, 68, 128)]
    [InlineData(3, 51, 104, 102, 102, 128)]
    [InlineData(4, 68, 118, 34, 34, 64)]
    [InlineData(5, 85, 118, 170, 170, 192)]
    [InlineData(6, 102, 119, 51, 51, 64)]
    [InlineData(7, 119, 119, 187, 187, 192)]
    [InlineData(8, 136, 129, 17, 17, 128)]
    [InlineData(9, 153, 147, 51, 51, 128)]
    [InlineData(10, 170, 165, 85, 85, 128)]
    [InlineData(11, 187, 183, 119, 119, 128)]
    [InlineData(12, 204, 201, 153, 153, 128)]
    [InlineData(13, 221, 219, 187, 187, 128)]
    [InlineData(14, 238, 237, 221, 221, 128)]
    [InlineData(15, 255, 255, 255, 255, 128)]
    public void RawBytesReadLiteral4Bits(int value, byte exp0, byte exp1, byte exp2, byte exp3, byte exp4)
    {
        byte[] buffer = [exp0, exp1, exp2, exp3, exp4];
        AssertRawBytesRead(4, buffer, value);
    }

    private static void AssertRawBytesRead(int bitCount, byte[] buffer, int expected)
    {
        // Assign
        int[] values = new int[8];
        int[] expectedValues = new int[8];
        Array.Fill(expectedValues, expected);
        Av1SymbolReader reader = new(buffer);

        // Act
        for (int i = 0; i < 8; i++)
        {
            values[i] = reader.ReadLiteral(bitCount);
        }

        // Assert
        Assert.Equal(expectedValues, values);
    }

    [Fact]
    public void RoundTripUniformPaletteIndices()
    {
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 64, BaseQIndex);

        for (int valueCount = 2; valueCount <= Av1Constants.PaletteMaxSize; valueCount++)
        {
            for (int value = 0; value < valueCount; value++)
            {
                encoder.WriteUniform(valueCount, value);
            }
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(configuration, encoded.GetSpan(), BaseQIndex);
        for (int valueCount = 2; valueCount <= Av1Constants.PaletteMaxSize; valueCount++)
        {
            for (int value = 0; value < valueCount; value++)
            {
                Assert.Equal(value, decoder.ReadUniform(valueCount));
            }
        }
    }

    [Fact]
    public void RoundTripPaletteSymbols()
    {
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 256, BaseQIndex);

        for (int blockSizeContext = 0; blockSizeContext < 7; blockSizeContext++)
        {
            for (int neighborContext = 0; neighborContext < 3; neighborContext++)
            {
                encoder.WritePaletteYMode(
                    ((blockSizeContext + neighborContext) & 1) != 0,
                    blockSizeContext,
                    neighborContext);
            }

            int paletteSize = blockSizeContext + 2;
            encoder.WritePaletteSize(paletteSize, blockSizeContext, Av1PlaneType.Y);
            encoder.WritePaletteSize(Av1Constants.PaletteMaxSize - blockSizeContext, blockSizeContext, Av1PlaneType.Uv);
        }

        encoder.WritePaletteUvMode(false, false);
        encoder.WritePaletteUvMode(true, false);
        encoder.WritePaletteUvMode(false, true);
        encoder.WritePaletteUvMode(true, true);
        for (int paletteSize = 2; paletteSize <= Av1Constants.PaletteMaxSize; paletteSize++)
        {
            for (int colorContext = 0; colorContext < 5; colorContext++)
            {
                int colorOrderIndex = (paletteSize + colorContext - 1) % paletteSize;
                encoder.WritePaletteColorIndex(colorOrderIndex, paletteSize, colorContext, Av1PlaneType.Y);
                encoder.WritePaletteColorIndex(colorOrderIndex, paletteSize, colorContext, Av1PlaneType.Uv);
            }
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(configuration, encoded.GetSpan(), BaseQIndex);
        for (int blockSizeContext = 0; blockSizeContext < 7; blockSizeContext++)
        {
            for (int neighborContext = 0; neighborContext < 3; neighborContext++)
            {
                Assert.Equal(
                    ((blockSizeContext + neighborContext) & 1) != 0,
                    decoder.ReadPaletteYMode(blockSizeContext, neighborContext));
            }

            Assert.Equal(blockSizeContext + 2, decoder.ReadPaletteSize(blockSizeContext, Av1PlaneType.Y));
            Assert.Equal(
                Av1Constants.PaletteMaxSize - blockSizeContext,
                decoder.ReadPaletteSize(blockSizeContext, Av1PlaneType.Uv));
        }

        Assert.False(decoder.ReadPaletteUvMode(false));
        Assert.True(decoder.ReadPaletteUvMode(false));
        Assert.False(decoder.ReadPaletteUvMode(true));
        Assert.True(decoder.ReadPaletteUvMode(true));
        for (int paletteSize = 2; paletteSize <= Av1Constants.PaletteMaxSize; paletteSize++)
        {
            for (int colorContext = 0; colorContext < 5; colorContext++)
            {
                int expected = (paletteSize + colorContext - 1) % paletteSize;
                Assert.Equal(
                    expected,
                    decoder.ReadPaletteColorIndex(paletteSize, colorContext, Av1PlaneType.Y));

                Assert.Equal(
                    expected,
                    decoder.ReadPaletteColorIndex(paletteSize, colorContext, Av1PlaneType.Uv));
            }
        }
    }

    [Fact]
    public void PaletteSyntaxCostsMatchCurrentDistributions()
    {
        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, BaseQIndex, updateCdf: false);
        Av1Distribution[][] yMode = Av1DefaultDistributions.PaletteYMode;
        Av1Distribution[] uvMode = Av1DefaultDistributions.PaletteUvMode;
        Av1Distribution[] ySize = Av1DefaultDistributions.PaletteYSize;
        Av1Distribution[] uvSize = Av1DefaultDistributions.PaletteUvSize;
        Av1Distribution[][] yColorIndex = Av1DefaultDistributions.PaletteYColorIndex;
        Av1Distribution[][] uvColorIndex = Av1DefaultDistributions.PaletteUvColorIndex;

        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(yMode[4][2], 1),
            encoder.GetPaletteYModeCost(true, 4, 2));

        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(uvMode[1], 0),
            encoder.GetPaletteUvModeCost(false, true));

        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(ySize[3], 4),
            encoder.GetPaletteSizeCost(6, 3, Av1PlaneType.Y));

        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(uvSize[5], 1),
            encoder.GetPaletteSizeCost(3, 5, Av1PlaneType.Uv));

        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(yColorIndex[6][4], 7),
            encoder.GetPaletteColorIndexCost(7, 8, 4, Av1PlaneType.Y));

        Assert.Equal(
            Av1ProbabilityCost.GetSymbolCost(uvColorIndex[3][2], 4),
            encoder.GetPaletteColorIndexCost(4, 5, 2, Av1PlaneType.Uv));

        Assert.Equal(Av1ProbabilityCost.GetLiteralCost(2), Av1SymbolEncoder.GetUniformCost(5, 2));
        Assert.Equal(Av1ProbabilityCost.GetLiteralCost(3), Av1SymbolEncoder.GetUniformCost(5, 3));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void RoundTripPaletteColors(int bitDepth)
    {
        ushort[] colorCache = [4, 17, 23, 51];
        ushort[] yColors = [4, 23, 90];
        ushort[] uColors = [17, 51, 100];
        ushort[] deltaVColors = [1, 2, 1];
        ushort[] rawVColors = [0, (ushort)(1 << (bitDepth - 1)), 0];
        using Av1SymbolEncoder encoder = new(Configuration.Default, 128, BaseQIndex);
        encoder.WritePaletteYColors(colorCache, yColors, bitDepth);
        encoder.WritePaletteUvColors(colorCache, uColors, deltaVColors, bitDepth);
        encoder.WritePaletteUvColors(colorCache, uColors, rawVColors, bitDepth);

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        ushort[] decodedYColors = new ushort[yColors.Length];
        ushort[] decodedUColors = new ushort[uColors.Length];
        ushort[] decodedDeltaVColors = new ushort[deltaVColors.Length];
        ushort[] decodedRawVColors = new ushort[rawVColors.Length];
        decoder.ReadPaletteYColors(colorCache, yColors.Length, bitDepth, decodedYColors);
        decoder.ReadPaletteUvColors(
            colorCache,
            uColors.Length,
            bitDepth,
            decodedUColors,
            decodedDeltaVColors);

        Assert.Equal(yColors, decodedYColors);
        Assert.Equal(uColors, decodedUColors);
        Assert.Equal(deltaVColors, decodedDeltaVColors);

        decodedUColors.AsSpan().Clear();
        decoder.ReadPaletteUvColors(
            colorCache,
            uColors.Length,
            bitDepth,
            decodedUColors,
            decodedRawVColors);

        Assert.Equal(uColors, decodedUColors);
        Assert.Equal(rawVColors, decodedRawVColors);
    }

    [Fact]
    public void PaletteColorCostsMatchCurrentLibaomBitCounts()
    {
        ushort[] colorCache = [5, 10, 20];

        Assert.Equal(
            Av1ProbabilityCost.GetLiteralCost(20),
            Av1SymbolEncoder.GetPaletteYColorCost([], [10, 20, 21], 8));

        Assert.Equal(
            Av1ProbabilityCost.GetLiteralCost(11),
            Av1SymbolEncoder.GetPaletteYColorCost(colorCache, [5, 20, 30], 8));

        Assert.Equal(
            Av1ProbabilityCost.GetLiteralCost(32),
            Av1SymbolEncoder.GetPaletteUvColorCost(colorCache, [5, 20, 30], [20, 21, 20], 8));

        Assert.Equal(
            Av1ProbabilityCost.GetLiteralCost(36),
            Av1SymbolEncoder.GetPaletteUvColorCost(colorCache, [5, 20, 30], [0, 128, 0], 8));
    }

    [Fact]
    public void PaletteColorCostDoesNotAllocate()
    {
        ushort[] colorCache = [5, 10, 20];
        ushort[] yColors = [5, 20, 30];
        ushort[] uColors = [5, 20, 30];
        ushort[] vColors = [20, 21, 20];
        _ = Av1SymbolEncoder.GetPaletteYColorCost(colorCache, yColors, 8);
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 1000; i++)
        {
            _ = Av1SymbolEncoder.GetPaletteYColorCost(colorCache, yColors, 8);
            _ = Av1SymbolEncoder.GetPaletteUvColorCost(colorCache, uColors, vColors, 8);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    [Fact]
    public void PaletteColorMapCostMatchesCurrentDistributions()
    {
        using Buffer2D<byte> map = Configuration.Default.MemoryAllocator.Allocate2D<byte>(2, 2, AllocationOptions.Clean);
        map.DangerousGetRowSpan(0)[0] = 2;
        map.DangerousGetRowSpan(0)[1] = 0;
        map.DangerousGetRowSpan(1)[0] = 1;
        map.DangerousGetRowSpan(1)[1] = 2;
        Buffer2DRegion<byte> region = new(map);
        Av1Distribution[][] distributions = Av1DefaultDistributions.PaletteYColorIndex;
        int expected = Av1SymbolEncoder.GetUniformCost(3, 2);
        expected += Av1ProbabilityCost.GetSymbolCost(distributions[1][0], 1);
        expected += Av1ProbabilityCost.GetSymbolCost(distributions[1][0], 2);
        expected += Av1ProbabilityCost.GetSymbolCost(distributions[1][1], 2);
        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, BaseQIndex, updateCdf: false);

        Assert.Equal(
            expected,
            encoder.GetPaletteColorMapCost(3, Av1PlaneType.Y, 2, 2, region));
    }

    [Fact]
    public void PaletteCacheMergesSortedNeighborColorsWithoutDuplicates()
    {
        ReadOnlySpan<ushort> above = [1, 3, 5, 7];
        ReadOnlySpan<ushort> left = [2, 3, 6, 7];
        Span<ushort> cache = stackalloc ushort[2 * Av1Constants.PaletteMaxSize];

        int count = Av1PaletteCache.Merge(above, left, cache);

        Assert.Equal([1, 2, 3, 5, 6, 7], cache[..count].ToArray());
    }

    [Theory]
    [InlineData(1, 1, 0, 0, 0, 1, 4, 1)]
    [InlineData(1, 1, 0, 1, 0, 1, 3, 1)]
    [InlineData(1, 1, 0, 0, 1, 1, 2, 1)]
    [InlineData(1, 1, 0, 2, 1, 2, 1, 2)]
    [InlineData(0, 1, 0, 2, 0, 0, 0, 1)]
    public void PaletteColorMapContextMatchesCurrentLibaom(
        int row,
        int column,
        byte left,
        byte upperLeft,
        byte above,
        byte current,
        int expectedContext,
        int expectedOrderIndex)
    {
        using Buffer2D<byte> map = Configuration.Default.MemoryAllocator.Allocate2D<byte>(2, 2, AllocationOptions.Clean);
        map.DangerousGetRowSpan(0)[0] = upperLeft;
        map.DangerousGetRowSpan(0)[1] = above;
        map.DangerousGetRowSpan(1)[0] = left;
        map.DangerousGetRowSpan(row)[column] = current;
        Span<byte> colorOrder = stackalloc byte[Av1Constants.PaletteMaxSize];

        int actualContext = Av1PaletteColorMap.GetContext(
            new Buffer2DRegion<byte>(map),
            row,
            column,
            paletteSize: 4,
            current,
            colorOrder,
            out int actualOrderIndex);

        Assert.Equal(expectedContext, actualContext);
        Assert.Equal(expectedOrderIndex, actualOrderIndex);
    }

    [Fact]
    public void RoundTripPaletteColorMaps()
    {
        const int Rows = 5;
        const int Columns = 7;
        const int Width = 9;
        const int Height = 6;
        Configuration configuration = Configuration.Default;
        using Buffer2D<byte> source = configuration.MemoryAllocator.Allocate2D<byte>(Width, Height);
        using Buffer2D<byte> decoded = configuration.MemoryAllocator.Allocate2D<byte>(Width, Height);
        Buffer2DRegion<byte> sourceRegion = new(source);
        Buffer2DRegion<byte> decodedRegion = new(decoded);
        using Av1SymbolEncoder encoder = new(configuration, 512, BaseQIndex);
        for (int paletteSize = 2; paletteSize <= Av1Constants.PaletteMaxSize; paletteSize++)
        {
            for (int plane = 0; plane < 2; plane++)
            {
                for (int row = 0; row < Rows; row++)
                {
                    Span<byte> sourceRow = source.DangerousGetRowSpan(row);
                    for (int column = 0; column < Columns; column++)
                    {
                        sourceRow[column] = (byte)(((row * 3) + (column * 5) + plane) % paletteSize);
                    }
                }

                encoder.WritePaletteColorMap(
                    paletteSize,
                    (Av1PlaneType)plane,
                    Rows,
                    Columns,
                    sourceRegion);
            }
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(configuration, encoded.GetSpan(), BaseQIndex);
        for (int paletteSize = 2; paletteSize <= Av1Constants.PaletteMaxSize; paletteSize++)
        {
            for (int plane = 0; plane < 2; plane++)
            {
                for (int row = 0; row < Height; row++)
                {
                    decoded.DangerousGetRowSpan(row).Fill(byte.MaxValue);
                }

                decoder.ReadPaletteColorMap(
                    paletteSize,
                    (Av1PlaneType)plane,
                    Rows,
                    Columns,
                    decodedRegion);

                for (int row = 0; row < Rows; row++)
                {
                    ReadOnlySpan<byte> decodedRow = decoded.DangerousGetRowSpan(row);
                    for (int column = 0; column < Columns; column++)
                    {
                        Assert.Equal(
                            (byte)(((row * 3) + (column * 5) + plane) % paletteSize),
                            decodedRow[column]);
                    }

                    for (int column = Columns; column < Width; column++)
                    {
                        Assert.Equal(byte.MaxValue, decodedRow[column]);
                    }
                }

                for (int row = Rows; row < Height; row++)
                {
                    ReadOnlySpan<byte> decodedRow = decoded.DangerousGetRowSpan(row);
                    for (int column = 0; column < Width; column++)
                    {
                        Assert.Equal(byte.MaxValue, decodedRow[column]);
                    }
                }
            }
        }

        decoder.ValidateTrailingBits();
    }

    [Fact]
    public void PaletteColorMapCostDoesNotAllocateAfterEntropyInitialization()
    {
        using Buffer2D<byte> map = Configuration.Default.MemoryAllocator.Allocate2D<byte>(7, 5, AllocationOptions.Clean);
        for (int row = 0; row < map.Height; row++)
        {
            Span<byte> mapRow = map.DangerousGetRowSpan(row);
            for (int column = 0; column < map.Width; column++)
            {
                mapRow[column] = (byte)(((row * 3) + (column * 5)) % 4);
            }
        }

        using Av1SymbolEncoder encoder = new(Configuration.Default, 64, BaseQIndex, updateCdf: false);
        Buffer2DRegion<byte> region = new(map);
        _ = encoder.GetPaletteColorMapCost(4, Av1PlaneType.Y, map.Height, map.Width, region);
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 1000; i++)
        {
            _ = encoder.GetPaletteColorMapCost(4, Av1PlaneType.Y, map.Height, map.Width, region);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    [Theory]
    [MemberData(nameof(GetRangeData), 20)]
    public void RoundTripPartitionType(int context)
    {
        // Assign
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.None,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.None, Av1PartitionType.None];
        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WritePartitionType(value, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadPartitionType(context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetSplitPartitionTypeData))]
    public void RoundTripSplitOrHorizontalPartitionType(int size, int context)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)size;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Horizontal,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Horizontal, Av1PartitionType.Horizontal];
        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WriteSplitOrHorizontal(value, blockSize, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSplitOrHorizontal(blockSize, context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetSplitPartitionTypeData))]
    public void RoundTripSplitOrVerticalPartitionType(int size, int context)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)size;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1PartitionType[] values = [
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Vertical,
            Av1PartitionType.Split, Av1PartitionType.Split, Av1PartitionType.Vertical, Av1PartitionType.Vertical];
        Av1PartitionType[] actuals = new Av1PartitionType[values.Length];

        // Act
        foreach (Av1PartitionType value in values)
        {
            encoder.WriteSplitOrVertical(value, blockSize, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSplitOrVertical(blockSize, context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RoundTripSkip(int context)
    {
        // Assign
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        bool[] values = [true, true, false, false, false, false, false, false, true];
        bool[] actuals = new bool[values.Length];

        // Act
        foreach (bool value in values)
        {
            encoder.WriteSkip(value, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSkip(context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetTransformBlockSkipData))]
    internal void RoundTripTransformBlockSkip(int transformContext, int skipContext)
    {
        // Assign
        Av1TransformSize transformSizeContext = (Av1TransformSize)transformContext;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        bool[] values = [true, true, false, false, false, false, false, false, true];
        bool[] actuals = new bool[values.Length];

        // Act
        foreach (bool value in values)
        {
            encoder.WriteTransformBlockSkip(value, transformSizeContext, skipContext);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadTransformBlockSkip(transformSizeContext, skipContext);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetTransformTypeData))]

    // [InlineData(2, 0, 1)]
    public void RoundTripTransformType(int txSizeContext, int intraMode, int intraDir)
    {
        // Assign
        Av1TransformSize transformSizeContext = (Av1TransformSize)txSizeContext;
        Av1FilterIntraMode filterIntraMode = (Av1FilterIntraMode)intraMode;
        Av1PredictionMode intraDirection = (Av1PredictionMode)intraDir;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);

        // TODO: Include AdstFlipAdst, which is currently mapped to Identity.
        Av1TransformType[] values = [
            Av1TransformType.DctDct, Av1TransformType.DctDct, Av1TransformType.Identity, Av1TransformType.AdstDct,
            Av1TransformType.DctDct, Av1TransformType.AdstAdst, Av1TransformType.Identity, Av1TransformType.DctAdst
        ];
        Av1TransformType[] actuals = new Av1TransformType[values.Length];

        // Act
        foreach (Av1TransformType value in values)
        {
            encoder.WriteTransformType(value, transformSizeContext, true, BaseQIndex, filterIntraMode, intraDirection);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadTransformType(transformSizeContext, true, false, false, false, filterIntraMode, intraDirection);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetEndOfBlockPositionData))]
    public void RoundTripEndOfBlockPosition(int txSize, int txSizeContext, int plane, int txClass)
    {
        // Assign
        Av1TransformSize transformSize = (Av1TransformSize)txSize;
        Av1TransformSize transformSizeContext = (Av1TransformSize)txSizeContext;
        Av1ComponentType componentType = (Av1ComponentType)plane;
        Av1PlaneType planeType = (Av1PlaneType)plane;
        Av1TransformClass transformClass = (Av1TransformClass)txClass;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);

        int[] values = [1, 2, 3, 4, 5];
        int[] actuals = new int[values.Length];

        // Act
        foreach (int value in values)
        {
            encoder.WriteEndOfBlockPosition((ushort)value, componentType, transformClass, transformSize, transformSizeContext);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadEndOfBlockPosition(transformSize, transformClass, transformSizeContext, planeType);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Fact]
    public void RoundTripGolomb()
    {
        // Assign
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);

        int[] values = Enumerable.Range(0, 16384).ToArray();
        int[] actuals = new int[values.Length];

        // Act
        foreach (int value in values)
        {
            encoder.WriteGolomb(value);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadGolomb();
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RoundTripSegmentId(int context)
    {
        // Assign
        int[] values = [3, 6, 7, 0, 2, 0, 2, 1, 1];
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        int[] actuals = new int[values.Length];

        // Act
        foreach (int value in values)
        {
            encoder.WriteSegmentId(value, context);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadSegmentId(context);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Fact]
    public void RoundTripDeltaQuantizerIndex()
    {
        // Assign
        int[] values = [3, 6, -7, -8, -2, 0, 2, 1, -1];
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        int[] actuals = new int[values.Length];

        // Act
        foreach (int value in values)
        {
            encoder.WriteDeltaQuantizerIndex(value);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadDeltaQuantizerIndex();
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Theory]
    [MemberData(nameof(GetRangeData), (int)Av1BlockSize.AllSizes)]
    public void RoundTripFilterIntraMode(int bSize)
    {
        // Assign
        Av1BlockSize blockSize = (Av1BlockSize)bSize;
        Av1FilterIntraMode[] values = [
            Av1FilterIntraMode.DC, Av1FilterIntraMode.Vertical, Av1FilterIntraMode.DC, Av1FilterIntraMode.Paeth,
            Av1FilterIntraMode.AllFilterIntraModes, Av1FilterIntraMode.Directional157, Av1FilterIntraMode.DC, Av1FilterIntraMode.Directional157];
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Av1FilterIntraMode[] actuals = new Av1FilterIntraMode[values.Length];

        // Act
        foreach (Av1FilterIntraMode value in values)
        {
            encoder.WriteFilterIntraMode(value, blockSize);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadFilterUltraMode(blockSize);
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    [Fact]
    public void RoundTripUseIntraBlockCopy()
    {
        // Assign
        bool[] values = [true, true, false, true, false, false, false];
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        bool[] actuals = new bool[values.Length];

        // Act
        foreach (bool value in values)
        {
            encoder.WriteUseIntraBlockCopy(value);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        for (int i = 0; i < values.Length; i++)
        {
            actuals[i] = decoder.ReadUseIntraBlockCopy();
        }

        // Assert
        Assert.Equal(values, actuals);
    }

    /// <summary>
    /// Verifies integer displacement-vector joints, signs, magnitude classes, and adaptive offset bits.
    /// </summary>
    [Fact]
    public void RoundTripIntraBlockCopyDisplacementVectors()
    {
        Av1MotionVector[] references =
        [
            new(0, -4096),
            new(-1024, 0),
            new(256, -256),
            new(0, 0),
            new(-2048, 2048),
        ];

        Av1MotionVector[] values =
        [
            new(8, -4096),
            new(-1040, 24),
            new(256, -336),
            new(512, 1024),
            new(6144, -6144),
        ];

        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 64, BaseQIndex);

        for (int i = 0; i < values.Length; i++)
        {
            encoder.WriteDisplacementVector(values[i], references[i]);
        }

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(configuration, encoded.GetSpan(), BaseQIndex);
        Av1MotionVector[] actual = new Av1MotionVector[values.Length];

        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = decoder.ReadDisplacementVector(references[i]);
        }

        Assert.Equal(values, actual);
    }

    public static TheoryData<int> GetRangeData(int count)
    {
        TheoryData<int> result = [];
        for (int i = 0; i < count; i++)
        {
            result.Add(i);
        }

        return result;
    }

    public static TheoryData<int, int> GetTransformBlockSkipData()
    {
        TheoryData<int, int> result = [];
        for (Av1TransformSize transformSizeContext = Av1TransformSize.Size4x4; transformSizeContext <= Av1TransformSize.Size64x64; transformSizeContext++)
        {
            for (int skipContext = 0; skipContext < 5; skipContext++)
            {
                result.Add((int)transformSizeContext, skipContext);
            }
        }

        return result;
    }

    public static TheoryData<int, int> GetSplitPartitionTypeData()
    {
        TheoryData<int, int> result = [];
        for (Av1BlockSize blockSize = Av1BlockSize.Block4x4; blockSize < Av1BlockSize.AllSizes; blockSize++)
        {
            for (int context = 4; context < 16; context++)
            {
                result.Add((int)blockSize, context);
            }
        }

        return result;
    }

    public static TheoryData<int, int, int> GetTransformTypeData()
    {
        TheoryData<int, int, int> result = [];
        for (Av1TransformSize transformSize = Av1TransformSize.Size4x4; transformSize < Av1TransformSize.AllSizes; transformSize++)
        {
            if (transformSize == Av1TransformSize.Size16x16)
            {
                for (Av1PredictionMode intraDirection = Av1PredictionMode.IntraModeStart; intraDirection < Av1PredictionMode.IntraModeEnd; intraDirection++)
                {
                    result.Add((int)transformSize, (int)Av1FilterIntraMode.AllFilterIntraModes, (int)intraDirection);
                }

                if (transformSize == Av1TransformSize.Size16x16)
                {
                    result.Add((int)transformSize, 0, 0);
                    result.Add((int)transformSize, 1, 1);
                    result.Add((int)transformSize, 2, 2);
                    result.Add((int)transformSize, 3, 6);
                    result.Add((int)transformSize, 4, 0);
                }

                continue;
            }

            if (transformSize.GetSquareSize() >= Av1TransformSize.Size16x16 || transformSize is Av1TransformSize.Size32x8 or Av1TransformSize.Size8x32)
            {
                // DctOnly, doesn't make sense to test.
                continue;
            }

            for (Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC; filterIntraMode <= Av1FilterIntraMode.AllFilterIntraModes; filterIntraMode++)
            {
                for (Av1PredictionMode intraDirection = Av1PredictionMode.IntraModeStart; intraDirection < Av1PredictionMode.IntraModeEnd; intraDirection++)
                {
                    result.Add((int)transformSize, (int)filterIntraMode, (int)intraDirection);
                }
            }
        }

        return result;
    }

    public static TheoryData<int, int, int, int> GetEndOfBlockPositionData()
    {
        TheoryData<int, int, int, int> result = [];
        for (Av1TransformSize transformSize = Av1TransformSize.Size4x4; transformSize < Av1TransformSize.AllSizes; transformSize++)
        {
            for (Av1TransformSize transformSizeContext = Av1TransformSize.Size4x4; transformSizeContext <= Av1TransformSize.Size64x64; transformSizeContext++)
            {
                for (int componentType = 0; componentType < 2; componentType++)
                {
                    for (Av1TransformClass transformClass = Av1TransformClass.Class2D; transformClass <= Av1TransformClass.ClassVertical; transformClass++)
                    {
                        result.Add((int)transformSize, (int)transformSizeContext, componentType, (int)transformClass);
                    }
                }
            }
        }

        return result;
    }
}
