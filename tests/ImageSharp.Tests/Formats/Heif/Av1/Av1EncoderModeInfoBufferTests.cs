// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

public class Av1EncoderModeInfoBufferTests
{
    [Theory]
    [InlineData(false, 1024, 1024, 12_288)]
    [InlineData(true, 1024, 256, 6_144)]
    public void ConstructorMatchesLibaomAlignedModeInfoGeometry(
        bool disallow4x4,
        int expectedGridLength,
        int expectedAllocationLength,
        int expectedStorageLength)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        TestMemoryAllocator.AllocationRequest allocation;
        using (Av1EncoderModeInfoBuffer buffer = new(configuration, 65, 33, disallow4x4))
        {
            allocation = Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(typeof(byte), allocation.ElementType);
            Assert.Equal(AllocationOptions.Clean, allocation.AllocationOptions);
            Assert.Equal(expectedStorageLength, allocation.Length);
            Assert.Equal(18, buffer.ModeInfoColumnCount);
            Assert.Equal(10, buffer.ModeInfoRowCount);
            Assert.Equal(32, buffer.ModeInfoStride);
            Assert.Equal(disallow4x4, buffer.Disallow4x4AllFrames);
            Assert.Equal(expectedGridLength, buffer.Grid.Length);
            Assert.Equal(expectedAllocationLength, buffer.Allocation.Length);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.AllocationId, returned.AllocationId);
    }

    [Theory]
    [InlineData(false, 2, 3, 98)]
    [InlineData(true, 2, 2, 17)]
    public void PictureMappingUsesPackedAlignedStorage(
        bool disallow4x4,
        int column,
        int row,
        int expectedAllocationOffset)
    {
        using Av1EncoderModeInfoBuffer buffer = new(Configuration.Default, 65, 33, disallow4x4);
        Av1PictureControlSet picture = CreatePicture(buffer);
        Point position = new(column, row);
        ref Av1MacroBlockModeInfo modeInfo = ref picture.GetMacroBlockModeInfo(position);
        modeInfo.Block.Mode = Av1PredictionMode.Paeth;

        picture.MapModeInfoBlock(position, Av1BlockSize.Block8x8);

        Assert.Equal(Av1PredictionMode.Paeth, buffer.Allocation.Span[expectedAllocationOffset].Block.Mode);
        int alignedRowCount = buffer.Grid.Length / buffer.ModeInfoStride;
        for (int y = 0; y < alignedRowCount; y++)
        {
            for (int x = 0; x < buffer.ModeInfoStride; x++)
            {
                int gridOffset = (y * buffer.ModeInfoStride) + x;
                bool isMapped = y >= row && y < row + 2 && x >= column && x < column + 2;
                Assert.Equal(isMapped ? expectedAllocationOffset : 0, buffer.Grid.Span[gridOffset]);

                if (isMapped)
                {
                    Assert.Equal(Av1PredictionMode.Paeth, picture.GetFromModeInfoGrid(new Point(x, y)).Block.Mode);
                }
            }
        }
    }

    private static Av1PictureControlSet CreatePicture(Av1EncoderModeInfoBuffer buffer)
    {
        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        tiles.TileColumnStartModeInfo[1] = buffer.ModeInfoColumnCount;
        tiles.TileRowStartModeInfo[1] = buffer.ModeInfoRowCount;
        ObuSequenceHeader sequenceHeader = new();
        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = buffer.ModeInfoColumnCount,
            ModeInfoRowCount = buffer.ModeInfoRowCount,
            TilesInfo = tiles
        };

        return new Av1PictureControlSet
        {
            PartitionContexts = [],
            LuminanceDcSignLevelCoefficientNeighbors = [],
            CrDcSignLevelCoefficientNeighbors = [],
            CbDcSignLevelCoefficientNeighbors = [],
            TransformFunctionContexts = [],
            Sequence = new Av1SequenceControlSet { SequenceHeader = sequenceHeader },
            Parent = new Av1PictureParentControlSet
            {
                Common = new Av1EncoderCommon
                {
                    ModeInfoColumnCount = buffer.ModeInfoColumnCount,
                    ModeInfoRowCount = buffer.ModeInfoRowCount,
                    ModeInfoStride = buffer.ModeInfoStride,
                    FrameSize = new ObuFrameSize(),
                    TilesInfo = tiles
                },
                FrameHeader = frameHeader,
                PreviousQIndex = []
            },
            SegmentationNeighborMap = [],
            ModeInfoGrid = buffer.Grid,
            ModeInfoAllocation = buffer.Allocation,
            ModeInfoStride = buffer.ModeInfoStride,
            Disallow4x4AllFrames = buffer.Disallow4x4AllFrames,
            CdefPreset = []
        };
    }
}
