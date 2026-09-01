// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies inter-frame block-prefix, reference selection, motion-mode, and interpolation-filter syntax.
/// </summary>
[Trait("Format", "Avif")]
public class Av1InterFrameModeInfoTests
{
    /// <summary>
    /// Verifies that an inter frame can select an intra-coded block using the block-size luma distribution.
    /// </summary>
    [Fact]
    public void ReadInterFrameModeInfoReadsIntraCodedBlock()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);
        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None);

        Av1Distribution skip = Av1DefaultDistributions.Skip[0];
        Av1Distribution intraInter = Av1DefaultDistributions.IntraInter[0];
        Av1Distribution yMode = Av1DefaultDistributions.FrameYMode[1];
        using Av1SymbolWriter writer = new(Configuration.Default, 1, updateCdf: true);
        writer.WriteSymbol(false, skip);
        writer.WriteSymbol(false, intraInter);
        writer.WriteSymbol((int)Av1PredictionMode.DC, yMode);
        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo, new Av1TileInfo(0, 0, frameHeader));
        modeInfo = partitionInfo.ModeInfo;

        Assert.False(modeInfo.SkipMode);
        Assert.False(modeInfo.Skip);
        Assert.Equal(Av1ReferenceFrameType.Intra, modeInfo.ReferenceFrames[0]);
        Assert.Equal(Av1ReferenceFrameType.None, modeInfo.ReferenceFrames[1]);
        Assert.Equal(Av1PredictionMode.DC, modeInfo.YMode);
        Assert.Equal(Av1ChromaPredictionMode.DC, modeInfo.UvMode);
    }

    /// <summary>
    /// Verifies that skip mode omits the residual-skip and intra-inter symbols and marks the block as inter coded.
    /// </summary>
    [Fact]
    public void SkipModeForcesInterAndSkipsResidual()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        sequenceHeader.OrderHintInfo.EnableOrderHint = true;
        sequenceHeader.OrderHintInfo.OrderHintBits = 3;
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.ReferenceMode = ObuReferenceMode.ReferenceModeSelect;
        frameHeader.OrderHint = 4;
        for (int index = 0; index < Av1Constants.ReferencesPerFrame; index++)
        {
            frameHeader.GetReferenceFrameIndices()[index] = (uint)index;
        }

        Span<uint> referenceOrderHints = frameHeader.GetReferenceOrderHints();
        referenceOrderHints[0] = 3;
        referenceOrderHints[1] = 2;
        referenceOrderHints[2] = 1;
        referenceOrderHints[3] = 0;
        referenceOrderHints[4] = 5;
        referenceOrderHints[5] = 6;
        referenceOrderHints[6] = 7;
        frameHeader.SkipModeParameters.Derive(sequenceHeader.OrderHintInfo, frameHeader);
        frameHeader.SkipModeParameters.SkipModeFlag = true;
        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo aboveModeInfo = new(Av1BlockSize.Block8x8, Point.Empty) { SkipMode = true };
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);

        Av1Distribution skipMode = Av1DefaultDistributions.SkipMode[1];
        using Av1SymbolWriter writer = new(Configuration.Default, 1, updateCdf: true);
        writer.WriteSymbol(true, skipMode);
        using IMemoryOwner<byte> encoded = writer.Exit();
        Memory<byte> encodedMemory = encoded.Memory;

        modeInfo = ReadInterFrameModeInfo(tileReader, encodedMemory, modeInfo, aboveModeInfo);

        Assert.True(modeInfo.SkipMode);
        Assert.True(modeInfo.Skip);
        Assert.Equal(Av1PredictionMode.NearestNearestMotionVector, modeInfo.YMode);
        Assert.Equal(Av1ReferenceFrameType.Last, modeInfo.ReferenceFrames[0]);
        Assert.Equal(Av1ReferenceFrameType.Backward, modeInfo.ReferenceFrames[1]);
        Assert.Equal(Av1CompoundType.Average, modeInfo.CompoundType);
    }

    /// <summary>
    /// Verifies switchable interpolation-filter decoding with shared and independent axis selections.
    /// </summary>
    /// <param name="enableDualFilter">Whether the horizontal axis carries an independent filter symbol.</param>
    /// <param name="expectedHorizontalFilter">The expected horizontal interpolation filter.</param>
    [Theory]
    [InlineData(false, (int)Av1InterpolationFilter.Smooth)]
    [InlineData(true, (int)Av1InterpolationFilter.Sharp)]
    public void ReadInterFrameModeInfoReadsInterpolationFilters(
        bool enableDualFilter,
        int expectedHorizontalFilter)
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        sequenceHeader.EnableDualFilter = enableDualFilter;
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.InterpolationFilter = Av1InterpolationFilter.Switchable;

        // Forcing segment zero to GLOBALMV removes reference and inter-mode symbols from this focused fixture. A
        // translational global model still requires interpolation, leaving only the filter branch under test.
        ObuSegmentationParameters segmentationParameters = frameHeader.SegmentationParameters;
        segmentationParameters.Enabled = true;
        segmentationParameters.SetFeatureEnabled(0, (int)ObuSegmentationLevelFeature.GlobalMotionVector, true);
        frameHeader.GetGlobalMotionParameters()[0].Type = Av1GlobalMotionType.Translation;

        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);
        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None);
        using Av1SymbolWriter writer = new(Configuration.Default, 2, updateCdf: true);
        writer.WriteSymbol(false, Av1DefaultDistributions.Skip[0]);
        writer.WriteSymbol((int)Av1InterpolationFilter.Smooth, Av1DefaultDistributions.SwitchableInterpolation[3]);
        if (enableDualFilter)
        {
            writer.WriteSymbol((int)Av1InterpolationFilter.Sharp, Av1DefaultDistributions.SwitchableInterpolation[11]);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);

        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo, new Av1TileInfo(0, 0, frameHeader));
        modeInfo = partitionInfo.ModeInfo;

        Assert.Equal(Av1InterpolationFilter.Smooth, modeInfo.InterpolationFilters[0]);
        Assert.Equal((Av1InterpolationFilter)expectedHorizontalFilter, modeInfo.InterpolationFilters[1]);
    }

    /// <summary>
    /// Verifies that an identity global-motion block omits switchable interpolation-filter symbols.
    /// </summary>
    [Fact]
    public void IdentityGlobalMotionOmitsInterpolationFilters()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        sequenceHeader.EnableDualFilter = true;
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.InterpolationFilter = Av1InterpolationFilter.Switchable;
        ObuSegmentationParameters segmentationParameters = frameHeader.SegmentationParameters;
        segmentationParameters.Enabled = true;
        segmentationParameters.SetFeatureEnabled(0, (int)ObuSegmentationLevelFeature.GlobalMotionVector, true);

        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);
        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None);
        using Av1SymbolWriter writer = new(Configuration.Default, 3, updateCdf: true);
        writer.WriteSymbol(false, Av1DefaultDistributions.Skip[0]);

        // Identity is distinct from Translation for this syntax gate. These sentinel symbols must remain unread even
        // though the separate global-motion-block classification requires a model greater than Translation.
        writer.WriteSymbol((int)Av1InterpolationFilter.Smooth, Av1DefaultDistributions.SwitchableInterpolation[3]);
        writer.WriteSymbol((int)Av1InterpolationFilter.Sharp, Av1DefaultDistributions.SwitchableInterpolation[11]);
        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);

        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo, new Av1TileInfo(0, 0, frameHeader));
        modeInfo = partitionInfo.ModeInfo;

        Assert.Equal(Av1InterpolationFilter.Regular, modeInfo.InterpolationFilters[0]);
        Assert.Equal(Av1InterpolationFilter.Regular, modeInfo.InterpolationFilters[1]);
    }

    /// <summary>
    /// Verifies every unidirectional and bidirectional compound reference-tree leaf through paired motion parsing.
    /// </summary>
    /// <param name="pairIndex">The zero-based normative compound reference pair.</param>
    /// <param name="expectedPrimary">The expected primary retained-reference label.</param>
    /// <param name="expectedSecondary">The expected secondary retained-reference label.</param>
    [Theory]
    [InlineData(0, (int)Av1ReferenceFrameType.Backward, (int)Av1ReferenceFrameType.Alternate)]
    [InlineData(1, (int)Av1ReferenceFrameType.Last, (int)Av1ReferenceFrameType.Last2)]
    [InlineData(2, (int)Av1ReferenceFrameType.Last, (int)Av1ReferenceFrameType.Last3)]
    [InlineData(3, (int)Av1ReferenceFrameType.Last, (int)Av1ReferenceFrameType.Golden)]
    [InlineData(4, (int)Av1ReferenceFrameType.Last, (int)Av1ReferenceFrameType.Backward)]
    [InlineData(5, (int)Av1ReferenceFrameType.Last2, (int)Av1ReferenceFrameType.Alternate2)]
    [InlineData(6, (int)Av1ReferenceFrameType.Last3, (int)Av1ReferenceFrameType.Alternate)]
    [InlineData(7, (int)Av1ReferenceFrameType.Golden, (int)Av1ReferenceFrameType.Alternate)]
    public void ReadInterFrameModeInfoReadsCompoundReferencePair(
        int pairIndex,
        int expectedPrimary,
        int expectedSecondary)
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.ReferenceMode = ObuReferenceMode.ReferenceModeSelect;
        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);
        using Av1SymbolWriter writer = new(Configuration.Default, 8, updateCdf: true);
        writer.WriteSymbol(false, Av1DefaultDistributions.Skip[0]);
        writer.WriteSymbol(true, Av1DefaultDistributions.IntraInter[0]);
        writer.WriteSymbol(true, Av1DefaultDistributions.CompInter[1]);
        WriteCompoundReferencePair(writer, pairIndex);
        writer.WriteSymbol(0, Av1DefaultDistributions.InterCompoundMode[0]);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Memory<byte> encodedMemory = encoded.Memory;

        modeInfo = ReadInterFrameModeInfo(tileReader, encodedMemory, modeInfo);

        Assert.Equal((Av1ReferenceFrameType)expectedPrimary, modeInfo.ReferenceFrames[0]);
        Assert.Equal((Av1ReferenceFrameType)expectedSecondary, modeInfo.ReferenceFrames[1]);
        Assert.Equal(Av1PredictionMode.NearestNearestMotionVector, modeInfo.YMode);
        Assert.Equal(default(Av1MotionVector), modeInfo.MotionVectors[0]);
        Assert.Equal(default(Av1MotionVector), modeInfo.MotionVectors[1]);
        Assert.Equal(Av1CompoundType.Average, modeInfo.CompoundType);
    }

    /// <summary>
    /// Verifies selectable compound syntax in its normative position before interpolation filtering.
    /// </summary>
    /// <param name="compoundTypeValue">The selected compound operation.</param>
    [Theory]
    [InlineData((int)Av1CompoundType.DistanceWeighted)]
    [InlineData((int)Av1CompoundType.Wedge)]
    [InlineData((int)Av1CompoundType.DifferenceWeighted)]
    public void ReadsSelectableCompoundBeforeInterpolation(int compoundTypeValue)
    {
        Av1CompoundType compoundType = (Av1CompoundType)compoundTypeValue;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        sequenceHeader.EnableMaskedCompound = true;
        sequenceHeader.EnableDualFilter = false;
        sequenceHeader.OrderHintInfo.EnableOrderHint = true;
        sequenceHeader.OrderHintInfo.EnableJointCompound = true;
        sequenceHeader.OrderHintInfo.OrderHintBits = 3;
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.ReferenceMode = ObuReferenceMode.ReferenceModeSelect;
        frameHeader.InterpolationFilter = Av1InterpolationFilter.Switchable;
        frameHeader.OrderHint = 4;
        frameHeader.GetReferenceFrameIndices()[0] = 0;
        frameHeader.GetReferenceFrameIndices()[1] = 1;
        frameHeader.GetReferenceOrderHints()[0] = 3;
        frameHeader.GetReferenceOrderHints()[1] = 5;

        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);
        using Av1SymbolWriter writer = new(Configuration.Default, 12, updateCdf: true);
        writer.WriteSymbol(false, Av1DefaultDistributions.Skip[0]);
        writer.WriteSymbol(true, Av1DefaultDistributions.IntraInter[0]);
        writer.WriteSymbol(true, Av1DefaultDistributions.CompInter[1]);
        WriteCompoundReferencePair(writer, pairIndex: 1);
        writer.WriteSymbol(0, Av1DefaultDistributions.InterCompoundMode[0]);

        bool masked = compoundType is Av1CompoundType.Wedge or Av1CompoundType.DifferenceWeighted;
        writer.WriteSymbol(masked, Av1DefaultDistributions.CompoundGroupIndex[0]);
        if (masked)
        {
            writer.WriteSymbol(
                compoundType == Av1CompoundType.Wedge ? 0 : 1,
                Av1DefaultDistributions.CompoundType[(int)Av1BlockSize.Block8x8]);

            if (compoundType == Av1CompoundType.Wedge)
            {
                writer.WriteSymbol(13, Av1DefaultDistributions.WedgeIndex[(int)Av1BlockSize.Block8x8]);
                writer.WriteLiteral(true);
            }
            else
            {
                writer.WriteLiteral(true);
            }
        }
        else
        {
            // Equal reference distances select context three; false chooses distance weighting.
            writer.WriteSymbol(false, Av1DefaultDistributions.CompoundIndex[3]);
        }

        writer.WriteSymbol((int)Av1InterpolationFilter.Sharp, Av1DefaultDistributions.SwitchableInterpolation[3]);

        using IMemoryOwner<byte> encoded = writer.Exit();
        modeInfo = ReadInterFrameModeInfo(tileReader, encoded.Memory, modeInfo);

        Assert.Equal(Av1ReferenceFrameType.Last, modeInfo.ReferenceFrames[0]);
        Assert.Equal(Av1ReferenceFrameType.Last2, modeInfo.ReferenceFrames[1]);
        Assert.Equal(masked, modeInfo.CompoundGroupIndex);
        Assert.Equal(compoundType != Av1CompoundType.DistanceWeighted, modeInfo.CompoundIndex);
        Assert.Equal(compoundType, modeInfo.CompoundType);
        Assert.Equal(compoundType == Av1CompoundType.Wedge ? 13 : 0, modeInfo.CompoundWedgeIndex);
        Assert.Equal(compoundType == Av1CompoundType.Wedge, modeInfo.CompoundWedgeSign);
        Assert.Equal(
            compoundType == Av1CompoundType.DifferenceWeighted
                ? Av1DifferenceWeightedMaskType.Type38Inverse
                : Av1DifferenceWeightedMaskType.Type38,
            modeInfo.DifferenceWeightedMaskType);

        Assert.Equal(Av1InterpolationFilter.Sharp, modeInfo.InterpolationFilters[0]);
        Assert.Equal(Av1InterpolationFilter.Sharp, modeInfo.InterpolationFilters[1]);
    }

    /// <summary>
    /// Invokes the ref-struct mode parser with one available above neighbor.
    /// </summary>
    /// <param name="tileReader">The tile reader.</param>
    /// <param name="encoded">The range-coded block-prefix symbols.</param>
    /// <param name="modeInfo">The current coding block.</param>
    /// <param name="aboveModeInfo">The available above block supplying skip-mode context.</param>
    /// <returns>The decoded block mode information.</returns>
    private static Av1BlockModeInfo ReadInterFrameModeInfo(
        Av1TileReader tileReader,
        Memory<byte> encoded,
        Av1BlockModeInfo modeInfo,
        Av1BlockModeInfo aboveModeInfo)
    {
        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None)
        {
            AvailableAbove = true,
            AboveModeInfo = aboveModeInfo,
        };

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Span, 0, updateCdf: true);
        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo, new Av1TileInfo(0, 0, tileReader.FrameHeader));
        return partitionInfo.ModeInfo;
    }

    /// <summary>
    /// Invokes the ref-struct mode parser without spatial neighbors.
    /// </summary>
    /// <returns>The decoded block mode information.</returns>
    private static Av1BlockModeInfo ReadInterFrameModeInfo(
        Av1TileReader tileReader,
        Memory<byte> encoded,
        Av1BlockModeInfo modeInfo)
    {
        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None);
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Span, 0, updateCdf: true);
        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo, new Av1TileInfo(0, 0, tileReader.FrameHeader));
        return partitionInfo.ModeInfo;
    }

    /// <summary>
    /// Writes one complete compound-reference tree leaf using the neutral no-neighbor contexts.
    /// </summary>
    private static void WriteCompoundReferencePair(Av1SymbolWriter writer, int pairIndex)
    {
        bool bidirectional = pairIndex >= 4;
        writer.WriteSymbol(bidirectional, Av1DefaultDistributions.CompoundReferenceType[2]);
        if (!bidirectional)
        {
            bool backwardPair = pairIndex == 0;
            writer.WriteSymbol(backwardPair, Av1DefaultDistributions.UnidirectionalCompoundReference[1][0]);
            if (!backwardPair)
            {
                bool last3OrGolden = pairIndex >= 2;
                writer.WriteSymbol(last3OrGolden, Av1DefaultDistributions.UnidirectionalCompoundReference[1][1]);
                if (last3OrGolden)
                {
                    writer.WriteSymbol(pairIndex == 3, Av1DefaultDistributions.UnidirectionalCompoundReference[1][2]);
                }
            }

            return;
        }

        bool last3OrGoldenForward = pairIndex >= 6;
        writer.WriteSymbol(last3OrGoldenForward, Av1DefaultDistributions.CompoundReference[1][0]);
        if (last3OrGoldenForward)
        {
            writer.WriteSymbol(pairIndex == 7, Av1DefaultDistributions.CompoundReference[1][2]);
        }
        else
        {
            writer.WriteSymbol(pairIndex == 5, Av1DefaultDistributions.CompoundReference[1][1]);
        }

        bool alternateBackward = pairIndex >= 6;
        writer.WriteSymbol(alternateBackward, Av1DefaultDistributions.CompoundBackwardReference[1][0]);
        if (!alternateBackward)
        {
            writer.WriteSymbol(pairIndex == 5, Av1DefaultDistributions.CompoundBackwardReference[1][1]);
        }
    }

    /// <summary>
    /// Creates the monochrome 64x64 sequence geometry used by direct mode-prefix tests.
    /// </summary>
    /// <returns>The initialized sequence header.</returns>
    private static ObuSequenceHeader CreateSequenceHeader()
        => new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            Use128x128Superblock = false,
            EnableCdef = false,
            EnableFilterIntra = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                BitDepth = Av1BitDepth.EightBit,
            },
        };

    /// <summary>
    /// Creates an inter-frame header whose optional block-prefix tools are disabled.
    /// </summary>
    /// <returns>The initialized frame header.</returns>
    private static ObuFrameHeader CreateFrameHeader()
    {
        ObuFrameHeader frameHeader = new()
        {
            FrameType = ObuFrameType.InterFrame,
            ModeInfoColumnCount = 16,
            ModeInfoRowCount = 16,
            CodedLossless = true,
            AllowScreenContentTools = false,
            FrameSize = new ObuFrameSize
            {
                FrameWidth = 64,
                FrameHeight = 64,
                SuperResolutionUpscaledWidth = 64,
                RenderWidth = 64,
                RenderHeight = 64,
            },
        };

        frameHeader.TilesInfo.TileColumnStartModeInfo[1] = frameHeader.ModeInfoColumnCount;
        frameHeader.TilesInfo.TileRowStartModeInfo[1] = frameHeader.ModeInfoRowCount;
        return frameHeader;
    }
}
