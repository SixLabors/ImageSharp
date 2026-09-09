// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the spatial neighbor and projection-sample rules used to select an AV1 motion mode.
/// </summary>
[Trait("Format", "Avif")]
public class Av1MotionVariationCandidatesTests
{
    /// <summary>
    /// Verifies that overlap detection exhausts the complete above edge before falling back to the complete left edge.
    /// </summary>
    [Fact]
    public void BuildScansCompleteAboveEdgeThenFallsBackToLeftEdge()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        using Av1FrameInfo frameInfo = new(sequenceHeader);

        for (int offset = 0; offset < 8; offset += 2)
        {
            AddModeInfo(
                frameInfo,
                sequenceHeader,
                new Point(4 + offset, 2),
                Av1BlockSize.Block8x8,
                Av1ReferenceFrameType.Intra,
                Av1ReferenceFrameType.None,
                default);

            Av1ReferenceFrameType leftReference = offset == 6
                ? Av1ReferenceFrameType.Last
                : Av1ReferenceFrameType.Intra;

            AddModeInfo(
                frameInfo,
                sequenceHeader,
                new Point(2, 4 + offset),
                Av1BlockSize.Block8x8,
                leftReference,
                Av1ReferenceFrameType.None,
                default);
        }

        Av1PartitionInfo partitionInfo = CreatePartitionInfo(
            frameInfo,
            sequenceHeader,
            new Point(4, 4),
            Av1BlockSize.Block32x32,
            availableAbove: true,
            availableLeft: true);

        Av1MotionVariationCandidates candidates = new();

        candidates.Build(
            ref partitionInfo,
            new Av1TileInfo(0, 0, frameHeader),
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        Assert.True(candidates.HasOverlappableNeighbor);
        Assert.Equal(1, candidates.Count);
    }

    /// <summary>
    /// Verifies that 4x4 neighbors use the second mode record of each horizontal or vertical 8-sample pair.
    /// </summary>
    [Fact]
    public void BuildUsesSecondCellForFourSampleNeighborPairs()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        using Av1FrameInfo horizontalFrameInfo = new(sequenceHeader);
        AddModeInfo(
            horizontalFrameInfo,
            sequenceHeader,
            new Point(4, 3),
            Av1BlockSize.Block4x4,
            Av1ReferenceFrameType.Intra,
            Av1ReferenceFrameType.None,
            default);

        AddModeInfo(
            horizontalFrameInfo,
            sequenceHeader,
            new Point(5, 3),
            Av1BlockSize.Block4x4,
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.None,
            default);

        Av1PartitionInfo horizontalPartition = CreatePartitionInfo(
            horizontalFrameInfo,
            sequenceHeader,
            new Point(4, 4),
            Av1BlockSize.Block8x8,
            availableAbove: true,
            availableLeft: false);

