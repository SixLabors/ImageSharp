// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
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
        int expectedContextStorageLength)
    {
        const int Width = 16;
        const int Height = 16;

        // Four CDEF presets, the preceding quantizer, and two payload bounds follow the context regions.
        const int TileStateStorageLength = 7 * sizeof(int);
        const int AllocatedBlockCount = 256;
        const int BlockEncodingStorageLength = AllocatedBlockCount * 8;
        const int BlockPaletteStorageLength = AllocatedBlockCount * 50;
        const int PaletteTokenStorageLength = 2 * 128 * 128;

        // Four vectors (16 bytes), four weights (8), mode context (2), count (1), and one alignment byte.
        const int ReferenceContextStorageLength = AllocatedBlockCount * 28;

        // Retained syntax uses fixed-width entries at the existing block origins. Palette tokens reserve
        // two complete maximum-superblock planes, including coded padding beyond this small visible frame.
        int retainedStorageLength = BlockEncodingStorageLength +
            (allowScreenContentTools ? BlockPaletteStorageLength + PaletteTokenStorageLength : 0) +
            (allowIntraBlockCopy ? ReferenceContextStorageLength : 0);

        int expectedTileStateOffset = expectedContextStorageLength + retainedStorageLength;
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
            Height,
            disallow4x4AllFrames: true))
        {
            allocations = allocator.AllocationLog.ToArray();
            Assert.Equal(2, allocations.Length);
            Assert.Equal(typeof(byte), allocations[0].ElementType);
            Assert.Equal(6_144, allocations[0].Length);
            Assert.Equal(AllocationOptions.Clean, allocations[0].AllocationOptions);
            Assert.Equal(typeof(byte), allocations[1].ElementType);
            Assert.Equal(expectedTileStateOffset + TileStateStorageLength, allocations[1].Length);
            Assert.Equal(AllocationOptions.Clean, allocations[1].AllocationOptions);
            Assert.Empty(allocator.ReturnLog);

            Av1PictureControlSet picture = buffer.Picture;
            Assert.Equal(AllocatedBlockCount, picture.BlockEncodings.Length);
            Assert.Equal(8, sizeof(Av1EncoderBlockStruct));
            Assert.Equal(28, sizeof(Av1EncoderReferenceContext));
            Assert.Equal(-1, MemoryMarshal.AsBytes(picture.BlockEncodings.Span).IndexOfAnyExcept((byte)0));
            Assert.Equal(16, picture.SegmentationNeighborMap.Length);
            Assert.Equal(32, picture.PartitionContexts[0].Left.Length);
            Assert.Equal(32, picture.PartitionContexts[0].Top.Length);
            Assert.Equal(32, picture.LuminanceDcSignLevelCoefficientNeighbors[0].Left.Length);
            Assert.Equal(32, picture.LuminanceDcSignLevelCoefficientNeighbors[0].Top.Length);
            Assert.Equal(16, picture.CbDcSignLevelCoefficientNeighbors[0].Left.Length);
            Assert.Equal(16, picture.CbDcSignLevelCoefficientNeighbors[0].Top.Length);
            Assert.Equal(32, picture.TransformFunctionContexts[0].Left.Length);
            Assert.Equal(32, picture.TransformFunctionContexts[0].Top.Length);
            Assert.Equal(4, picture.CdefPreset.Length);
            Assert.Equal(1, picture.Parent.PreviousQIndex.Length);
            Assert.Equal(1, picture.TileDataOffsets.Length);
            Assert.Equal(1, picture.TileDataLengths.Length);

            // Exact offsets prove that all four typed views occupy the trailing region of the same owner,
            // without gaps, overlapping fields, or a separate allocation hidden behind a memory manager.
            fixed (byte* state = picture.SegmentationNeighborMap.Span)
            {
                fixed (int* cdef = picture.CdefPreset.Span,
                    quantizer = picture.Parent.PreviousQIndex.Span,
                    offsets = picture.TileDataOffsets.Span,
                    lengths = picture.TileDataLengths.Span)
                {
                    Assert.Equal((nuint)0, (nuint)cdef % (nuint)sizeof(int));
                    Assert.Equal(expectedTileStateOffset, (byte*)cdef - state);
                    Assert.Equal(4, quantizer - cdef);
                    Assert.Equal(1, offsets - quantizer);
                    Assert.Equal(1, lengths - offsets);
                    Assert.Equal(allocations[1].Length, (byte*)(lengths + 1) - state);
                }
            }

            if (allowScreenContentTools)
            {
                Assert.Equal(AllocatedBlockCount, picture.BlockPalettes.Length);
                Assert.Equal(PaletteTokenStorageLength, picture.PaletteTokens.Length);
                Assert.Equal(-1, MemoryMarshal.AsBytes(picture.BlockPalettes.Span).IndexOfAnyExcept((byte)0));
                Assert.Equal(0, picture.PaletteTokens.Span[^1]);
                fixed (byte* tokens = picture.PaletteTokens.Span)
                {
                    fixed (Av1EncoderPaletteInfo* palettes = picture.BlockPalettes.Span)
                    {
                        fixed (Av1EncoderBlockStruct* encodings = picture.BlockEncodings.Span)
                        {
                            Assert.Equal(BlockPaletteStorageLength, tokens - (byte*)palettes);
                            Assert.Equal(PaletteTokenStorageLength, (byte*)encodings - tokens);
                        }
                    }
                }

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
                Assert.True(picture.BlockPalettes.IsEmpty);
                Assert.True(picture.PaletteTokens.IsEmpty);
            }

            if (allowIntraBlockCopy)
            {
                Assert.Equal(AllocatedBlockCount, picture.ReferenceContexts.Length);
                Assert.Equal(-1, MemoryMarshal.AsBytes(picture.ReferenceContexts.Span).IndexOfAnyExcept((byte)0));
                fixed (Av1EncoderDisplacementVector* vectors = picture.DisplacementVectors.Span)
                {
                    fixed (Av1EncoderReferenceContext* references = picture.ReferenceContexts.Span)
                    {
                        fixed (Av1EncoderBlockStruct* encodings = picture.BlockEncodings.Span)
                        {
                            Assert.Equal(BlockEncodingStorageLength, (byte*)vectors - (byte*)encodings);
                            Assert.Equal(AllocatedBlockCount * 4, (byte*)references - (byte*)vectors);
                        }
                    }
                }

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
                Assert.True(picture.ReferenceContexts.IsEmpty);
            }
        }

        Assert.Equal(2, allocator.ReturnLog.Count);
        Assert.Equal(
            allocations.Select(x => x.AllocationId).Order(),
            allocator.ReturnLog.Select(x => x.AllocationId).Order());
    }

    [Fact]
    public void InterPictureBufferExposesPackedMotionVectorStorage()
    {
        const int Width = 16;
        const int Height = 16;
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
        ObuSequenceHeader sequenceHeader = new() { ColorConfig = colorConfig };
        ObuFrameHeader frameHeader = new()
        {
            FrameType = ObuFrameType.InterFrame,
            ModeInfoColumnCount = Width >> Av1Constants.ModeInfoSizeLog2,
            ModeInfoRowCount = Height >> Av1Constants.ModeInfoSizeLog2,
            TilesInfo = tiles
        };

        using Av1EncoderPictureBuffer buffer = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true);

        Av1PictureControlSet picture = buffer.Picture;
        Assert.Equal(256, picture.DisplacementVectors.Length);
        Assert.Equal(0, picture.IntraBlockCopySearch.OriginWidth);

        Point position = new(2, 2);
        Av1MotionVector vector = new(-32, 40);
        picture.MapModeInfoBlock(position, Av1BlockSize.Block8x8);
        picture.SetDisplacementVector(position, vector);
        Assert.Equal(vector, picture.GetDisplacementVector(new Point(3, 3)));
    }

    [Fact]
    public void PictureBufferResetReusesStorageAndRestoresFrameState()
    {
        const int Width = 16;
        const int Height = 16;
        const int InitialQIndex = 37;
        const int NextQIndex = 91;
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

        ObuTileGroupHeader initialTiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        initialTiles.TileColumnStartModeInfo[1] = Width >> Av1Constants.ModeInfoSizeLog2;
        initialTiles.TileRowStartModeInfo[1] = Height >> Av1Constants.ModeInfoSizeLog2;
        ObuSequenceHeader sequenceHeader = new()
        {
            Use128x128Superblock = true,
            ColorConfig = colorConfig
        };

        ObuFrameHeader initialFrameHeader = new()
        {
            FrameType = ObuFrameType.KeyFrame,
            ModeInfoColumnCount = Width >> Av1Constants.ModeInfoSizeLog2,
            ModeInfoRowCount = Height >> Av1Constants.ModeInfoSizeLog2,
            TilesInfo = initialTiles
        };

        initialFrameHeader.QuantizationParameters.BaseQIndex = InitialQIndex;
        using Av1EncoderPictureBuffer buffer = new(
            configuration,
            sequenceHeader,
            initialFrameHeader,
            Width,
            Height,
            disallow4x4AllFrames: true,
            allocateScreenContentState: true,
            allocateMotionVectorState: true,
            allocateIntraBlockCopySearch: true);

        Av1PictureControlSet picture = buffer.Picture;
        int allocationCount = allocator.AllocationLog.Count;
        picture.ModeInfoGrid.Span[0] = 7;
        picture.ModeInfoAllocation.Span[0].Block.Mode = Av1PredictionMode.Paeth;
        picture.SegmentationNeighborMap.Span[0] = 3;
        picture.PartitionContexts[0].Left[0] = new Av1PartitionContext(5, 7);
        picture.TransformFunctionContexts[0].Top[0] = 8;
        picture.PaletteContexts[0].Left[0].PaletteSizes[0] = 2;
        picture.DisplacementVectors.Span[0] = new Av1EncoderDisplacementVector { Row = -8, Column = 16 };
        picture.BlockEncodings.Span[0].QuantizationIndex = 53;
        picture.BlockPalettes.Span[0].PaletteSizes[0] = 3;
        picture.PaletteTokens.Span[0] = 0x42;
        picture.ReferenceContexts.Span[0].ModeContext = 37;
        picture.CdefPreset.Span[0] = 2;
        picture.Parent.PreviousQIndex.Span[0] = InitialQIndex + 1;
        picture.TileDataOffsets.Span[0] = 11;
        picture.TileDataLengths.Span[0] = 13;
        ObuTileGroupHeader nextTiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        nextTiles.TileColumnStartModeInfo[1] = Width >> Av1Constants.ModeInfoSizeLog2;
        nextTiles.TileRowStartModeInfo[1] = Height >> Av1Constants.ModeInfoSizeLog2;
        ObuFrameHeader nextFrameHeader = new()
        {
            FrameType = ObuFrameType.InterFrame,
            ModeInfoColumnCount = Width >> Av1Constants.ModeInfoSizeLog2,
            ModeInfoRowCount = Height >> Av1Constants.ModeInfoSizeLog2,
            TilesInfo = nextTiles
        };

        nextFrameHeader.QuantizationParameters.BaseQIndex = NextQIndex;
        buffer.Reset(nextFrameHeader);

        Assert.Equal(allocationCount, allocator.AllocationLog.Count);
        Assert.Empty(allocator.ReturnLog);
        Assert.Equal(0, picture.ModeInfoGrid.Span[0]);
        Assert.Equal(Av1PredictionMode.DC, picture.ModeInfoAllocation.Span[0].Block.Mode);
        Assert.Equal(0, picture.SegmentationNeighborMap.Span[0]);
        Assert.Equal(default, picture.PartitionContexts[0].Left[0]);
        Assert.Equal(Av1Constants.MaxTransformSize, picture.TransformFunctionContexts[0].Top[0]);
        Assert.Equal(0, picture.PaletteContexts[0].Left[0].PaletteSizes[0]);
        Assert.Equal(default, picture.DisplacementVectors.Span[0]);
        Assert.Equal(-1, MemoryMarshal.AsBytes(picture.BlockEncodings.Span).IndexOfAnyExcept((byte)0));
        Assert.Equal(-1, MemoryMarshal.AsBytes(picture.BlockPalettes.Span).IndexOfAnyExcept((byte)0));
        Assert.Equal(0, picture.PaletteTokens.Span[0]);
        Assert.Equal(-1, MemoryMarshal.AsBytes(picture.ReferenceContexts.Span).IndexOfAnyExcept((byte)0));
        Assert.Equal(-1, picture.CdefPreset.Span[0]);
        Assert.Equal(NextQIndex, picture.Parent.PreviousQIndex.Span[0]);
        Assert.Equal(0, picture.TileDataOffsets.Span[0]);
        Assert.Equal(0, picture.TileDataLengths.Span[0]);
        Assert.Same(nextFrameHeader, picture.Parent.FrameHeader);
        Assert.Same(nextTiles, picture.Parent.Common.TilesInfo);

        picture.ModeInfoAllocation.Span[0].Block.Mode = Av1PredictionMode.Paeth;
        picture.BlockEncodings.Span[0].QuantizationIndex = 53;
        picture.BlockPalettes.Span[0].PaletteSizes[0] = 3;
        picture.PaletteTokens.Span[0] = 0x42;
        picture.ReferenceContexts.Span[0].ModeContext = 37;
        picture.LuminanceDcSignLevelCoefficientNeighbors[0].Top[0] = 0x41;
        picture.TransformFunctionContexts[0].Left[0] = 4;
        picture.ResetEntropyContexts();

        Assert.Equal(allocationCount, allocator.AllocationLog.Count);
        Assert.Empty(allocator.ReturnLog);
        Assert.Equal(Av1PredictionMode.Paeth, picture.ModeInfoAllocation.Span[0].Block.Mode);
        Assert.Equal(53, picture.BlockEncodings.Span[0].QuantizationIndex);
        Assert.Equal(3, picture.BlockPalettes.Span[0].PaletteSizes[0]);
        Assert.Equal(0x42, picture.PaletteTokens.Span[0]);
        Assert.Equal(37, picture.ReferenceContexts.Span[0].ModeContext);
        Assert.Equal(0, picture.LuminanceDcSignLevelCoefficientNeighbors[0].Top[0]);
        Assert.Equal(Av1Constants.MaxTransformSize, picture.TransformFunctionContexts[0].Left[0]);
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
                PreviousQIndex = Memory<int>.Empty
            },
            SegmentationNeighborMap = Memory<byte>.Empty,
            ModeInfoGrid = buffer.Grid,
            ModeInfoAllocation = buffer.Allocation,
            ModeInfoStride = buffer.ModeInfoStride,
            Disallow4x4AllFrames = buffer.Disallow4x4AllFrames,
            CdefPreset = Memory<int>.Empty,
            TileDataOffsets = Memory<int>.Empty,
            TileDataLengths = Memory<int>.Empty
        };
    }
}
