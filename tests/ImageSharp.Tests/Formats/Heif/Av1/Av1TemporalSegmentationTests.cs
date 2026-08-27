// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 temporal segment-map prediction against libaom's decoder rules.
/// </summary>
[Trait("Format", "Avif")]
public class Av1TemporalSegmentationTests
{
    /// <summary>
    /// Verifies temporal segment-map prediction symbols through each of AV1's three neighbor contexts.
    /// </summary>
    /// <param name="context">The sum of predicted above and left neighbors.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SegmentIdPredictedRoundTrips(int context)
    {
        bool[] expected = [false, true, true, false, true, false];
        using Av1SymbolWriter writer = new(Configuration.Default, 1, updateCdf: true);
        Av1Distribution writerDistribution = Av1DefaultDistributions.SegmentIdPredicted[context];

        foreach (bool value in expected)
        {
            writer.WriteSymbol(value, writerDistribution);
        }

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);

        foreach (bool value in expected)
        {
            Assert.Equal(value, decoder.ReadSegmentIdPredicted(context));
        }
    }

    /// <summary>
    /// Verifies that the frame entropy graph copies adapted temporal-prediction state instead of restoring defaults.
    /// </summary>
    [Fact]
    public void FrameEntropyCopyRetainsAdaptedSegmentPrediction()
    {
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext destination = new(0);
        source.SegmentIdPredicted[2].Update(1);

        destination.CopyFrom(source);

        Assert.Equal(source.SegmentIdPredicted[2][0], destination.SegmentIdPredicted[2][0]);
        Assert.NotEqual(16384U, destination.SegmentIdPredicted[2][0]);
    }

    /// <summary>
    /// Verifies that only neighboring blocks which selected temporal prediction contribute to the binary CDF context.
    /// </summary>
    /// <param name="hasAbove">Whether an above block is available.</param>
    /// <param name="abovePredicted">Whether the available above block selected temporal prediction.</param>
    /// <param name="hasLeft">Whether a left block is available.</param>
    /// <param name="leftPredicted">Whether the available left block selected temporal prediction.</param>
    /// <param name="expected">The expected context in the inclusive range zero through two.</param>
    [Theory]
    [InlineData(false, false, false, false, 0)]
    [InlineData(true, false, true, false, 0)]
    [InlineData(true, true, false, false, 1)]
    [InlineData(false, false, true, true, 1)]
    [InlineData(true, true, true, true, 2)]
    public void SegmentPredictionContextCountsPredictedNeighbors(
        bool hasAbove,
        bool abovePredicted,
        bool hasLeft,
        bool leftPredicted,
        int expected)
    {
        Av1BlockModeInfo aboveModeInfo = hasAbove ? CreateModeInfo(abovePredicted) : null;
        Av1BlockModeInfo leftModeInfo = hasLeft ? CreateModeInfo(leftPredicted) : null;

        int actual = Av1SymbolContextHelper.GetSegmentIdPredictedContext(aboveModeInfo, leftModeInfo);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Verifies that a temporal-prediction symbol selects the minimum retained segment across the complete block and writes it to the current map.
    /// </summary>
    /// <param name="segmentIdPrecedesSkip">Whether segment syntax precedes the residual-skip flag.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadInterSegmentIdUsesRetainedPrimaryMap(bool segmentIdPrecedesSkip)
    {
        const int modeInfoSize = 16;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(64, 64);
        ObuFrameHeader primaryHeader = CreateFrameHeader(modeInfoSize, modeInfoSize, segmentationUpdateMap: 1, segmentationTemporalUpdate: 0);
        Av1FrameInfo primaryFrameInfo = new(sequenceHeader);
        primaryFrameInfo.InitializeSegmentIds(primaryHeader, null);
        primaryFrameInfo.SetSegmentId(Av1BlockSize.Block64x64, Point.Empty, 6);

        // The target 16x16 block covers sixteen 4x4 cells. One lower retained value proves that prediction scans the
        // complete clipped coverage rather than reading only the block origin.
        Point lowSegmentPosition = new(4, 4);
        primaryFrameInfo.SetSegmentId(Av1BlockSize.Block4x4, lowSegmentPosition, 2);

        // The production reference store owns complete reconstructed frames. A minimal monochrome frame buffer keeps
        // this test on the real ownership path while the assertions remain confined to retained segmentation state.
        Av1FrameBuffer<byte> primaryBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false);
        Av1ReferenceFrame primaryFrame = new(primaryBuffer, primaryHeader, primaryFrameInfo);
        using Av1ReferenceFrameStore referenceFrames = new();
        referenceFrames.Commit(1, primaryFrame, showFrame: false);

        ObuFrameHeader currentHeader = CreateFrameHeader(modeInfoSize, modeInfoSize, segmentationUpdateMap: 1, segmentationTemporalUpdate: 1);
        currentHeader.FrameType = ObuFrameType.InterFrame;
        currentHeader.PrimaryReferenceFrame = 0;
        currentHeader.PrimaryReferenceSlot = 0;
        currentHeader.SegmentationParameters.SegmentIdPrecedesSkip = segmentIdPrecedesSkip;
        Av1FrameEntropyContexts entropyContexts = new(0);
        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, currentHeader, entropyContexts, null, referenceFrames);

        Point blockPosition = new(2, 2);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, blockPosition);
        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None)
        {
            ColumnIndex = blockPosition.X,
            RowIndex = blockPosition.Y,
            AvailableAbove = true,
            AvailableLeft = true,
            AboveModeInfo = CreateModeInfo(predicted: true),
            LeftModeInfo = CreateModeInfo(predicted: false)
        };

        const int predictionContext = 1;
        using Av1SymbolWriter writer = new(Configuration.Default, 1, updateCdf: true);
        writer.WriteSymbol(true, Av1DefaultDistributions.SegmentIdPredicted[predictionContext]);
        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);

        tileReader.ReadInterSegmentId(ref decoder, ref partitionInfo, beforeSkip: segmentIdPrecedesSkip);

        Assert.True(modeInfo.SegmentIdPredicted);
        Assert.Equal(2, modeInfo.SegmentId);
        for (int row = blockPosition.Y; row < blockPosition.Y + modeInfo.BlockSize.Get4x4HighCount(); row++)
        {
            for (int column = blockPosition.X; column < blockPosition.X + modeInfo.BlockSize.Get4x4WideCount(); column++)
            {
                Assert.Equal(2, tileReader.FrameInfo.GetSegmentId(row, column));
            }
        }
    }

    /// <summary>
    /// Verifies that a skipped inter block uses the spatial predictor without reading a temporal-prediction symbol.
    /// </summary>
    [Fact]
    public void SkippedInterBlockClearsTemporalPredictionAndUsesSpatialSegment()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(64, 64);
        ObuFrameHeader frameHeader = CreateFrameHeader(16, 16, segmentationUpdateMap: 1, segmentationTemporalUpdate: 1);
        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);
        Point blockPosition = new(2, 2);

        // Three equal spatial neighbors select segment three without consuming a spatial segment symbol. The block is
        // initialized as predicted to prove that the normative skipped-block branch explicitly clears the stale flag.
        tileReader.FrameInfo.SetSegmentId(Av1BlockSize.Block4x4, new Point(1, 1), 3);
        tileReader.FrameInfo.SetSegmentId(Av1BlockSize.Block4x4, new Point(2, 1), 3);
        tileReader.FrameInfo.SetSegmentId(Av1BlockSize.Block4x4, new Point(1, 2), 3);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, blockPosition)
        {
            Skip = true,
            SegmentIdPredicted = true
        };

        Av1SuperblockInfo superblockInfo = new(tileReader.FrameInfo, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, false, Av1PartitionType.None)
        {
            ColumnIndex = blockPosition.X,
            RowIndex = blockPosition.Y,
            AvailableAbove = true,
            AvailableLeft = true
        };

        using Av1SymbolWriter writer = new(Configuration.Default, 1, updateCdf: true);
        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), 0, updateCdf: true);

        tileReader.ReadInterSegmentId(ref decoder, ref partitionInfo, beforeSkip: false);

        Assert.False(modeInfo.SegmentIdPredicted);
        Assert.Equal(3, modeInfo.SegmentId);
        Assert.Equal(3, tileReader.FrameInfo.GetSegmentId(blockPosition.Y, blockPosition.X));
    }

    /// <summary>
    /// Verifies that retained segmentation maps with different mode-info geometry are unavailable for temporal prediction.
    /// </summary>
    [Fact]
    public void PredictedSegmentIdIsZeroForMismatchedPrimaryGeometry()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(64, 64);
        ObuFrameHeader currentHeader = CreateFrameHeader(16, 16, segmentationUpdateMap: 1, segmentationTemporalUpdate: 1);
        ObuFrameHeader primaryHeader = CreateFrameHeader(8, 16, segmentationUpdateMap: 1, segmentationTemporalUpdate: 0);
        Av1FrameInfo currentFrameInfo = new(sequenceHeader);
        Av1FrameInfo primaryFrameInfo = new(sequenceHeader);
        currentFrameInfo.InitializeSegmentIds(currentHeader, null);
        primaryFrameInfo.InitializeSegmentIds(primaryHeader, null);
        primaryFrameInfo.SetSegmentId(Av1BlockSize.Block32x64, Point.Empty, 5);

        int actual = currentFrameInfo.GetPredictedSegmentId(primaryFrameInfo, Av1BlockSize.Block16x16, Point.Empty);

        Assert.Equal(0, actual);
    }

    /// <summary>
    /// Creates block mode state with the requested temporal segment-prediction flag.
    /// </summary>
    /// <param name="predicted">Whether the block selected its segment identifier from the retained map.</param>
    /// <returns>The initialized block mode state.</returns>
    private static Av1BlockModeInfo CreateModeInfo(bool predicted)
        => new(Av1BlockSize.Block4x4, Point.Empty) { SegmentIdPredicted = predicted };

    /// <summary>
    /// Creates the fixed 64x64-superblock sequence geometry used by segmentation-map tests.
    /// </summary>
    /// <param name="width">The maximum coded width in pixels.</param>
    /// <param name="height">The maximum coded height in pixels.</param>
    /// <returns>The initialized monochrome sequence header.</returns>
    private static ObuSequenceHeader CreateSequenceHeader(int width, int height)
        => new()
        {
            MaxFrameWidth = width,
            MaxFrameHeight = height,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                BitDepth = Av1BitDepth.EightBit
            }
        };

    /// <summary>
    /// Creates the frame geometry and segmentation controls used by direct map tests.
    /// </summary>
    /// <param name="modeInfoColumnCount">The active width in 4x4 mode-info units.</param>
    /// <param name="modeInfoRowCount">The active height in 4x4 mode-info units.</param>
    /// <param name="segmentationUpdateMap">Whether the frame updates its segment map.</param>
    /// <param name="segmentationTemporalUpdate">Whether map updates may select the retained primary map.</param>
    /// <returns>The initialized frame header.</returns>
    private static ObuFrameHeader CreateFrameHeader(
        int modeInfoColumnCount,
        int modeInfoRowCount,
        int segmentationUpdateMap,
        int segmentationTemporalUpdate)
        => new()
        {
            ModeInfoColumnCount = modeInfoColumnCount,
            ModeInfoRowCount = modeInfoRowCount,
            SegmentationParameters = new ObuSegmentationParameters
            {
                Enabled = true,
                LastActiveSegmentId = Av1Constants.MaxSegmentCount - 1,
                SegmentationUpdateMap = segmentationUpdateMap,
                SegmentationTemporalUpdate = segmentationTemporalUpdate
            }
        };
}