        Av1MotionVariationCandidates horizontalCandidates = new();
        horizontalCandidates.Build(
            ref horizontalPartition,
            new Av1TileInfo(0, 0, frameHeader),
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        using Av1FrameInfo verticalFrameInfo = new(sequenceHeader);
        AddModeInfo(
            verticalFrameInfo,
            sequenceHeader,
            new Point(3, 4),
            Av1BlockSize.Block4x4,
            Av1ReferenceFrameType.Intra,
            Av1ReferenceFrameType.None,
            default);

        AddModeInfo(
            verticalFrameInfo,
            sequenceHeader,
            new Point(3, 5),
            Av1BlockSize.Block4x4,
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.None,
            default);

        Av1PartitionInfo verticalPartition = CreatePartitionInfo(
            verticalFrameInfo,
            sequenceHeader,
            new Point(4, 4),
            Av1BlockSize.Block8x8,
            availableAbove: false,
            availableLeft: true);

        Av1MotionVariationCandidates verticalCandidates = new();
        verticalCandidates.Build(
            ref verticalPartition,
            new Av1TileInfo(0, 0, frameHeader),
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        Assert.True(horizontalCandidates.HasOverlappableNeighbor);
        Assert.True(verticalCandidates.HasOverlappableNeighbor);
    }

    /// <summary>
    /// Verifies that projection samples require a matching single reference and stop at the normative capacity of eight.
    /// </summary>
    [Fact]
    public void BuildCollectsMatchingSingleReferenceSamplesUpToCapacity()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        using Av1FrameInfo frameInfo = new(sequenceHeader);

        // The first candidate has the wrong primary reference and the second is compound. Ten following candidates
        // are eligible, so the retained range must begin at offset two and stop after eight samples at offset nine.
        for (int offset = 0; offset < 12; offset++)
        {
            Av1ReferenceFrameType primaryReference = offset == 0
                ? Av1ReferenceFrameType.Golden
                : Av1ReferenceFrameType.Last;

            Av1ReferenceFrameType secondaryReference = offset == 1
                ? Av1ReferenceFrameType.Golden
                : Av1ReferenceFrameType.None;

            AddModeInfo(
                frameInfo,
                sequenceHeader,
                new Point(8 + offset, 15),
                Av1BlockSize.Block4x4,
                primaryReference,
                secondaryReference,
                new Av1MotionVector(offset, offset + 1));
        }

        Av1PartitionInfo partitionInfo = CreatePartitionInfo(
            frameInfo,
            sequenceHeader,
            new Point(8, 16),
            Av1BlockSize.Block64x64,
            availableAbove: true,
            availableLeft: false);

        Av1MotionVariationCandidates candidates = new();

        candidates.Build(
            ref partitionInfo,
            new Av1TileInfo(0, 0, frameHeader),
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        Assert.Equal(8, candidates.Count);

        // Positions are Q3 neighbor centers relative to the current block. Reference points add the corresponding
        // Q3 motion vector without rounding, which also proves that the rejected first two candidates were skipped.
        Assert.Equal(new Point(72, -24), candidates.SourcePoints[0]);
        Assert.Equal(new Point(75, -22), candidates.ReferencePoints[0]);
        Assert.Equal(new Point(296, -24), candidates.SourcePoints[7]);
        Assert.Equal(new Point(306, -15), candidates.ReferencePoints[7]);
    }

    /// <summary>
    /// Verifies that eligible top-left and top-right diagonal blocks contribute after the direct edge neighbors.
    /// </summary>
    [Fact]
    public void BuildIncludesEligibleTopLeftAndTopRightSamples()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        AddModeInfo(frameInfo, sequenceHeader, new Point(4, 2), Av1BlockSize.Block8x8, Av1ReferenceFrameType.Last, Av1ReferenceFrameType.None, default);
        AddModeInfo(frameInfo, sequenceHeader, new Point(2, 4), Av1BlockSize.Block8x8, Av1ReferenceFrameType.Last, Av1ReferenceFrameType.None, default);
        AddModeInfo(frameInfo, sequenceHeader, new Point(2, 2), Av1BlockSize.Block8x8, Av1ReferenceFrameType.Last, Av1ReferenceFrameType.None, default);
        AddModeInfo(frameInfo, sequenceHeader, new Point(6, 2), Av1BlockSize.Block8x8, Av1ReferenceFrameType.Last, Av1ReferenceFrameType.None, default);

        Av1PartitionInfo partitionInfo = CreatePartitionInfo(
            frameInfo,
            sequenceHeader,
            new Point(4, 4),
            Av1BlockSize.Block8x8,
            availableAbove: true,
            availableLeft: true);

        Av1MotionVariationCandidates candidates = new();

        candidates.Build(
            ref partitionInfo,
            new Av1TileInfo(0, 0, frameHeader),
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        Assert.Equal(4, candidates.Count);
        Assert.Equal(new Point(24, -40), candidates.SourcePoints[0]);
        Assert.Equal(new Point(-40, 24), candidates.SourcePoints[1]);
        Assert.Equal(new Point(-40, -40), candidates.SourcePoints[2]);
        Assert.Equal(new Point(88, -40), candidates.SourcePoints[3]);
    }

    /// <summary>
    /// Verifies that edge blocks already covering the diagonal positions suppress duplicate top-left and top-right samples.
    /// </summary>
    [Fact]
    public void BuildSuppressesDiagonalSamplesCoveredByEdgeNeighbors()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        ObuFrameHeader frameHeader = CreateFrameHeader();
        using Av1FrameInfo frameInfo = new(sequenceHeader);

        // The aligned 16x8 above block covers the top-right position. The 8x16 left block begins two mode-info rows
        // above the current block and therefore covers its top-left position.
        AddModeInfo(frameInfo, sequenceHeader, new Point(4, 4), Av1BlockSize.Block16x8, Av1ReferenceFrameType.Last, Av1ReferenceFrameType.None, default);
        AddModeInfo(frameInfo, sequenceHeader, new Point(2, 4), Av1BlockSize.Block8x16, Av1ReferenceFrameType.Last, Av1ReferenceFrameType.None, default);

        Av1PartitionInfo partitionInfo = CreatePartitionInfo(
            frameInfo,
            sequenceHeader,
            new Point(4, 6),
            Av1BlockSize.Block8x8,
            availableAbove: true,
            availableLeft: true);

        Av1MotionVariationCandidates candidates = new();

        candidates.Build(
            ref partitionInfo,
            new Av1TileInfo(0, 0, frameHeader),
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        Assert.Equal(2, candidates.Count);
    }

    /// <summary>
    /// Creates the monochrome 128x128 sequence geometry used by spatial motion-mode tests.
    /// </summary>
    /// <returns>The initialized sequence header.</returns>
    private static ObuSequenceHeader CreateSequenceHeader()
        => new()
        {
            MaxFrameWidth = 128,
            MaxFrameHeight = 128,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                BitDepth = Av1BitDepth.EightBit,
            },
        };

