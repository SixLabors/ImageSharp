// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
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
        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf: true);
        Span<byte> output = writer.GetTileBuffer();
        writer.WriteSymbol(ref output, true, skipMode);
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
        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf: true);
        Span<byte> output = writer.GetTileBuffer();
        writer.WriteSymbol(ref output, false, Av1DefaultDistributions.Skip[0]);
        writer.WriteSymbol(ref output, (int)Av1InterpolationFilter.Smooth, Av1DefaultDistributions.SwitchableInterpolation[3]);
        if (enableDualFilter)
        {
            writer.WriteSymbol(ref output, (int)Av1InterpolationFilter.Sharp, Av1DefaultDistributions.SwitchableInterpolation[11]);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);

        tileReader.ReadInterFrameModeInfo(ref decoder, ref partitionInfo, new Av1TileInfo(0, 0, frameHeader));
        modeInfo = partitionInfo.ModeInfo;

        Assert.Equal(Av1InterpolationFilter.Smooth, modeInfo.InterpolationFilters[0]);
        Assert.Equal((Av1InterpolationFilter)expectedHorizontalFilter, modeInfo.InterpolationFilters[1]);
    }

    /// <summary>
    /// Verifies that an inter block cannot predict from a reference outside the AV1 scaling range of the current frame.
    /// </summary>
    /// <remarks>
    /// AV1 permits a reference that is at most twice and at least one sixteenth of the coded frame size on each axis. The scaled predictor
    /// sizes its intermediate rows and the reference border for that range, so a reference outside it must be rejected before prediction.
    /// </remarks>
    /// <param name="referenceSize">The width and height of the reference frame for the 64x64 current frame.</param>
    /// <param name="isValid">Whether AV1 permits prediction from that reference.</param>
    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    [InlineData(4, true)]
    [InlineData(3, false)]
    public void ReadInterFrameModeInfoRejectsReferenceOutsideScalingRange(int referenceSize, bool isValid)
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();

        // Segment zero forces GLOBALMV. The block is then inter coded and selects the last-frame role without reference symbols. Every role
        // maps to slot zero, which holds the reference under test.
        ObuSegmentationParameters segmentationParameters = frameHeader.SegmentationParameters;
        segmentationParameters.Enabled = true;
        segmentationParameters.SetFeatureEnabled(0, (int)ObuSegmentationLevelFeature.GlobalMotionVector, true);

        ObuSequenceHeader referenceSequenceHeader = CreateSequenceHeader();
        referenceSequenceHeader.MaxFrameWidth = referenceSize;
        referenceSequenceHeader.MaxFrameHeight = referenceSize;
        using Av1ReferenceFrameStore referenceFrames = new();
        Av1FrameBuffer<byte> referenceFrameBuffer = new(Configuration.Default, referenceSequenceHeader, Av1ColorFormat.Yuv400, false);
        referenceFrames.Commit(0b0000_0001, new Av1ReferenceFrame(referenceFrameBuffer, new ObuFrameHeader()), showFrame: false);

        using Av1TileReader tileReader = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            new Av1FrameEntropyContexts(frameHeader.QuantizationParameters.BaseQIndex),
            null,
            referenceFrames);

        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf: true);
        Span<byte> output = writer.GetTileBuffer();
        writer.WriteSymbol(ref output, false, Av1DefaultDistributions.Skip[0]);
        using IMemoryOwner<byte> encoded = writer.Exit();
        Memory<byte> encodedMemory = encoded.Memory;
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);

        if (isValid)
        {
            modeInfo = ReadInterFrameModeInfo(tileReader, encodedMemory, modeInfo);
            Assert.Equal(Av1ReferenceFrameType.Last, modeInfo.ReferenceFrames[0]);
        }
        else
        {
            Assert.Throws<InvalidImageContentException>(() => ReadInterFrameModeInfo(tileReader, encodedMemory, modeInfo));
        }
    }

    /// <summary>
    /// Verifies every unidirectional and bidirectional compound reference-tree leaf through paired motion parsing.
    /// </summary>
    /// <param name="pairIndex">The zero-based normative compound reference pair.</param>
    /// <param name="expectedPrimary">The expected primary retained-reference label.</param>
    /// <param name="expectedSecondary">The expected secondary retained-reference label.</param>
    [Fact]
    public void ReadInterFrameModeInfoReadsCompoundReferencePair()
    {
        ReadInterFrameModeInfoReadsCompoundReferencePairCase(0, (int)Av1ReferenceFrameType.Backward, (int)Av1ReferenceFrameType.Alternate);
        ReadInterFrameModeInfoReadsCompoundReferencePairCase(1, (int)Av1ReferenceFrameType.Last, (int)Av1ReferenceFrameType.Last2);
        ReadInterFrameModeInfoReadsCompoundReferencePairCase(2, (int)Av1ReferenceFrameType.Last, (int)Av1ReferenceFrameType.Last3);
        ReadInterFrameModeInfoReadsCompoundReferencePairCase(3, (int)Av1ReferenceFrameType.Last, (int)Av1ReferenceFrameType.Golden);
        ReadInterFrameModeInfoReadsCompoundReferencePairCase(4, (int)Av1ReferenceFrameType.Last, (int)Av1ReferenceFrameType.Backward);
        ReadInterFrameModeInfoReadsCompoundReferencePairCase(5, (int)Av1ReferenceFrameType.Last2, (int)Av1ReferenceFrameType.Alternate2);
        ReadInterFrameModeInfoReadsCompoundReferencePairCase(6, (int)Av1ReferenceFrameType.Last3, (int)Av1ReferenceFrameType.Alternate);
        ReadInterFrameModeInfoReadsCompoundReferencePairCase(7, (int)Av1ReferenceFrameType.Golden, (int)Av1ReferenceFrameType.Alternate);
    }

    private static void ReadInterFrameModeInfoReadsCompoundReferencePairCase(
        int pairIndex,
        int expectedPrimary,
        int expectedSecondary)
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        frameHeader.ReferenceMode = ObuReferenceMode.ReferenceModeSelect;
        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, Point.Empty);
        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf: true);
        Span<byte> output = writer.GetTileBuffer();
        writer.WriteSymbol(ref output, false, Av1DefaultDistributions.Skip[0]);
        writer.WriteSymbol(ref output, true, Av1DefaultDistributions.IntraInter[0]);
        writer.WriteSymbol(ref output, true, Av1DefaultDistributions.CompInter[1]);
        WriteCompoundReferencePair(writer, ref output, pairIndex);
        writer.WriteSymbol(ref output, 0, Av1DefaultDistributions.InterCompoundMode[0]);

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
    private static void WriteCompoundReferencePair(Av1SymbolWriter writer, ref Span<byte> output, int pairIndex)
    {
        bool bidirectional = pairIndex >= 4;
        writer.WriteSymbol(ref output, bidirectional, Av1DefaultDistributions.CompoundReferenceType[2]);
        if (!bidirectional)
        {
            bool backwardPair = pairIndex == 0;
            writer.WriteSymbol(ref output, backwardPair, Av1DefaultDistributions.UnidirectionalCompoundReference[1][0]);
            if (!backwardPair)
            {
                bool last3OrGolden = pairIndex >= 2;
                writer.WriteSymbol(ref output, last3OrGolden, Av1DefaultDistributions.UnidirectionalCompoundReference[1][1]);
                if (last3OrGolden)
                {
                    writer.WriteSymbol(ref output, pairIndex == 3, Av1DefaultDistributions.UnidirectionalCompoundReference[1][2]);
                }
            }

            return;
        }

        bool last3OrGoldenForward = pairIndex >= 6;
        writer.WriteSymbol(ref output, last3OrGoldenForward, Av1DefaultDistributions.CompoundReference[1][0]);
        if (last3OrGoldenForward)
        {
            writer.WriteSymbol(ref output, pairIndex == 7, Av1DefaultDistributions.CompoundReference[1][2]);
        }
        else
        {
            writer.WriteSymbol(ref output, pairIndex == 5, Av1DefaultDistributions.CompoundReference[1][1]);
        }

        bool alternateBackward = pairIndex >= 6;
        writer.WriteSymbol(ref output, alternateBackward, Av1DefaultDistributions.CompoundBackwardReference[1][0]);
        if (!alternateBackward)
        {
            writer.WriteSymbol(ref output, pairIndex == 5, Av1DefaultDistributions.CompoundBackwardReference[1][1]);
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
