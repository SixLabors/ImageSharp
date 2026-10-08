// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    [InlineData(true, 1024, 256)]
    public void ConstructorMatchesLibaomAlignedModeInfoGeometry(
        bool disallow4x4,
        int expectedGridLength,
        int expectedAllocationLength)
    {
        // One owner holds the integer grid followed by the mode entries, with no other storage.
        int expectedStorageLength = (expectedGridLength * sizeof(int)) +
            (expectedAllocationLength * Unsafe.SizeOf<Av1MacroBlockModeInfo>());

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
        Assert.Equal(allocation.HashCodeOfBuffer, returned.HashCodeOfBuffer);
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
            1 << sequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: true,
            allocateScreenContentState: true,
            allocateMotionVectorState: true,
            allocateIntraBlockCopySearch: true);

        Av1PictureControlSet picture = buffer.Picture;
        int allocationCount = allocator.AllocationLog.Count;
        picture.ModeInfoGrid.Span[0] = 7;
        picture.ModeInfoAllocation.Span[0].Block.Mode = Av1PredictionMode.Paeth;
        picture.SegmentationNeighborMap.Span[0] = 3;
        picture.PartitionContexts[0].GetEdges().Left[0] = new Av1PartitionContext(5, 7);
        picture.TransformFunctionContexts[0].GetEdges().Top[0] = 8;
        picture.PaletteContexts[0].GetEdges().Left[0].PaletteSizes[0] = 2;
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
        Assert.Equal(default, picture.PartitionContexts[0].GetEdges().Left[0]);
        Assert.Equal(Av1Constants.MaxTransformSize, picture.TransformFunctionContexts[0].GetEdges().Top[0]);
        Assert.Equal(0, picture.PaletteContexts[0].GetEdges().Left[0].PaletteSizes[0]);
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
        picture.LuminanceDcSignLevelCoefficientNeighbors[0].GetEdges().Top[0] = 0x41;
        picture.TransformFunctionContexts[0].GetEdges().Left[0] = 4;
        picture.ResetEntropyContexts();

        Assert.Equal(allocationCount, allocator.AllocationLog.Count);
        Assert.Empty(allocator.ReturnLog);
        Assert.Equal(Av1PredictionMode.Paeth, picture.ModeInfoAllocation.Span[0].Block.Mode);
        Assert.Equal(53, picture.BlockEncodings.Span[0].QuantizationIndex);
        Assert.Equal(3, picture.BlockPalettes.Span[0].PaletteSizes[0]);
        Assert.Equal(0x42, picture.PaletteTokens.Span[0]);
        Assert.Equal(37, picture.ReferenceContexts.Span[0].ModeContext);
        Assert.Equal(0, picture.LuminanceDcSignLevelCoefficientNeighbors[0].GetEdges().Top[0]);
        Assert.Equal(Av1Constants.MaxTransformSize, picture.TransformFunctionContexts[0].GetEdges().Left[0]);
    }
}