    /// <summary>
    /// Creates a single-tile frame covering the complete sequence geometry.
    /// </summary>
    /// <returns>The initialized frame header.</returns>
    private static ObuFrameHeader CreateFrameHeader()
    {
        ObuTileGroupHeader tilesInfo = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1,
        };

        tilesInfo.TileColumnStartModeInfo[1] = 32;
        tilesInfo.TileRowStartModeInfo[1] = 32;

        return new()
        {
            FrameType = ObuFrameType.InterFrame,
            ModeInfoColumnCount = 32,
            ModeInfoRowCount = 32,
            TilesInfo = tilesInfo,
        };
    }

    /// <summary>
    /// Creates one current partition at a frame-relative mode-information position.
    /// </summary>
    /// <param name="frameInfo">The frame map containing the neighboring mode records.</param>
    /// <param name="sequenceHeader">The sequence geometry defining superblock-relative addressing.</param>
    /// <param name="position">The frame-relative block origin in 4x4 units.</param>
    /// <param name="blockSize">The current block geometry.</param>
    /// <param name="availableAbove">Whether the above edge is available.</param>
    /// <param name="availableLeft">Whether the left edge is available.</param>
    /// <returns>The initialized partition state.</returns>
    private static Av1PartitionInfo CreatePartitionInfo(
        Av1FrameInfo frameInfo,
        ObuSequenceHeader sequenceHeader,
        Point position,
        Av1BlockSize blockSize,
        bool availableAbove,
        bool availableLeft)
    {
        int superblockSize = sequenceHeader.SuperblockModeInfoSize;
        Point superblockPosition = new(position.X / superblockSize, position.Y / superblockSize);
        Point relativePosition = new(position.X % superblockSize, position.Y % superblockSize);
        Av1BlockModeInfo modeInfo = new(blockSize, relativePosition);

        return new Av1PartitionInfo(modeInfo, frameInfo.GetSuperblock(superblockPosition), false, Av1PartitionType.None)
        {
            ColumnIndex = position.X,
            RowIndex = position.Y,
            AvailableAbove = availableAbove,
            AvailableLeft = availableLeft,
        };
    }

    /// <summary>
    /// Creates and maps one decoded neighbor at a frame-relative mode-information position.
    /// </summary>
    /// <param name="frameInfo">The frame map that owns the neighbor.</param>
    /// <param name="sequenceHeader">The sequence geometry defining superblock-relative addressing.</param>
    /// <param name="position">The frame-relative block origin in 4x4 units.</param>
    /// <param name="blockSize">The neighboring block geometry.</param>
    /// <param name="primaryReference">The primary prediction reference.</param>
    /// <param name="secondaryReference">The optional secondary prediction reference.</param>
    /// <param name="motionVector">The primary motion vector in one-eighth-sample units.</param>
    private static void AddModeInfo(
        Av1FrameInfo frameInfo,
        ObuSequenceHeader sequenceHeader,
        Point position,
        Av1BlockSize blockSize,
        Av1ReferenceFrameType primaryReference,
        Av1ReferenceFrameType secondaryReference,
        Av1MotionVector motionVector)
    {
        int superblockSize = sequenceHeader.SuperblockModeInfoSize;
        Point superblockPosition = new(position.X / superblockSize, position.Y / superblockSize);
        Point relativePosition = new(position.X % superblockSize, position.Y % superblockSize);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(superblockPosition);
        Av1BlockModeInfo modeInfo = new(blockSize, relativePosition)
        {
            YMode = primaryReference == Av1ReferenceFrameType.Intra
                ? Av1PredictionMode.DC
                : Av1PredictionMode.NearestMotionVector,
        };

        modeInfo.ReferenceFrames[0] = primaryReference;
        modeInfo.ReferenceFrames[1] = secondaryReference;
        modeInfo.MotionVectors[0] = motionVector;
        frameInfo.UpdateModeInfo(modeInfo, superblockInfo);
    }
}
