// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the spatial, temporal, global, and extension rules used to derive single- and compound-reference AV1 motion vectors.
/// </summary>
[Trait("Format", "Avif")]
public class Av1ReferenceMotionVectorsTests
{
    /// <summary>
    /// Verifies adjacent-direction counting, duplicate weighting, stable ordering, and the nearest, near, and new-reference accessors.
    /// </summary>
    [Fact]
    public void BuildOrdersAdjacentCandidatesAndPacksModeContext()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(enableTemporalMotionVectors: false);
        ObuFrameHeader frameHeader = CreateFrameHeader(orderHint: 0, useReferenceFrameMotionVectors: false);
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        FillFrameWithIntraBlocks(frameInfo, sequenceHeader);

        Av1MotionVector above = new(24, -10);
        Av1MotionVector left = new(-14, 30);
        AddModeInfo(frameInfo, sequenceHeader, new Point(8, 4), Av1BlockSize.Block16x16, Av1ReferenceFrameType.Last, above, Av1PredictionMode.NewMotionVector);
        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(4, 8),
            Av1BlockSize.Block16x16,
            Av1ReferenceFrameType.Last,
            left,
            Av1PredictionMode.NearestMotionVector);

        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(12, 7),
            Av1BlockSize.Block4x4,
            Av1ReferenceFrameType.Last,
            above,
            Av1PredictionMode.NearestMotionVector);

        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, new Point(8, 8));
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 8,
            RowIndex = 8,
        };

        Av1TileInfo tileInfo = new(0, 0, frameHeader);
        partitionInfo.ComputeBoundaryOffsets(sequenceHeader, frameHeader, tileInfo);
        Av1ReferenceMotionVectors referenceMotionVectors = new();

        referenceMotionVectors.Build(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        Assert.Equal(2, referenceMotionVectors.Count);
        Assert.Equal(84, referenceMotionVectors.ModeContext);
        Assert.Equal(above, referenceMotionVectors.Candidates[0]);
        Assert.Equal(left, referenceMotionVectors.Candidates[1]);
        Assert.Equal((ushort)660, referenceMotionVectors.Weights[0]);
        Assert.Equal((ushort)656, referenceMotionVectors.Weights[1]);
        Assert.Equal(above, referenceMotionVectors.Nearest);
        Assert.Equal(left, referenceMotionVectors.GetNearReference(0));
        Assert.Equal(above, referenceMotionVectors.GetNewReference(0));
    }

    /// <summary>
    /// Verifies that outer candidates are weight-sorted independently without crossing the nearest-region boundary.
    /// </summary>
    [Fact]
    public void BuildSortsOuterCandidatesInsideTheirOwnRegion()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(enableTemporalMotionVectors: false);
        ObuFrameHeader frameHeader = CreateFrameHeader(orderHint: 0, useReferenceFrameMotionVectors: false);
        Av1FrameInfo frameInfo = new(sequenceHeader);
        FillFrameWithIntraBlocks(frameInfo, sequenceHeader);

        Av1MotionVector nearest = new(8, 16);
        Av1MotionVector topLeft = new(24, 32);
        Av1MotionVector outerRow = new(40, 48);
        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(8, 7),
            Av1BlockSize.Block4x4,
            Av1ReferenceFrameType.Last,
            nearest,
            Av1PredictionMode.NearestMotionVector);

        // A 4x4 intra neighbor keeps the adjacent scan from marking the deeper row as covered by a large background block.
        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(9, 7),
            Av1BlockSize.Block4x4,
            Av1ReferenceFrameType.Intra,
            default,
            Av1PredictionMode.DC);

        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(7, 7),
            Av1BlockSize.Block4x4,
            Av1ReferenceFrameType.Last,
            topLeft,
            Av1PredictionMode.NearestMotionVector);

        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(9, 3),
            Av1BlockSize.Block8x16,
            Av1ReferenceFrameType.Last,
            outerRow,
            Av1PredictionMode.NearestMotionVector);

        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block8x8, new Point(8, 8));
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 8,
            RowIndex = 8,
        };

        Av1TileInfo tileInfo = new(0, 0, frameHeader);
        partitionInfo.ComputeBoundaryOffsets(sequenceHeader, frameHeader, tileInfo);
        Av1ReferenceMotionVectors referenceMotionVectors = new();

        referenceMotionVectors.Build(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        Assert.Equal(3, referenceMotionVectors.Count);
        Assert.Equal(nearest, referenceMotionVectors.Candidates[0]);
        Assert.Equal(outerRow, referenceMotionVectors.Candidates[1]);
        Assert.Equal(topLeft, referenceMotionVectors.Candidates[2]);
        Assert.Equal((ushort)642, referenceMotionVectors.Weights[0]);
        Assert.Equal((ushort)8, referenceMotionVectors.Weights[1]);
        Assert.Equal((ushort)4, referenceMotionVectors.Weights[2]);
        Assert.Equal(51, referenceMotionVectors.ModeContext);
    }

    /// <summary>
    /// Verifies that an affine global-motion neighbor contributes the current block's global vector while extension retains its decoded vector.
    /// </summary>
    [Fact]
    public void BuildSubstitutesAffineGlobalMotionForDirectCandidate()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(enableTemporalMotionVectors: false);
        ObuFrameHeader frameHeader = CreateFrameHeader(orderHint: 0, useReferenceFrameMotionVectors: false);
        Av1GlobalMotionParameters globalMotion = Av1GlobalMotionParameters.Identity;
        globalMotion.Type = Av1GlobalMotionType.Affine;
        globalMotion[0] = 4096;
        globalMotion[1] = -2048;
        globalMotion[2] = Av1GlobalMotionParameters.ModelScale + 512;
        globalMotion[5] = Av1GlobalMotionParameters.ModelScale;
        frameHeader.GetGlobalMotionParameters()[0] = globalMotion;

        Av1FrameInfo frameInfo = new(sequenceHeader);
        FillFrameWithIntraBlocks(frameInfo, sequenceHeader);
        Av1MotionVector decoded = new(40, -24);
        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(8, 4),
            Av1BlockSize.Block16x16,
            Av1ReferenceFrameType.Last,
            decoded,
            Av1PredictionMode.GlobalMotionVector);

        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, new Point(8, 8));
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 8,
            RowIndex = 8,
        };

        Av1TileInfo tileInfo = new(0, 0, frameHeader);
        partitionInfo.ComputeBoundaryOffsets(sequenceHeader, frameHeader, tileInfo);
        Av1ReferenceMotionVectors referenceMotionVectors = new();

        referenceMotionVectors.Build(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        Av1MotionVector expectedGlobal = globalMotion.GetMotionVector(
            frameHeader.AllowHighPrecisionMotionVector,
            modeInfo.BlockSize,
            new Point(partitionInfo.ColumnIndex, partitionInfo.RowIndex),
            frameHeader.ForceIntegerMotionVector);

        Assert.Equal(2, referenceMotionVectors.Count);
        Assert.Equal(expectedGlobal, referenceMotionVectors.Candidates[0]);
        Assert.Equal(decoded, referenceMotionVectors.Candidates[1]);
        Assert.Equal((ushort)656, referenceMotionVectors.Weights[0]);
        Assert.Equal((ushort)2, referenceMotionVectors.Weights[1]);
    }

    /// <summary>
    /// Verifies that stack extension reverses an opposite-side vector without reweighting a candidate already in the
    /// direct stack.
    /// </summary>
    [Fact]
    public void BuildReversesOppositeDirectionExtensionCandidate()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(enableTemporalMotionVectors: false);
        sequenceHeader.OrderHintInfo.EnableOrderHint = true;
        sequenceHeader.OrderHintInfo.OrderHintBits = 5;
        ObuFrameHeader frameHeader = CreateFrameHeader(orderHint: 10, useReferenceFrameMotionVectors: false);
        frameHeader.GetReferenceFrameIndices()[0] = 0;
        frameHeader.GetReferenceFrameIndices()[4] = 1;

        using Av1ReferenceFrameStore referenceFrames = new();
        Av1ReferenceFrame past = CreateReferenceFrame(sequenceHeader, orderHint: 8);
        Av1ReferenceFrame future = CreateReferenceFrame(sequenceHeader, orderHint: 12);
        Assert.True(referenceFrames.Commit(1, past, showFrame: false));
        Assert.True(referenceFrames.Commit(2, future, showFrame: false));

        using Av1FrameInfo frameInfo = new(sequenceHeader);
        frameInfo.InitializeMotionField(Configuration.Default, sequenceHeader, frameHeader, referenceFrames);
        FillFrameWithIntraBlocks(frameInfo, sequenceHeader);
        Av1MotionVector direct = new(16, 24);
        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(8, 4),
            Av1BlockSize.Block16x16,
            Av1ReferenceFrameType.Last,
            direct,
            Av1PredictionMode.NearestMotionVector,
            Av1ReferenceFrameType.Backward,
            new Av1MotionVector(40, -24));

        // The direct scan adds the first reference with its normative adjacent weight. Extension visits both entries:
        // it must ignore that duplicate and append only the sign-corrected backward-reference vector.
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, new Point(8, 8));
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 8,
            RowIndex = 8,
        };

        Av1TileInfo tileInfo = new(0, 0, frameHeader);
        partitionInfo.ComputeBoundaryOffsets(sequenceHeader, frameHeader, tileInfo);
        Av1ReferenceMotionVectors referenceMotionVectors = new();

        referenceMotionVectors.Build(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        Av1MotionVector expected = new(-40, 24);
        Assert.Equal(2, referenceMotionVectors.Count);
        Assert.Equal(direct, referenceMotionVectors.Candidates[0]);
        Assert.Equal(expected, referenceMotionVectors.Candidates[1]);
        Assert.Equal((ushort)656, referenceMotionVectors.Weights[0]);
        Assert.Equal((ushort)2, referenceMotionVectors.Weights[1]);
        Assert.Equal(direct, referenceMotionVectors.Nearest);
        Assert.Equal(direct, referenceMotionVectors.GetNewReference(0));
        Assert.Equal(expected, referenceMotionVectors.GetNearReference(0));
    }

    /// <summary>
    /// Verifies temporal field sampling, candidate deduplication, accumulated weight, and the global-motion context bit.
    /// </summary>
    [Fact]
    public void BuildAccumulatesProjectedTemporalCandidates()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(enableTemporalMotionVectors: true);
        sequenceHeader.OrderHintInfo.EnableOrderHint = true;
        sequenceHeader.OrderHintInfo.OrderHintBits = 5;

        using Av1ReferenceFrameStore priorReferences = new();
        Av1ReferenceFrame prior = CreateReferenceFrame(sequenceHeader, orderHint: 6);
        Assert.True(priorReferences.Commit(1, prior, showFrame: false));

        ObuFrameHeader sourceHeader = CreateFrameHeader(orderHint: 8, useReferenceFrameMotionVectors: false);
        using Av1FrameInfo sourceFrameInfo = new(sequenceHeader);
        sourceFrameInfo.InitializeMotionField(Configuration.Default, sequenceHeader, sourceHeader, priorReferences);
        FillFrameWithInterBlocks(sourceFrameInfo, sequenceHeader, Av1ReferenceFrameType.Last, default);

        using Av1ReferenceFrameStore sourceReferences = new();
        Av1ReferenceFrame source = new(
            new Av1FrameBuffer<byte>(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false),
            sourceHeader,
            sourceFrameInfo);

        Assert.True(sourceReferences.Commit(1, source, showFrame: false));

        ObuFrameHeader frameHeader = CreateFrameHeader(orderHint: 10, useReferenceFrameMotionVectors: true);
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        frameInfo.InitializeMotionField(Configuration.Default, sequenceHeader, frameHeader, sourceReferences);
        FillFrameWithIntraBlocks(frameInfo, sequenceHeader);

        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, new Point(8, 8));
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 8,
            RowIndex = 8,
        };

        Av1TileInfo tileInfo = new(0, 0, frameHeader);
        partitionInfo.ComputeBoundaryOffsets(sequenceHeader, frameHeader, tileInfo);
        Av1ReferenceMotionVectors referenceMotionVectors = new();

        referenceMotionVectors.Build(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last);

        Assert.Equal(1, referenceMotionVectors.Count);
        Assert.Equal(default, referenceMotionVectors.Candidates[0]);
        Assert.Equal((ushort)14, referenceMotionVectors.Weights[0]);
        Assert.Equal(0, referenceMotionVectors.ModeContext);
    }

    /// <summary>
    /// Verifies that compound candidates retain their primary and secondary vectors through weighting, sorting, and DRL access.
    /// </summary>
    [Fact]
    public void BuildRetainsPairedCompoundCandidates()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(enableTemporalMotionVectors: false);
        ObuFrameHeader frameHeader = CreateFrameHeader(orderHint: 0, useReferenceFrameMotionVectors: false);
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        FillFrameWithIntraBlocks(frameInfo, sequenceHeader);

        Av1MotionVector abovePrimary = new(8, 16);
        Av1MotionVector aboveSecondary = new(24, 32);
        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(8, 4),
            Av1BlockSize.Block16x16,
            Av1ReferenceFrameType.Last,
            abovePrimary,
            Av1PredictionMode.NewNewMotionVector,
            Av1ReferenceFrameType.Backward,
            aboveSecondary);

        Av1MotionVector leftPrimary = new(40, 48);
        Av1MotionVector leftSecondary = new(56, 64);
        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(4, 8),
            Av1BlockSize.Block16x16,
            Av1ReferenceFrameType.Last,
            leftPrimary,
            Av1PredictionMode.NearestNearestMotionVector,
            Av1ReferenceFrameType.Backward,
            leftSecondary);

        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, new Point(8, 8));
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 8,
            RowIndex = 8,
        };

        Av1TileInfo tileInfo = new(0, 0, frameHeader);
        partitionInfo.ComputeBoundaryOffsets(sequenceHeader, frameHeader, tileInfo);
        Av1ReferenceMotionVectors referenceMotionVectors = new();

        referenceMotionVectors.Build(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.Backward);

        Assert.Equal(2, referenceMotionVectors.Count);
        Assert.Equal(abovePrimary, referenceMotionVectors.Candidates[0]);
        Assert.Equal(aboveSecondary, referenceMotionVectors.CompoundCandidates[0]);
        Assert.Equal(leftPrimary, referenceMotionVectors.Candidates[1]);
        Assert.Equal(leftSecondary, referenceMotionVectors.CompoundCandidates[1]);
        Assert.Equal((ushort)656, referenceMotionVectors.Weights[0]);
        Assert.Equal((ushort)656, referenceMotionVectors.Weights[1]);
        Assert.Equal(abovePrimary, referenceMotionVectors.GetCompoundNearestReference(0));
        Assert.Equal(aboveSecondary, referenceMotionVectors.GetCompoundNearestReference(1));
        Assert.Equal(leftPrimary, referenceMotionVectors.GetCompoundNearReference(0, 0));
        Assert.Equal(leftSecondary, referenceMotionVectors.GetCompoundNearReference(0, 1));
        Assert.Equal(abovePrimary, referenceMotionVectors.GetCompoundNewReference(0, 0));
        Assert.Equal(aboveSecondary, referenceMotionVectors.GetCompoundNewReference(0, 1));
    }

    /// <summary>
    /// Verifies the positional compound fallback assembled from independent exact-reference neighbor lists.
    /// </summary>
    [Fact]
    public void BuildExtendsCompoundStackWithPairedFallbacks()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(enableTemporalMotionVectors: false);
        ObuFrameHeader frameHeader = CreateFrameHeader(orderHint: 0, useReferenceFrameMotionVectors: false);
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        FillFrameWithIntraBlocks(frameInfo, sequenceHeader);

        Av1MotionVector above = new(8, 16);
        Av1MotionVector left = new(24, 32);
        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(8, 4),
            Av1BlockSize.Block16x16,
            Av1ReferenceFrameType.Last,
            above,
            Av1PredictionMode.NearestMotionVector);

        AddModeInfo(
            frameInfo,
            sequenceHeader,
            new Point(4, 8),
            Av1BlockSize.Block16x16,
            Av1ReferenceFrameType.Backward,
            left,
            Av1PredictionMode.NearestMotionVector);

        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(Point.Empty);
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, new Point(8, 8));
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 8,
            RowIndex = 8,
        };

        Av1TileInfo tileInfo = new(0, 0, frameHeader);
        partitionInfo.ComputeBoundaryOffsets(sequenceHeader, frameHeader, tileInfo);
        Av1ReferenceMotionVectors referenceMotionVectors = new();

        referenceMotionVectors.Build(
            ref partitionInfo,
            tileInfo,
            frameInfo,
            sequenceHeader,
            frameHeader,
            Av1ReferenceFrameType.Last,
            Av1ReferenceFrameType.Backward);

        Assert.Equal(2, referenceMotionVectors.Count);
        Assert.Equal(above, referenceMotionVectors.Candidates[0]);
        Assert.Equal(left, referenceMotionVectors.CompoundCandidates[0]);
        Assert.Equal(left, referenceMotionVectors.Candidates[1]);
        Assert.Equal(above, referenceMotionVectors.CompoundCandidates[1]);
        Assert.Equal((ushort)2, referenceMotionVectors.Weights[0]);
        Assert.Equal((ushort)2, referenceMotionVectors.Weights[1]);
    }

    /// <summary>
    /// Creates the monochrome 128-by-128 sequence geometry shared by reference-motion-vector tests.
    /// </summary>
    /// <param name="enableTemporalMotionVectors">Whether projected reference-frame motion vectors are enabled.</param>
    /// <returns>The configured sequence header.</returns>
    private static ObuSequenceHeader CreateSequenceHeader(bool enableTemporalMotionVectors)
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
            OrderHintInfo = new ObuOrderHintInfo
            {
                EnableReferenceFrameMotionVectors = enableTemporalMotionVectors,
            },
        };

    /// <summary>
    /// Creates one inter-frame header whose single tile covers the complete test frame.
    /// </summary>
    /// <param name="orderHint">The frame's modulo display-order hint.</param>
    /// <param name="useReferenceFrameMotionVectors">Whether this frame consumes its projected temporal motion field.</param>
    /// <returns>The configured frame header.</returns>
    private static ObuFrameHeader CreateFrameHeader(uint orderHint, bool useReferenceFrameMotionVectors)
        => new()
        {
            FrameType = ObuFrameType.InterFrame,
            OrderHint = orderHint,
            ModeInfoColumnCount = 32,
            ModeInfoRowCount = 32,
            AllowHighPrecisionMotionVector = true,
            UseReferenceFrameMotionVectors = useReferenceFrameMotionVectors,
            TilesInfo = new ObuTileGroupHeader
            {
                TileColumnCount = 1,
                TileRowCount = 1,
                TileColumnStartModeInfo = [0, 32],
                TileRowStartModeInfo = [0, 32],
            },
        };

    /// <summary>
    /// Maps one intra block over each 64-by-64 superblock so every spatial search position has initialized mode information.
    /// </summary>
    /// <param name="frameInfo">The frame map to initialize.</param>
    /// <param name="sequenceHeader">The sequence geometry defining the superblock grid.</param>
    private static void FillFrameWithIntraBlocks(Av1FrameInfo frameInfo, ObuSequenceHeader sequenceHeader)
    {
        for (int row = 0; row < 32; row += sequenceHeader.SuperblockModeInfoSize)
        {
            for (int column = 0; column < 32; column += sequenceHeader.SuperblockModeInfoSize)
            {
                AddModeInfo(
                    frameInfo,
                    sequenceHeader,
                    new Point(column, row),
                    Av1BlockSize.Block64x64,
                    Av1ReferenceFrameType.Intra,
                    default,
                    Av1PredictionMode.DC);
            }
        }
    }

    /// <summary>
    /// Maps one inter block over each 64-by-64 superblock and publishes its vector to the retained motion field.
    /// </summary>
    /// <param name="frameInfo">The frame map and retained field to initialize.</param>
    /// <param name="sequenceHeader">The sequence geometry defining the superblock grid.</param>
    /// <param name="referenceFrame">The canonical reference selected by each block.</param>
    /// <param name="motionVector">The retained motion vector.</param>
    private static void FillFrameWithInterBlocks(
        Av1FrameInfo frameInfo,
        ObuSequenceHeader sequenceHeader,
        Av1ReferenceFrameType referenceFrame,
        Av1MotionVector motionVector)
    {
        for (int row = 0; row < 32; row += sequenceHeader.SuperblockModeInfoSize)
        {
            for (int column = 0; column < 32; column += sequenceHeader.SuperblockModeInfoSize)
            {
                AddModeInfo(
                    frameInfo,
                    sequenceHeader,
                    new Point(column, row),
                    Av1BlockSize.Block64x64,
                    referenceFrame,
                    motionVector,
                    Av1PredictionMode.NearestMotionVector);
            }
        }
    }

    /// <summary>
    /// Creates and maps one mode-information block at a frame-relative position.
    /// </summary>
    /// <param name="frameInfo">The frame map that owns the block.</param>
    /// <param name="sequenceHeader">The sequence geometry defining superblock-relative addressing.</param>
    /// <param name="position">The block origin in frame-relative 4x4 units.</param>
    /// <param name="blockSize">The block geometry.</param>
    /// <param name="referenceFrame">The primary prediction reference.</param>
    /// <param name="motionVector">The primary motion vector.</param>
    /// <param name="predictionMode">The decoded luma or inter prediction mode.</param>
    /// <param name="secondaryReferenceFrame">The optional secondary prediction reference.</param>
    /// <param name="secondaryMotionVector">The optional secondary motion vector.</param>
    /// <returns>The mapped mode-information block.</returns>
    private static Av1BlockModeInfo AddModeInfo(
        Av1FrameInfo frameInfo,
        ObuSequenceHeader sequenceHeader,
        Point position,
        Av1BlockSize blockSize,
        Av1ReferenceFrameType referenceFrame,
        Av1MotionVector motionVector,
        Av1PredictionMode predictionMode,
        Av1ReferenceFrameType secondaryReferenceFrame = Av1ReferenceFrameType.None,
        Av1MotionVector secondaryMotionVector = default)
    {
        int superblockSize = sequenceHeader.SuperblockModeInfoSize;
        Point superblockPosition = new(position.X / superblockSize, position.Y / superblockSize);
        Point relativePosition = new(position.X % superblockSize, position.Y % superblockSize);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(superblockPosition);
        Av1BlockModeInfo modeInfo = new(blockSize, relativePosition)
        {
            YMode = predictionMode,
        };

        modeInfo.ReferenceFrames[0] = referenceFrame;
        modeInfo.ReferenceFrames[1] = secondaryReferenceFrame;
        modeInfo.MotionVectors[0] = motionVector;
        modeInfo.MotionVectors[1] = secondaryMotionVector;
        frameInfo.UpdateModeInfo(modeInfo, superblockInfo);
        superblockInfo.BlockCount++;
        return modeInfo;
    }

    /// <summary>
    /// Creates a retained monochrome frame at one display-order hint.
    /// </summary>
    /// <param name="sequenceHeader">The sequence geometry used by the retained frame.</param>
    /// <param name="orderHint">The retained frame's modulo display-order hint.</param>
    /// <returns>A frame owner whose sample buffer and mode state are ready for reference-map ownership.</returns>
    private static Av1ReferenceFrame CreateReferenceFrame(ObuSequenceHeader sequenceHeader, uint orderHint)
    {
        ObuFrameHeader frameHeader = CreateFrameHeader(orderHint, useReferenceFrameMotionVectors: false);
        Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false);
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        return new Av1ReferenceFrame(frameBuffer, frameHeader, frameInfo);
    }
}
