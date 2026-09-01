// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 intra-block-copy reference derivation and displacement-vector legality rules.
/// </summary>
[Trait("Format", "Heif")]
public class Av1IntraBlockCopyTests
{
    /// <summary>
    /// Verifies the horizontal fallback used in the tile's first superblock row.
    /// </summary>
    [Fact]
    public void FindReferenceUsesFirstRowFallback()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(new Point(5, 0));
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 80,
            RowIndex = 0,
        };

        Av1TileInfo tileInfo = CreateTileInfo();
        Av1MotionVector[] candidates = new Av1MotionVector[8];
        int[] weights = new int[8];

        Av1MotionVector actual = Av1IntraBlockCopy.FindReference(
            ref partitionInfo,
            tileInfo,
            sequenceHeader.SuperblockModeInfoSize,
            candidates,
            weights);

        Assert.Equal(new Av1MotionVector(0, -2560), actual);
    }

    /// <summary>
    /// Verifies the vertical fallback used after the tile's first superblock row when spatial candidates are absent.
    /// </summary>
    [Fact]
    public void FindReferenceUsesPreviousSuperblockRowFallback()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo aboveSuperblock = frameInfo.GetSuperblock(new Point(5, 0));
        Av1BlockModeInfo aboveModeInfo = new(Av1BlockSize.Block64x64, Point.Empty);
        frameInfo.UpdateModeInfo(aboveModeInfo, aboveSuperblock);

        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(new Point(5, 1));
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            AvailableAbove = true,
            ColumnIndex = 80,
            RowIndex = 16,
        };

        Av1TileInfo tileInfo = CreateTileInfo();
        Av1MotionVector[] candidates = new Av1MotionVector[8];
        int[] weights = new int[8];

        Av1MotionVector actual = Av1IntraBlockCopy.FindReference(
            ref partitionInfo,
            tileInfo,
            sequenceHeader.SuperblockModeInfoSize,
            candidates,
            weights);

        Assert.Equal(new Av1MotionVector(-512, 0), actual);
    }

    /// <summary>
    /// Verifies tile bounds, whole-sample precision, the four-block delay, and wavefront ordering.
    /// </summary>
    [Fact]
    public void IsValidEnforcesIntraBlockCopySourceRestrictions()
    {
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader();
        using Av1FrameInfo frameInfo = new(sequenceHeader);
        Av1SuperblockInfo superblockInfo = frameInfo.GetSuperblock(new Point(8, 2));
        Av1BlockModeInfo modeInfo = new(Av1BlockSize.Block16x16, Point.Empty);
        Av1PartitionInfo partitionInfo = new(modeInfo, superblockInfo, true, Av1PartitionType.None)
        {
            ColumnIndex = 128,
            RowIndex = 32,
        };

        Av1TileInfo tileInfo = CreateTileInfo();

        // A source five 64-sample columns earlier satisfies both the four-column delay and same-row wavefront limit.
        Assert.True(Av1IntraBlockCopy.IsValid(new Av1MotionVector(0, -2560), ref partitionInfo, tileInfo, sequenceHeader));

        // Moving the source one 64-sample column to the right reaches the forbidden delay boundary exactly.
        Assert.False(Av1IntraBlockCopy.IsValid(new Av1MotionVector(0, -2048), ref partitionInfo, tileInfo, sequenceHeader));
        Assert.False(Av1IntraBlockCopy.IsValid(new Av1MotionVector(0, -2559), ref partitionInfo, tileInfo, sequenceHeader));
        Assert.False(Av1IntraBlockCopy.IsValid(new Av1MotionVector(0, -4608), ref partitionInfo, tileInfo, sequenceHeader));
        Assert.False(Av1IntraBlockCopy.IsValid(new Av1MotionVector(512, -2560), ref partitionInfo, tileInfo, sequenceHeader));
    }

    /// <summary>
    /// Creates the 640-by-256, 4:2:0 sequence geometry shared by the displacement tests.
    /// </summary>
    private static ObuSequenceHeader CreateSequenceHeader()
        => new()
        {
            MaxFrameWidth = 640,
            MaxFrameHeight = 256,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = false,
                SubSamplingX = true,
                SubSamplingY = true,
                BitDepth = Av1BitDepth.EightBit,
            },
        };

    /// <summary>
    /// Creates one tile covering the complete test frame.
    /// </summary>
    private static Av1TileInfo CreateTileInfo()
    {
        ObuTileGroupHeader tilesInfo = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1,
        };

        tilesInfo.TileColumnStartModeInfo[1] = 160;
        tilesInfo.TileRowStartModeInfo[1] = 64;

        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = 160,
            ModeInfoRowCount = 64,
            TilesInfo = tilesInfo,
        };

        return new Av1TileInfo(0, 0, frameHeader);
    }
}
