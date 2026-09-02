// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
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
    [InlineData(false, false, 336)]
    [InlineData(true, false, 3_536)]
    [InlineData(true, true, 6_416)]
    public unsafe void PictureBufferPacksAllPictureStateIntoTwoAllocatorOwners(
        bool allowScreenContentTools,
        bool allowIntraBlockCopy,
        int expectedStateStorageLength)
    {
        const int Width = 16;
        const int Height = 16;
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        tiles.TileColumnStartModeInfo[1] = Width >> Av1Constants.ModeInfoSizeLog2;
        tiles.TileRowStartModeInfo[1] = Height >> Av1Constants.ModeInfoSizeLog2;
        ObuSequenceHeader sequenceHeader = new()
        {
            Use128x128Superblock = true,
            ColorConfig = colorConfig
        };

        ObuFrameHeader frameHeader = new()
        {
            AllowScreenContentTools = allowScreenContentTools,
            AllowIntraBlockCopy = allowIntraBlockCopy,
            ModeInfoColumnCount = Width >> Av1Constants.ModeInfoSizeLog2,
            ModeInfoRowCount = Height >> Av1Constants.ModeInfoSizeLog2,
            TilesInfo = tiles
        };

        TestMemoryAllocator.AllocationRequest[] allocations;
        using (Av1EncoderPictureBuffer buffer = new(
            configuration,
            sequenceHeader,
            frameHeader,
            Width,
            Height))
        {
            allocations = allocator.AllocationLog.ToArray();
            Assert.Equal(2, allocations.Length);
            Assert.Equal(typeof(byte), allocations[0].ElementType);
            Assert.Equal(6_144, allocations[0].Length);
            Assert.Equal(AllocationOptions.Clean, allocations[0].AllocationOptions);
            Assert.Equal(typeof(byte), allocations[1].ElementType);
            Assert.Equal(expectedStateStorageLength, allocations[1].Length);
            Assert.Equal(AllocationOptions.Clean, allocations[1].AllocationOptions);
            Assert.Empty(allocator.ReturnLog);

            Av1PictureControlSet picture = buffer.Picture;
            Assert.Equal(16, picture.SegmentationNeighborMap.Length);
            Assert.Equal(32, picture.PartitionContexts[0].Left.Length);
            Assert.Equal(32, picture.PartitionContexts[0].Top.Length);
            Assert.Equal(32, picture.LuminanceDcSignLevelCoefficientNeighbors[0].Left.Length);
            Assert.Equal(32, picture.LuminanceDcSignLevelCoefficientNeighbors[0].Top.Length);
            Assert.Equal(16, picture.CbDcSignLevelCoefficientNeighbors[0].Left.Length);
            Assert.Equal(16, picture.CbDcSignLevelCoefficientNeighbors[0].Top.Length);
            Assert.Equal(32, picture.TransformFunctionContexts[0].Left.Length);
            Assert.Equal(32, picture.TransformFunctionContexts[0].Top.Length);
            if (allowScreenContentTools)
            {
                Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContext = Assert.Single(picture.PaletteContexts);
                Assert.Equal(32, paletteContext.Left.Length);
                Assert.Equal(32, paletteContext.Top.Length);
                Assert.Equal(Av1Constants.ModeInfoSizeLog2, paletteContext.GranularityNormalLog2);
                Assert.Equal(0, paletteContext.Left[0].PaletteSizes[0]);
                Assert.Equal(0, paletteContext.Top[^1].PaletteSizes[1]);

                // The typed palette region starts at its natural 16-bit alignment inside the shared byte owner.
                fixed (Av1EncoderPaletteInfo* pointer = paletteContext.Left)
                {
                    Assert.Equal((nuint)0, (nuint)pointer % (nuint)sizeof(ushort));
                }

                paletteContext.Left[0].PaletteSizes[0] = 3;
                Assert.Equal(0, paletteContext.Top[0].PaletteSizes[0]);
            }
            else
            {
                Assert.Empty(picture.PaletteContexts);
            }

            if (allowIntraBlockCopy)
            {
                Assert.Equal(256, picture.DisplacementVectors.Length);
                Assert.Equal(4, sizeof(Av1EncoderDisplacementVector));
                Assert.Equal(9, picture.IntraBlockCopySearch.OriginWidth);
                Assert.Equal(9, picture.IntraBlockCopySearch.OriginHeight);

                // The packed vector region starts at its natural 16-bit alignment inside the shared byte owner.
                fixed (Av1EncoderDisplacementVector* pointer = picture.DisplacementVectors.Span)
                {
                    Assert.Equal((nuint)0, (nuint)pointer % (nuint)sizeof(short));
                }

                Point position = new(2, 2);
                Av1MotionVector displacement = new(-16376, 16376);
                picture.MapModeInfoBlock(position, Av1BlockSize.Block8x8);
                picture.SetDisplacementVector(position, displacement);
                Assert.Equal(displacement, picture.GetDisplacementVector(new Point(3, 3)));
            }
            else
            {
                Assert.Equal(0, picture.DisplacementVectors.Length);
            }
        }

        Assert.Equal(2, allocator.ReturnLog.Count);
        Assert.Equal(
            allocations.Select(x => x.AllocationId).Order(),
            allocator.ReturnLog.Select(x => x.AllocationId).Order());
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
            SegmentationNeighborMap = Memory<byte>.Empty,
            ModeInfoGrid = buffer.Grid,
            ModeInfoAllocation = buffer.Allocation,
            ModeInfoStride = buffer.ModeInfoStride,
            Disallow4x4AllFrames = buffer.Disallow4x4AllFrames,
            CdefPreset = []
        };
    }
}
