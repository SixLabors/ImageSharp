// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1CoefficientsEntropyTests
{
    private const int BaseQIndex = 23;

    [Fact]
    public void NeighborArrayWritesEveryCoveredFourByFourEdgeUnit()
    {
        using Av1NeighborArrayUnit<byte> neighbors = new(
            Configuration.Default,
            leftSize: 8,
            topSize: 8)
        {
            GranularityNormalLog2 = 2
        };

        neighbors.UnitModeWrite(
            37,
            new Point(8, 4),
            new Size(16, 8),
            Av1NeighborArrayUnit<byte>.UnitMask.Top | Av1NeighborArrayUnit<byte>.UnitMask.Left);

        Assert.Equal(new byte[] { 0, 0, 37, 37, 37, 37, 0, 0 }, neighbors.Top.ToArray());
        Assert.Equal(new byte[] { 0, 37, 37, 0, 0, 0, 0, 0 }, neighbors.Left.ToArray());
    }

    [Fact]
    public void NeighborArrayOwnsOnlyLeftAndTopContexts()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        TestMemoryAllocator.AllocationRequest allocation;
        using (Av1NeighborArrayUnit<byte> neighbors = new(configuration, leftSize: 8, topSize: 12)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        })
        {
            allocation = Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(20, allocation.Length);
            Assert.Equal(AllocationOptions.Clean, allocation.AllocationOptions);
            Assert.Equal(8, neighbors.Left.Length);
            Assert.Equal(12, neighbors.Top.Length);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.AllocationId, returned.AllocationId);
    }

    [Theory]
    [InlineData((int)Av1ComponentType.Luminance, 5)]
    [InlineData((int)Av1ComponentType.Chroma, 12)]
    public void WriterDerivesTransformContextFromCompleteFourByFourEdges(
        int componentType,
        int expectedSkipContext)
    {
        using Av1NeighborArrayUnit<byte> neighbors = new(
            Configuration.Default,
            leftSize: 8,
            topSize: 8)
        {
            GranularityNormalLog2 = 2
        };

        // The high bits carry positive, positive, and negative DC signs. The low bits select
        // the high-above and low-left coefficient classes used by the luma skip-context table.
        neighbors.Top[2] = (2 << Av1Constants.CoefficientContextBitCount) | 4;
        neighbors.Top[3] = 2 << Av1Constants.CoefficientContextBitCount;
        neighbors.Left[1] = (1 << Av1Constants.CoefficientContextBitCount) | 1;
        Av1TransformBlockContext context = Av1TileWriter.GetTransformBlockContexts(
            (Av1ComponentType)componentType,
            neighbors,
            new Point(8, 4),
            Av1BlockSize.Block16x16,
            Av1TransformSize.Size8x8);

        Assert.Equal(2, context.DcSignContext);
        Assert.Equal(expectedSkipContext, context.SkipContext);
    }

    [Theory]
    [InlineData(false, 2, 1, 6, 6)]
    [InlineData(true, 2, 2, 10, 3)]
    public void PictureControlSetMapsModeInfoAllocationByIndex(
        bool disallow4x4,
        int column,
        int row,
        int gridOffset,
        int allocationOffset)
    {
        Av1MacroBlockModeInfo expected = CreateModeInfo(Av1PredictionMode.Paeth);
        Av1MacroBlockModeInfo[] allocation = new Av1MacroBlockModeInfo[16];
        allocation[allocationOffset] = expected;
        int[] grid = new int[16];
        Av1PictureControlSet picture = new()
        {
            PartitionContexts = [],
            LuminanceDcSignLevelCoefficientNeighbors = [],
            CrDcSignLevelCoefficientNeighbors = [],
            CbDcSignLevelCoefficientNeighbors = [],
            TransformFunctionContexts = [],
            Sequence = new Av1SequenceControlSet { SequenceHeader = new ObuSequenceHeader() },
            Parent = new Av1PictureParentControlSet
            {
                Common = new Av1EncoderCommon
                {
                    ModeInfoColumnCount = 4,
                    ModeInfoRowCount = 4,
                    ModeInfoStride = 4,
                    FrameSize = new ObuFrameSize(),
                    TilesInfo = new ObuTileGroupHeader()
                },
                FrameHeader = new ObuFrameHeader(),
                PreviousQIndex = []
            },
            SegmentationNeighborMap = Memory<byte>.Empty,
            ModeInfoGrid = grid,
            ModeInfoAllocation = allocation,
            ModeInfoStride = 4,
            Disallow4x4AllFrames = disallow4x4,
            CdefPreset = []
        };

        Point position = new(column, row);
        ref Av1MacroBlockModeInfo result = ref picture.GetMacroBlockModeInfo(position);
        result.Block.Mode = Av1PredictionMode.Smooth;
        picture.MapModeInfoBlock(position, Av1BlockSize.Block8x8);

        Assert.Equal(Av1PredictionMode.Smooth, allocation[allocationOffset].Block.Mode);
        Assert.Equal(allocationOffset, grid[gridOffset]);
        Assert.Equal(allocationOffset, grid[gridOffset + 1]);
        Assert.Equal(allocationOffset, grid[gridOffset + 4]);
        Assert.Equal(allocationOffset, grid[gridOffset + 5]);
    }

    [Fact]
    public void MacroBlockReadsNeighborsRelativeToCurrentGridEntry()
    {
        Av1MacroBlockModeInfo[] allocation =
        [
            CreateModeInfo(Av1PredictionMode.Vertical),
            CreateModeInfo(Av1PredictionMode.Horizontal),
            CreateModeInfo(Av1PredictionMode.DC)
        ];

        int[] grid = new int[9];
        grid[1] = 0;
        grid[3] = 1;
        grid[4] = 2;
        Av1MacroBlockD macroBlock = CreateMacroBlock();
        macroBlock.SetModeInfoGrid(grid, allocation, 4);

        Assert.Equal(Av1PredictionMode.Horizontal, macroBlock.GetRelativeModeInfo(-1).Block.Mode);
        Assert.Equal(Av1PredictionMode.Vertical, macroBlock.GetRelativeModeInfo(-3).Block.Mode);
        Assert.Equal(Av1PredictionMode.DC, macroBlock.GetRelativeModeInfo(0).Block.Mode);
    }

    [Fact]
    public void EncoderBlockModeInfoStoresSelectedSyntax()
    {
        Av1EncoderBlockModeInfo modeInfo = default;

        Assert.False(modeInfo.Skip);
        Assert.False(modeInfo.SkipMode);
        Assert.False(modeInfo.UseIntraBlockCopy);

        modeInfo.Skip = true;
        modeInfo.SkipMode = true;
        modeInfo.UseIntraBlockCopy = true;
        modeInfo.BlockSize = Av1BlockSize.Block16x16;
        modeInfo.PartitionType = Av1PartitionType.Split;
        modeInfo.SegmentId = 3;
        modeInfo.Mode = Av1PredictionMode.Smooth;
        modeInfo.UvMode = Av1ChromaPredictionMode.Smooth;

        Assert.True(modeInfo.Skip);
        Assert.True(modeInfo.SkipMode);
        Assert.True(modeInfo.UseIntraBlockCopy);
        Assert.Equal(Av1BlockSize.Block16x16, modeInfo.BlockSize);
        Assert.Equal(Av1PartitionType.Split, modeInfo.PartitionType);
        Assert.Equal(3, modeInfo.SegmentId);
        Assert.Equal(Av1PredictionMode.Smooth, modeInfo.Mode);
        Assert.Equal(Av1ChromaPredictionMode.Smooth, modeInfo.UvMode);

        modeInfo.SkipMode = false;

        Assert.True(modeInfo.Skip);
        Assert.False(modeInfo.SkipMode);
        Assert.True(modeInfo.UseIntraBlockCopy);
    }

    [Fact]
    public void EncoderModeInfoUsesPackedValueStorage()
    {
        Assert.Equal(7, Unsafe.SizeOf<Av1EncoderBlockModeInfo>());
        Assert.Equal(8, Unsafe.SizeOf<Av1MacroBlockModeInfo>());
    }

    [Fact]
    public void EncoderSuperblockWorkspaceUsesOneExactSizeOwner()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        TestMemoryAllocator.AllocationRequest allocation;
        using (Av1EncoderSuperblockWorkspace workspace = new(configuration))
        {
            allocation = Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(typeof(Av1EncoderBlockStruct), allocation.ElementType);
            Assert.Equal(AllocationOptions.None, allocation.AllocationOptions);
            Assert.Equal(Av1EncoderSuperblockWorkspace.StorageLength, allocation.Length);
            Assert.Equal(Av1EncoderSuperblockWorkspace.MaximumFinalBlockCount, workspace.FinalBlocks.Length);
            Assert.Equal(Av1EncoderSuperblockWorkspace.MaximumPartitionCount, workspace.PartitionTypes.Length);
            Assert.Equal(Av1EncoderBlockStruct.StorageSize, Unsafe.SizeOf<Av1EncoderBlockStruct>());
            Assert.Equal(Av1EncoderPaletteInfo.StorageSize, Unsafe.SizeOf<Av1EncoderPaletteInfo>());
            Assert.Equal(0, workspace.PaletteInfo.PaletteSizes[0]);
            Assert.Equal(0, workspace.FinalBlocks[^1].QuantizationIndex);
            Assert.Equal(Av1FilterIntraMode.AllFilterIntraModes, workspace.FinalBlocks[0].FilterIntraMode);
            Assert.Equal(Av1FilterIntraMode.AllFilterIntraModes, workspace.FinalBlocks[^1].FilterIntraMode);
            Assert.Equal(0, workspace.PartitionTypes[^1]);

            workspace.PaletteInfo.PaletteSizes[0] = 7;
            workspace.FinalBlocks[0].FilterIntraMode = Av1FilterIntraMode.DC;
            workspace.FinalBlocks[^1].QuantizationIndex = 255;
            workspace.FinalBlocks[^1].FilterIntraMode = Av1FilterIntraMode.Paeth;
            workspace.PartitionTypes.Fill(byte.MaxValue);
            workspace.Reset();

            Assert.Equal(0, workspace.PaletteInfo.PaletteSizes[0]);
            Assert.Equal(0, workspace.FinalBlocks[^1].QuantizationIndex);
            Assert.Equal(Av1FilterIntraMode.AllFilterIntraModes, workspace.FinalBlocks[0].FilterIntraMode);
            Assert.Equal(Av1FilterIntraMode.AllFilterIntraModes, workspace.FinalBlocks[^1].FilterIntraMode);
            for (int index = 0; index < workspace.PartitionTypes.Length; index++)
            {
                Assert.Equal(0, workspace.PartitionTypes[index]);
            }
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.AllocationId, returned.AllocationId);
    }

    [Fact]
    public void EncoderBlocksKeepInlineModeStateWithoutPerBlockAllocations()
    {
        Av1EncoderBlockStruct[] blocks = new Av1EncoderBlockStruct[2];
        Av1EncoderPaletteInfo[] palettes = new Av1EncoderPaletteInfo[2];

        // Exercise the inline-array accessors before measuring so one-time runtime generic initialization is
        // excluded from the steady-state allocation contract used for every encoded block.
        ref Av1EncoderBlockStruct warmupBlock = ref blocks[0];
        ref Av1EncoderPaletteInfo warmupPalette = ref palettes[0];
        warmupPalette.PaletteSizes[0] = 1;
        warmupBlock.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = 1;

        long before = GC.GetAllocatedBytesForCurrentThread();
        ref Av1EncoderBlockStruct block = ref blocks[1];
        ref Av1EncoderPaletteInfo palette = ref palettes[1];
        palette.PaletteSizes[0] = 3;
        palette.PaletteSizes[1] = 5;
        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Y] = -2;
        block.PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv] = 3;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(3, palettes[1].PaletteSizes[0]);
        Assert.Equal(5, palettes[1].PaletteSizes[1]);
        Assert.Equal(-2, blocks[1].PredictionUnit.AngleDelta[(int)Av1PlaneType.Y]);
        Assert.Equal(3, blocks[1].PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv]);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void EncoderPaletteMapsUseOneLazyExactSizeOwner()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        TestMemoryAllocator.AllocationRequest[] allocations;
        using (Av1EncoderSuperblockWorkspace workspace = new(configuration))
        {
            Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);

            Av1EncoderPaletteMapBuffer maps = workspace.GetPaletteMaps();
            Assert.Same(maps, workspace.GetPaletteMaps());
            allocations = allocator.AllocationLog.ToArray();
            Assert.Equal(2, allocations.Length);
            Assert.Equal(typeof(byte), allocations[1].ElementType);
            Assert.Equal(Av1EncoderPaletteMapBuffer.StorageLength, allocations[1].Length);
            Assert.Equal(AllocationOptions.None, allocations[1].AllocationOptions);

            Buffer2DRegion<byte> luma = maps.GetMap(Av1PlaneType.Y, 64, 64);
            Buffer2DRegion<byte> chroma = maps.GetMap(Av1PlaneType.Uv, 32, 32);
            luma.DangerousGetRowSpan(0)[0] = 3;
            chroma.DangerousGetRowSpan(0)[0] = 5;
            Assert.Equal(3, luma.DangerousGetRowSpan(0)[0]);
            Assert.Equal(5, chroma.DangerousGetRowSpan(0)[0]);
        }

        Assert.Equal(2, allocator.ReturnLog.Count);
        Assert.Equal(
            allocations.Select(x => x.AllocationId).Order(),
            allocator.ReturnLog.Select(x => x.AllocationId).Order());
    }

    [Fact]
    public void PaletteModeWriterMatchesColorCacheBoundaryAndRoundTrips()
    {
        const int Width = 16;
        const int Height = 72;
        const int BlockSizeContext = 0;
        Point blockOrigin = new(8, 64);
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = false,
            SubSamplingY = false,
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
            ColorConfig = colorConfig
        };

        ObuFrameHeader frameHeader = new()
        {
            AllowScreenContentTools = true,
            ModeInfoColumnCount = Width >> Av1Constants.ModeInfoSizeLog2,
            ModeInfoRowCount = Height >> Av1Constants.ModeInfoSizeLog2,
            TilesInfo = tiles
        };

        using Av1EncoderPictureBuffer pictureBuffer = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            Width,
            Height);

        Av1PictureControlSet picture = pictureBuffer.Picture;
        Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContexts = Assert.Single(picture.PaletteContexts);
        ref Av1EncoderPaletteInfo above = ref paletteContexts.Top[paletteContexts.GetTopIndex(blockOrigin)];
        above.PaletteSizes[0] = 2;
        above.PaletteSizes[1] = 2;
        above.SetColors(Av1Plane.Y, [10, 30]);
        above.SetColors(Av1Plane.U, [15, 35]);
        ref Av1EncoderPaletteInfo left = ref paletteContexts.Left[paletteContexts.GetLeftIndex(blockOrigin)];
        left.PaletteSizes[0] = 2;
        left.PaletteSizes[1] = 2;
        left.SetColors(Av1Plane.Y, [20, 40]);
        left.SetColors(Av1Plane.U, [25, 45]);

        Av1EncoderPaletteInfo current = default;
        current.PaletteSizes[0] = 3;
        current.PaletteSizes[1] = 3;
        current.SetColors(Av1Plane.Y, [20, 50, 70]);
        current.SetColors(Av1Plane.U, [25, 55, 80]);
        current.SetColors(Av1Plane.V, [10, 12, 9]);
        Av1MacroBlockModeInfo modeInfo = default;
        modeInfo.Block = new Av1EncoderBlockModeInfo
        {
            BlockSize = Av1BlockSize.Block8x8,
            Mode = Av1PredictionMode.DC,
            UvMode = Av1ChromaPredictionMode.DC
        };

        Av1MacroBlockD macroBlock = new()
        {
            Tile = new Av1TileInfo(0, 0, frameHeader),
            IsUpAvailable = true,
            IsLeftAvailable = true
        };

        using Av1SymbolEncoder encoder = new(Configuration.Default, 128, BaseQIndex);
        Av1TileWriter.WritePaletteModeInfo(
            picture.Sequence,
            picture,
            encoder,
            macroBlock,
            modeInfo,
            ref current,
            Av1BlockSize.Block8x8,
            blockOrigin,
            tileIndex: 0,
            hasChroma: true);

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        Assert.True(decoder.ReadPaletteYMode(BlockSizeContext, neighborContext: 2));
        Assert.Equal(3, decoder.ReadPaletteSize(BlockSizeContext, Av1PlaneType.Y));
        Span<ushort> decodedY = stackalloc ushort[3];
        decoder.ReadPaletteYColors([20, 40], 3, bitDepth: 8, decodedY);
        Assert.Equal([20, 50, 70], decodedY.ToArray());
        Assert.True(decoder.ReadPaletteUvMode(hasLumaPalette: true));
        Assert.Equal(3, decoder.ReadPaletteSize(BlockSizeContext, Av1PlaneType.Uv));
        Span<ushort> decodedU = stackalloc ushort[3];
        Span<ushort> decodedV = stackalloc ushort[3];
        decoder.ReadPaletteUvColors([25, 45], 3, bitDepth: 8, decodedU, decodedV);
        Assert.Equal([25, 55, 80], decodedU.ToArray());
        Assert.Equal([10, 12, 9], decodedV.ToArray());
        decoder.ValidateTrailingBits();
    }

    [Theory]
    [InlineData(false, 6, 4096, 1024, 6144, 256, 64, 384, 36864L)]
    [InlineData(true, 2, 16384, 4096, 24576, 1024, 256, 1536, 49152L)]
    public void EncoderCoefficientBufferMatchesLibaom420SuperblockLayout(
        bool use128x128Superblock,
        int expectedSuperblockCount,
        int expectedLumaCount,
        int expectedChromaCount,
        int expectedCoefficientsPerSuperblock,
        int expectedLumaTransformBlockCount,
        int expectedChromaTransformBlockCount,
        int expectedTransformBlocksPerSuperblock,
        long expectedTotalCoefficientCount)
    {
        ObuSequenceHeader sequenceHeader = new() { Use128x128Superblock = use128x128Superblock };
        sequenceHeader.ColorConfig.IsMonochrome = false;
        sequenceHeader.ColorConfig.SubSamplingX = true;
        sequenceHeader.ColorConfig.SubSamplingY = true;

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            sequenceHeader,
            width: 129,
            height: 65);

        Assert.Equal(expectedSuperblockCount, coefficients.SuperblockCount);
        Assert.Equal(expectedLumaCount, coefficients.LumaCoefficientCount);
        Assert.Equal(expectedChromaCount, coefficients.ChromaCoefficientCount);
        Assert.Equal(expectedCoefficientsPerSuperblock, coefficients.CoefficientsPerSuperblock);
        Assert.Equal(expectedLumaTransformBlockCount, coefficients.LumaTransformBlockCount);
        Assert.Equal(expectedChromaTransformBlockCount, coefficients.ChromaTransformBlockCount);
        Assert.Equal(expectedTransformBlocksPerSuperblock, coefficients.TransformBlocksPerSuperblock);
        Assert.Equal(expectedTotalCoefficientCount, coefficients.TotalCoefficientCount);
        Assert.Equal(expectedLumaCount, coefficients.GetPlaneSpan(0, Av1Plane.Y).Length);
        Assert.Equal(expectedChromaCount, coefficients.GetPlaneSpan(0, Av1Plane.U).Length);
        Assert.Equal(expectedChromaCount, coefficients.GetPlaneSpan(0, Av1Plane.V).Length);
        Assert.Equal(expectedLumaTransformBlockCount, coefficients.GetTransformBlockSpan(0, Av1Plane.Y).Length);
        Assert.Equal(expectedChromaTransformBlockCount, coefficients.GetTransformBlockSpan(0, Av1Plane.U).Length);
        Assert.Equal(expectedChromaTransformBlockCount, coefficients.GetTransformBlockSpan(0, Av1Plane.V).Length);
    }

    [Fact]
    public void EncoderCoefficientBufferKeepsEveryPlaneAndSuperblockDisjoint()
    {
        ObuSequenceHeader sequenceHeader = new() { Use128x128Superblock = true };
        sequenceHeader.ColorConfig.IsMonochrome = false;
        sequenceHeader.ColorConfig.SubSamplingX = true;
        sequenceHeader.ColorConfig.SubSamplingY = true;

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            sequenceHeader,
            width: 129,
            height: 65);

        coefficients.GetPlaneSpan(0, Av1Plane.Y)[0] = 11;
        coefficients.GetPlaneSpan(0, Av1Plane.U)[0] = 22;
        coefficients.GetPlaneSpan(0, Av1Plane.V)[0] = 33;
        coefficients.GetPlaneSpan(1, Av1Plane.Y)[0] = 44;
        coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[0].EndOfBlock = 55;
        coefficients.GetTransformBlockSpan(0, Av1Plane.U)[0].EndOfBlock = 66;
        coefficients.GetTransformBlockSpan(0, Av1Plane.V)[0].EndOfBlock = 77;
        coefficients.GetTransformBlockSpan(1, Av1Plane.Y)[0].EndOfBlock = 88;

        Assert.Equal(11, coefficients.GetPlaneSpan(0, Av1Plane.Y)[0]);
        Assert.Equal(22, coefficients.GetPlaneSpan(0, Av1Plane.U)[0]);
        Assert.Equal(33, coefficients.GetPlaneSpan(0, Av1Plane.V)[0]);
        Assert.Equal(44, coefficients.GetPlaneSpan(1, Av1Plane.Y)[0]);
        Assert.Equal(55, coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[0].EndOfBlock);
        Assert.Equal(66, coefficients.GetTransformBlockSpan(0, Av1Plane.U)[0].EndOfBlock);
        Assert.Equal(77, coefficients.GetTransformBlockSpan(0, Av1Plane.V)[0].EndOfBlock);
        Assert.Equal(88, coefficients.GetTransformBlockSpan(1, Av1Plane.Y)[0].EndOfBlock);
    }

    [Fact]
    public void EncoderLumaTraversalRepresentsAllTransformsIn128x128Block()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(32, 32, use128x128Superblock: true);
        ref Av1MacroBlockModeInfo modeInfo = ref picture.ModeInfoAllocation.Span[0];
        modeInfo.Block.BlockSize = Av1BlockSize.Block128x128;
        modeInfo.Block.TransformSize = Av1TransformSize.Size16x16;
        modeInfo.Block.SegmentId = 0;
        Av1TileInfo tile = new(0, 0, picture.Parent.FrameHeader);
        Av1TileWriter.Av1EntropyCodingContext context = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = tile },
            MacroBlockModeInfo = modeInfo,
            SuperblockOrigin = Point.Empty
        };

        using Av1NeighborArrayUnit<byte> luma = new(
            Configuration.Default,
            leftSize: 128,
            topSize: 128)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            width: 128,
            height: 128);

        Span<Av1EncoderTransformBlockState> transformBlocks =
            coefficients.GetTransformBlockSpan(0, Av1Plane.Y);
        transformBlocks.Fill(new Av1EncoderTransformBlockState { TransformType = Av1TransformType.Identity });

        Av1EncoderBlockStruct block = default;
        using Av1SymbolEncoder writer = new(Configuration.Default, 4096, BaseQIndex);
        Av1TileWriter.EncodeTransformCoefficientsY(
            picture,
            context,
            writer,
            ref block,
            Point.Empty,
            Av1PredictionMode.DC,
            Av1BlockSize.Block128x128,
            coefficients,
            superblockIndex: 0,
            luma);

        writer.Dispose();

        int visitedTransformCount = 0;
        for (int index = 0; index < transformBlocks.Length; index++)
        {
            if ((index % 16) == 0)
            {
                Assert.Equal(Av1TransformType.DctDct, transformBlocks[index].TransformType);
                visitedTransformCount++;
            }
            else
            {
                Assert.Equal(Av1TransformType.Identity, transformBlocks[index].TransformType);
            }
        }

        Assert.Equal(64, visitedTransformCount);
        Assert.Equal(16384, context.CodedAreaSuperblock);
    }

    [Fact]
    public void SegmentationUpdateUsesModeInfoUnits()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(8, 8);
        picture.SegmentationNeighborMap = new byte[64];

        picture.UpdateSegmentation(Av1BlockSize.Block16x8, new Point(8, 12), segmentId: 5);

        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                byte expected = row is 3 or 4 && column >= 2 && column < 6 ? (byte)5 : (byte)0;
                Assert.Equal(expected, picture.SegmentationNeighborMap.Span[(row * 8) + column]);
            }
        }
    }

    [Fact]
    public void TransformSizeContextUsesIntraBlockCopyNeighborExtents()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(16, 16);
        Point blockOrigin = new(16, 16);
        Av1MacroBlockD macroBlock = new()
        {
            Tile = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            IsUpAvailable = true,
            IsLeftAvailable = true
        };

        int modeInfoIndex =
            ((blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2) * picture.ModeInfoStride) +
            (blockOrigin.X >> Av1Constants.ModeInfoSizeLog2);

        macroBlock.ModeInfoStride = picture.ModeInfoStride;
        macroBlock.SetModeInfoGrid(picture.ModeInfoGrid, picture.ModeInfoAllocation, modeInfoIndex);

        ref Av1MacroBlockModeInfo aboveModeInfo = ref macroBlock.GetRelativeModeInfo(-macroBlock.ModeInfoStride);
        aboveModeInfo.Block.BlockSize = Av1BlockSize.Block16x8;
        aboveModeInfo.Block.UseIntraBlockCopy = true;
        ref Av1MacroBlockModeInfo leftModeInfo = ref macroBlock.GetRelativeModeInfo(-1);
        leftModeInfo.Block.BlockSize = Av1BlockSize.Block8x16;
        leftModeInfo.Block.UseIntraBlockCopy = true;

        using Av1NeighborArrayUnit<byte> transforms = new(
            Configuration.Default,
            leftSize: 64,
            topSize: 64)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        // Residual contexts report 8x8, but libaom derives 16x16 availability from the IBC coding blocks.
        transforms.Top[transforms.GetTopIndex(blockOrigin)] = 8;
        transforms.Left[transforms.GetLeftIndex(blockOrigin)] = 8;

        Assert.Equal(
            2,
            Av1TileWriter.GetTransformSizeContext(
                transforms,
                macroBlock,
                blockOrigin,
                Av1BlockSize.Block16x16));
    }

    [Fact]
    public void SelectedTransformSizeRoundTripsAndPublishesRectangularEdgeContexts()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(16, 16);
        picture.Parent.FrameHeader.TransformMode = Av1TransformMode.Select;
        ref Av1MacroBlockModeInfo modeInfo = ref picture.ModeInfoAllocation.Span[0];
        modeInfo.Block.BlockSize = Av1BlockSize.Block16x32;
        modeInfo.Block.TransformSize = Av1TransformSize.Size8x8;
        modeInfo.Block.SegmentId = 0;
        Point blockOrigin = new(16, 16);
        Av1MacroBlockD macroBlock = new()
        {
            Tile = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            IsUpAvailable = true,
            IsLeftAvailable = true
        };

        int modeInfoIndex =
            ((blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2) * picture.ModeInfoStride) +
            (blockOrigin.X >> Av1Constants.ModeInfoSizeLog2);

        // Uniform-size context substitutes coding-block extents for inter neighbors, so mirror production mode-info setup.
        macroBlock.ModeInfoStride = picture.ModeInfoStride;
        macroBlock.SetModeInfoGrid(picture.ModeInfoGrid, picture.ModeInfoAllocation, modeInfoIndex);

        using Av1NeighborArrayUnit<byte> transforms = new(
            Configuration.Default,
            leftSize: 64,
            topSize: 64)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        int topIndex = transforms.GetTopIndex(blockOrigin);
        int leftIndex = transforms.GetLeftIndex(blockOrigin);
        transforms.Top[topIndex] = 16;
        transforms.Left[leftIndex] = 16;
        picture.TransformFunctionContexts = [transforms];

        using Av1SymbolEncoder writer = new(Configuration.Default, 64, BaseQIndex);
        Av1TileWriter.WriteTransformSize(
            picture,
            writer,
            ref modeInfo,
            macroBlock,
            modeInfo.Block.BlockSize,
            blockOrigin,
            tileIndex: 0);

        using IMemoryOwner<byte> encoded = writer.Exit();
        writer.Dispose();

        Av1SymbolDecoder reader = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        Assert.Equal(
            Av1TransformSize.Size8x8,
            reader.ReadTransformSize(Av1BlockSize.Block16x32, context: 1));

        for (int index = 0; index < transforms.Top.Length; index++)
        {
            byte expected = index >= topIndex && index < topIndex + 4 ? (byte)8 : (byte)0;
            Assert.Equal(expected, transforms.Top[index]);
        }

        for (int index = 0; index < transforms.Left.Length; index++)
        {
            byte expected = index >= leftIndex && index < leftIndex + 8 ? (byte)8 : (byte)0;
            Assert.Equal(expected, transforms.Left[index]);
        }
    }

    [Theory]
    [InlineData((int)Av1PartitionType.None, 24, 24)]
    [InlineData((int)Av1PartitionType.Horizontal, 24, 28)]
    [InlineData((int)Av1PartitionType.Vertical, 28, 24)]
    [InlineData((int)Av1PartitionType.Split, 0, 0)]
    [InlineData((int)Av1PartitionType.HorizontalA, 24, 28)]
    [InlineData((int)Av1PartitionType.HorizontalB, 28, 28)]
    [InlineData((int)Av1PartitionType.VerticalA, 28, 24)]
    [InlineData((int)Av1PartitionType.VerticalB, 28, 28)]
    [InlineData((int)Av1PartitionType.Horizontal4, 24, 30)]
    [InlineData((int)Av1PartitionType.Vertical4, 30, 24)]
    public void PartitionContextUpdatesMatchLibaomExtendedPartitionRules(
        int partitionValue,
        byte expectedAbove,
        byte expectedLeft)
    {
        using Av1NeighborArrayUnit<Av1PartitionContext> neighbors = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 16)
        {
            GranularityNormalLog2 = 2
        };

        Av1PartitionType partition = (Av1PartitionType)partitionValue;
        Av1BlockSize blockSize = Av1BlockSize.Block32x32;
        Av1BlockSize subSize = partition.GetBlockSubSize(blockSize);

        Av1TileWriter.UpdatePartitionContexts(
            neighbors,
            new Point(8, 12),
            subSize,
            blockSize,
            partition);

        for (int index = 0; index < 16; index++)
        {
            byte above = index is >= 2 and < 10 ? expectedAbove : (byte)0;
            byte left = index is >= 3 and < 11 ? expectedLeft : (byte)0;
            Assert.Equal(above, neighbors.Top[index].Above);
            Assert.Equal(left, neighbors.Left[index].Left);
        }
    }

    [Fact]
    public void EightByEightSplitPublishesFourByFourPartitionContexts()
    {
        using Av1NeighborArrayUnit<Av1PartitionContext> neighbors = new(
            Configuration.Default,
            leftSize: 4,
            topSize: 4)
        {
            GranularityNormalLog2 = 2
        };

        Av1TileWriter.UpdatePartitionContexts(
            neighbors,
            new Point(4, 4),
            Av1BlockSize.Block4x4,
            Av1BlockSize.Block8x8,
            Av1PartitionType.Split);

        Assert.Equal(31, neighbors.Top[1].Above);
        Assert.Equal(31, neighbors.Top[2].Above);
        Assert.Equal(31, neighbors.Left[1].Left);
        Assert.Equal(31, neighbors.Left[2].Left);
    }

    [Fact]
    public void EncoderModeInfoEdgesUseFourByFourUnits()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(6, 5);
        Av1TileInfo tile = new(0, 0, picture.Parent.FrameHeader);
        Av1MacroBlockD macroBlock = new() { Tile = tile };
        Point position = new(2, 3);
        picture.GetMacroBlockModeInfo(position).Block.Mode = Av1PredictionMode.Paeth;
        picture.MapModeInfoBlock(position, Av1BlockSize.Block16x8);

        Av1TileWriter.SetModeInfoRowAndColumn(
            picture,
            macroBlock,
            tile,
            position,
            Av1BlockSize.Block16x8,
            picture.ModeInfoStride,
            picture.Parent.Common.ModeInfoRowCount,
            picture.Parent.Common.ModeInfoColumnCount);

        Assert.Equal(-96, macroBlock.ToTopEdge);
        Assert.Equal(0, macroBlock.ToBottomEdge);
        Assert.Equal(-64, macroBlock.ToLeftEdge);
        Assert.Equal(0, macroBlock.ToRightEdge);
        Assert.Equal(
            picture.GetFromModeInfoGrid(new Point(2, 2)).Block.Mode,
            macroBlock.GetRelativeModeInfo(-picture.ModeInfoStride).Block.Mode);
        Assert.Equal(
            picture.GetFromModeInfoGrid(new Point(1, 3)).Block.Mode,
            macroBlock.GetRelativeModeInfo(-1).Block.Mode);

        for (int row = 0; row < picture.Parent.Common.ModeInfoRowCount; row++)
        {
            for (int column = 0; column < picture.Parent.Common.ModeInfoColumnCount; column++)
            {
                Av1PredictionMode expected = row >= 3 && column >= 2 ? Av1PredictionMode.Paeth : Av1PredictionMode.DC;
                Assert.Equal(expected, picture.GetFromModeInfoGrid(new Point(column, row)).Block.Mode);
            }
        }
    }

    [Fact]
    public void CdefUsesLibaomUnitIndexAndFirstBlockStrength()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(32, 32, use128x128Superblock: true);
        picture.Parent.FrameHeader.CdefParameters.BitCount = 2;
        picture.ModeInfoAllocation.Span[16].CdefStrength = 3;
        picture.ModeInfoAllocation.Span[20].CdefStrength = 1;
        using Av1SymbolEncoder writer = new(Configuration.Default, 16, BaseQIndex);

        Av1TileWriter.WriteCdef(
            picture.Sequence,
            picture,
            writer,
            tileIndex: 0,
            skip: false,
            modeInfoPosition: new Point(20, 4));

        Assert.Equal(new[] { -1, 3, -1, -1 }, picture.CdefPreset[0]);
    }

    [Fact]
    public void SuperblockWriterTraversesSplitTreeFromAbsoluteOrigin()
    {
        Av1PictureControlSet picture = CreateEncoderPicture(32, 16);
        picture.Sequence.SequenceHeader.ColorConfig.IsMonochrome = true;
        picture.Parent.FrameHeader.CodedLossless = true;
        using Av1NeighborArrayUnit<Av1PartitionContext> partitions = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 32)
        {
            GranularityNormalLog2 = 2
        };

        using Av1NeighborArrayUnit<byte> luma = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 32)
        {
            GranularityNormalLog2 = 2
        };

        using Av1NeighborArrayUnit<byte> red = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 32)
        {
            GranularityNormalLog2 = 2
        };

        using Av1NeighborArrayUnit<byte> blue = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 32)
        {
            GranularityNormalLog2 = 2
        };

        using Av1NeighborArrayUnit<byte> transforms = new(
            Configuration.Default,
            leftSize: 16,
            topSize: 32)
        {
            GranularityNormalLog2 = 2
        };

        picture.PartitionContexts = [partitions];
        picture.LuminanceDcSignLevelCoefficientNeighbors = [luma];
        picture.CrDcSignLevelCoefficientNeighbors = [red];
        picture.CbDcSignLevelCoefficientNeighbors = [blue];
        picture.TransformFunctionContexts = [transforms];
        Av1TileInfo tile = new(0, 0, picture.Parent.FrameHeader);
        Point[] modeInfoPositions = [new(16, 0), new(24, 0), new(16, 8), new(24, 8)];
        using Av1EncoderSuperblockWorkspace workspace = new(Configuration.Default);
        for (int index = 0; index < modeInfoPositions.Length; index++)
        {
            Point position = modeInfoPositions[index];
            ref Av1EncoderBlockModeInfo blockMode = ref picture.ModeInfoAllocation.Span[
                (position.Y * picture.ModeInfoStride) + position.X].Block;

            blockMode.BlockSize = Av1BlockSize.Block32x32;
            blockMode.Skip = true;
            blockMode.Mode = Av1PredictionMode.DC;
            blockMode.UvMode = Av1ChromaPredictionMode.DC;
            workspace.FinalBlocks[index].HasChroma = false;
        }

        ReadOnlySpan<byte> partitionTypes =
        [
            (byte)Av1PartitionType.Split,
            (byte)Av1PartitionType.None,
            (byte)Av1PartitionType.None,
            (byte)Av1PartitionType.None,
            (byte)Av1PartitionType.None
        ];

        partitionTypes.CopyTo(workspace.PartitionTypes);
        Av1Superblock superblock = new()
        {
            Workspace = workspace,
            TileInfo = tile,
            Index = 1
        };
        Av1TileWriter.Av1EntropyCodingContext context = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = tile },
            MacroBlockModeInfo = picture.ModeInfoAllocation.Span[16],
            SuperblockOrigin = new Point(64, 0)
        };
        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            width: 128,
            height: 64);

        using Av1SymbolEncoder writer = new(Configuration.Default, 512, BaseQIndex);

        Av1TileWriter.WriteSuperblock(
            picture,
            context,
            writer,
            superblock,
            coefficients,
            tileIndex: 0);

        Assert.Equal(Av1TransformSize.Size32x32, context.MacroBlockModeInfo.Block.TransformSize);
        foreach (Point position in modeInfoPositions)
        {
            Assert.Equal(
                Av1TransformSize.Size32x32,
                picture.GetMacroBlockModeInfo(position).Block.TransformSize);
        }

        writer.Dispose();

        Assert.Equal(4096, context.CodedAreaSuperblock);
        Assert.Equal(0, context.CodedAreaSuperblockUv);
        for (int index = 0; index < partitions.Top.Length; index++)
        {
            Assert.Equal(index < 16 ? 0 : 24, partitions.Top[index].Above);
        }

        for (int index = 0; index < partitions.Left.Length; index++)
        {
            Assert.Equal(24, partitions.Left[index].Left);
        }

        for (int index = 0; index < transforms.Top.Length; index++)
        {
            Assert.Equal(index < 16 ? 0 : 32, transforms.Top[index]);
        }

        for (int index = 0; index < transforms.Left.Length; index++)
        {
            Assert.Equal(32, transforms.Left[index]);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PartitionWriterUsesMatchingFrameEdgeDistribution(bool bottomEdge)
    {
        const int partitionContext = 8;
        Av1BlockSize blockSize = Av1BlockSize.Block32x32;
        int modeInfoColumnCount = bottomEdge ? 8 : 4;
        int modeInfoRowCount = bottomEdge ? 4 : 8;
        Av1PictureControlSet picture = CreateEncoderPicture(modeInfoColumnCount, modeInfoRowCount);
        using Av1NeighborArrayUnit<Av1PartitionContext> neighbors = new(
            Configuration.Default,
            leftSize: 1,
            topSize: 1)
        {
            GranularityNormalLog2 = 2
        };

        Av1PartitionType nonSplitPartition = bottomEdge ? Av1PartitionType.Horizontal : Av1PartitionType.Vertical;
        ReadOnlySpan<Av1PartitionType> decisions =
        [
            nonSplitPartition,
            Av1PartitionType.Split,
            nonSplitPartition,
            nonSplitPartition,
            Av1PartitionType.Split,
            Av1PartitionType.Split,
            nonSplitPartition,
            Av1PartitionType.Split
        ];

        using Av1SymbolEncoder actualWriter = new(Configuration.Default, 16, BaseQIndex);
        using Av1SymbolEncoder expectedWriter = new(Configuration.Default, 16, BaseQIndex);
        foreach (Av1PartitionType decision in decisions)
        {
            Av1TileWriter.EncodePartition(
                picture,
                actualWriter,
                blockSize,
                decision,
                Point.Empty,
                neighbors);

            if (bottomEdge)
            {
                expectedWriter.WriteSplitOrHorizontal(decision, blockSize, partitionContext);
            }
            else
            {
                expectedWriter.WriteSplitOrVertical(decision, blockSize, partitionContext);
            }
        }

        using IMemoryOwner<byte> actual = actualWriter.Exit();
        using IMemoryOwner<byte> expected = expectedWriter.Exit();

        Assert.True(expected.GetSpan().SequenceEqual(actual.GetSpan()));
    }

    [Theory]
    [InlineData((int)Av1BlockSize.Block4x4, false, false, true, true)]
    [InlineData((int)Av1BlockSize.Block8x8, true, true, true, true)]
    [InlineData((int)Av1BlockSize.Block8x8, false, false, true, false)]
    [InlineData((int)Av1BlockSize.Block16x16, true, true, true, false)]
    [InlineData((int)Av1BlockSize.Block32x32, true, true, false, true)]
    [InlineData((int)Av1BlockSize.Block64x64, true, true, false, false)]
    public void ChromaFromLumaAvailabilityUsesLosslessPlaneGeometry(
        int blockSize,
        bool subSamplingX,
        bool subSamplingY,
        bool isLossless,
        bool expected)
        => Assert.Equal(
            expected,
            ((Av1BlockSize)blockSize).AllowsChromaFromLuma(isLossless, subSamplingX, subSamplingY));

    [Fact]
    public void LosslessChromaModeUsesPlaneSizedChromaFromLumaAlphabet()
    {
        ObuFrameHeader frameHeader = new();
        frameHeader.LosslessArray[0] = true;
        ObuColorConfig colorConfig = new()
        {
            SubSamplingX = true,
            SubSamplingY = true
        };

        Av1MacroBlockModeInfo modeInfo = default;
        modeInfo.Block.SegmentId = 0;
        Av1EncoderBlockStruct block = default;
        Av1BlockSize blockSize = Av1BlockSize.Block16x16;
        ReadOnlySpan<Av1ChromaPredictionMode> decisions =
        [
            Av1ChromaPredictionMode.DC,
            Av1ChromaPredictionMode.Smooth,
            Av1ChromaPredictionMode.Paeth,
            Av1ChromaPredictionMode.SmoothVertical,
            Av1ChromaPredictionMode.DC,
            Av1ChromaPredictionMode.SmoothHorizontal
        ];

        using Av1SymbolEncoder actualWriter = new(Configuration.Default, 16, BaseQIndex);
        using Av1SymbolEncoder expectedWriter = new(Configuration.Default, 16, BaseQIndex);
        foreach (Av1ChromaPredictionMode decision in decisions)
        {
            Av1TileWriter.EncodeIntraChromaMode(
                actualWriter,
                frameHeader,
                colorConfig,
                modeInfo,
                ref block,
                blockSize,
                Av1PredictionMode.DC,
                decision);

            expectedWriter.WriteChromaMode(
                decision,
                isChromaFromLumaAllowed: false,
                Av1PredictionMode.DC);
        }

        using IMemoryOwner<byte> actual = actualWriter.Exit();
        using IMemoryOwner<byte> expected = expectedWriter.Exit();

        Assert.True(expected.GetSpan().SequenceEqual(actual.GetSpan()));
    }

    [Fact]
    public void RoundTripZeroEndOfBlock()
    {
        // Assign
        Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        Av1TransformType transformType = Av1TransformType.Identity;
        Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        Av1ComponentType componentType = Av1ComponentType.Luminance;
        Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        ushort endOfBlock = 0;
        Av1BlockModeInfo modeInfo = new(blockSize, new Point(0, 0));
        Av1TransformInfo transformInfo = new(transformSize, 0, 0);
        int[] aboveContexts = new int[1];
        int[] leftContexts = new int[1];
        Av1TransformBlockContext transformBlockContext = default;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Span<int> coefficientsBuffer = [1, 2, 3, 4, 5];
        Span<int> expected = new int[16];
        Span<int> actuals = new int[16];

        // Act
        encoder.WriteCoefficients(transformSize, transformType, intraDirection, coefficientsBuffer, componentType, transformBlockContext, endOfBlock, true, filterIntraMode, usesInterTransformSet: false);

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        using Av1LevelBuffer levels = new(Configuration.Default);
        decoder.ReadCoefficients(
            modeInfo,
            new Point(0, 0),
            aboveContexts,
            leftContexts,
            0,
            0,
            0,
            1,
            1,
            transformBlockContext,
            transformSize,
            false,
            true,
            transformType,
            ref transformInfo,
            0,
            0,
            levels,
            actuals);

        // Assert
        Assert.Equal(endOfBlock, actuals[0]);
        Assert.Equal(expected, actuals);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public void RoundTripFullBlock(ushort endOfBlock)
    {
        // Assign
        const Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        const Av1TransformSize transformSize = Av1TransformSize.Size4x4;
        const Av1TransformType transformType = Av1TransformType.Identity;
        const Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        const Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        Av1BlockModeInfo modeInfo = new(blockSize, new Point(0, 0));
        Av1TransformInfo transformInfo = new(transformSize, 0, 0);
        int[] aboveContexts = new int[1];
        int[] leftContexts = new int[1];
        Av1TransformBlockContext transformBlockContext = default;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        Span<int> coefficientsBuffer = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        for (int scanIndex = endOfBlock; scanIndex < scan.Length; scanIndex++)
        {
            coefficientsBuffer[scan[scanIndex]] = 0;
        }

        Span<int> actuals = new int[16 + 1];

        // Act
        encoder.WriteCoefficients(transformSize, transformType, intraDirection, coefficientsBuffer, componentType, transformBlockContext, endOfBlock, true, filterIntraMode, usesInterTransformSet: false);

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        using Av1LevelBuffer levels = new(Configuration.Default);
        int plane = Math.Min((int)componentType, 1);
        decoder.ReadCoefficients(
            modeInfo,
            new Point(0, 0),
            aboveContexts,
            leftContexts,
            0,
            0,
            plane,
            1,
            1,
            transformBlockContext,
            transformSize,
            false,
            true,
            transformType,
            ref transformInfo,
            0,
            0,
            levels,
            actuals);

        decoder.ValidateTrailingBits();

        // Assert
        Assert.Equal(endOfBlock, actuals[0]);
    }

    [Theory]
    [MemberData(nameof(GetTransformTypes))]
    public void RoundTripFullCoefficientsYSize4x4(int txType)
    {
        // Assign
        const ushort endOfBlock = 16;
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        Av1TransformSize transformSize = blockSize.GetMaximumTransformSize();
        Av1TransformType transformType = (Av1TransformType)txType;
        Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        RoundTripCoefficientsCore(endOfBlock, componentType, blockSize, transformSize, transformType, intraDirection, filterIntraMode, true, false);
    }

    [Theory]
    [MemberData(nameof(GetTransformTypes))]
    public void RoundTripFullCoefficientsUvSize4x4(int txType)
    {
        // Assign
        const ushort endOfBlock = 16;
        const Av1ComponentType componentType = Av1ComponentType.Chroma;
        Av1BlockSize blockSize = Av1BlockSize.Block4x4;
        Av1TransformSize transformSize = blockSize.GetMaxUvTransformSize(true, true);
        Av1TransformType transformType = (Av1TransformType)txType;
        Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        RoundTripCoefficientsCore(endOfBlock, componentType, blockSize, transformSize, transformType, intraDirection, filterIntraMode, true, false);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(17)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    public void RoundTripCoefficientsYSize8x8(ushort endOfBlock)
    {
        const Av1ComponentType componentType = Av1ComponentType.Luminance;
        const Av1BlockSize blockSize = Av1BlockSize.Block8x8;
        const Av1TransformSize transformSize = Av1TransformSize.Size8x8;
        const Av1TransformType transformType = Av1TransformType.DctDct;
        const Av1PredictionMode intraDirection = Av1PredictionMode.DC;
        const Av1FilterIntraMode filterIntraMode = Av1FilterIntraMode.DC;
        RoundTripCoefficientsCore(endOfBlock, componentType, blockSize, transformSize, transformType, intraDirection, filterIntraMode, false, true);
    }

    private static void RoundTripCoefficientsCore(
        ushort endOfBlock,
        Av1ComponentType componentType,
        Av1BlockSize blockSize,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        Av1PredictionMode intraDirection,
        Av1FilterIntraMode filterIntraMode,
        bool useReducedTransformSet,
        bool useSparseCoefficients)
    {
        Av1BlockModeInfo modeInfo = new(blockSize, new Point(0, 0));
        Av1TransformInfo transformInfo = new(transformSize, 0, 0);
        int[] aboveContexts = new int[transformSize.Get4x4WideCount()];
        int[] leftContexts = new int[transformSize.Get4x4HighCount()];
        Av1TransformBlockContext transformBlockContext = default;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, 100 / 8, BaseQIndex);
        int coefficientCount = blockSize.GetHeight() * blockSize.GetWidth();
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        Span<int> coefficientsBuffer = new int[coefficientCount];

        for (int scanIndex = 0; scanIndex < endOfBlock; scanIndex++)
        {
            if (!useSparseCoefficients || scanIndex == endOfBlock - 1 || scanIndex % 4 == 0)
            {
                int level = scanIndex + 1;

                // Signed levels prove encoder context derivation uses magnitude; sparse cases also cover zero-map runs.
                coefficientsBuffer[scan[scanIndex]] = (scanIndex & 1) == 0 ? -level : level;
            }
        }

        Span<int> actuals = new int[coefficientCount + 1];

        // Act
        encoder.WriteCoefficients(
            transformSize,
            transformType,
            intraDirection,
            coefficientsBuffer,
            componentType,
            transformBlockContext,
            endOfBlock,
            useReducedTransformSet,
            filterIntraMode,
            usesInterTransformSet: false);

        using IMemoryOwner<byte> encoded = encoder.Exit();

        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        using Av1LevelBuffer levels = new(Configuration.Default);
        int plane = Math.Min((int)componentType, 1);
        decoder.ReadCoefficients(
            modeInfo,
            new Point(0, 0),
            aboveContexts,
            leftContexts,
            0,
            0,
            plane,
            1,
            1,
            transformBlockContext,
            transformSize,
            false,
            useReducedTransformSet,
            transformType,
            ref transformInfo,
            0,
            0,
            levels,
            actuals);

        decoder.ValidateTrailingBits();

        // Assert
        Assert.Equal(endOfBlock, actuals[0]);

        // The parser retains quantized levels in entropy scan order; inverse quantization maps them back to raster positions.
        for (int coefficientIndex = 0; coefficientIndex < endOfBlock; coefficientIndex++)
        {
            Assert.Equal(coefficientsBuffer[scan[coefficientIndex]], actuals[coefficientIndex + 1]);
        }
    }

    private static Av1MacroBlockModeInfo CreateModeInfo(Av1PredictionMode mode)
    {
        Av1MacroBlockModeInfo result = default;
        result.Block.Mode = mode;
        return result;
    }

    private static Av1PictureControlSet CreateEncoderPicture(
        int modeInfoColumnCount,
        int modeInfoRowCount,
        bool use128x128Superblock = false)
    {
        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        tiles.TileColumnStartModeInfo[1] = modeInfoColumnCount;
        tiles.TileRowStartModeInfo[1] = modeInfoRowCount;
        ObuSequenceHeader sequenceHeader = new() { Use128x128Superblock = use128x128Superblock };
        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = modeInfoColumnCount,
            ModeInfoRowCount = modeInfoRowCount,
            TilesInfo = tiles
        };

        Av1MacroBlockModeInfo[] modeInfoAllocation = new Av1MacroBlockModeInfo[modeInfoColumnCount * modeInfoRowCount];
        int[] modeInfoGrid = new int[modeInfoAllocation.Length];
        for (int index = 0; index < modeInfoAllocation.Length; index++)
        {
            modeInfoAllocation[index] = CreateModeInfo(Av1PredictionMode.DC);
            modeInfoGrid[index] = index;
        }

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
                    ModeInfoColumnCount = modeInfoColumnCount,
                    ModeInfoRowCount = modeInfoRowCount,
                    ModeInfoStride = modeInfoColumnCount,
                    FrameSize = new ObuFrameSize(),
                    TilesInfo = tiles
                },
                FrameHeader = frameHeader,
                PreviousQIndex = []
            },
            SegmentationNeighborMap = Memory<byte>.Empty,
            ModeInfoGrid = modeInfoGrid,
            ModeInfoAllocation = modeInfoAllocation,
            ModeInfoStride = modeInfoColumnCount,
            CdefPreset = [[-1, -1, -1, -1]]
        };
    }

    private static Av1MacroBlockD CreateMacroBlock()
    {
        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        tiles.TileColumnStartModeInfo[1] = 3;
        tiles.TileRowStartModeInfo[1] = 3;
        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = 3,
            ModeInfoRowCount = 3,
            TilesInfo = tiles
        };

        return new Av1MacroBlockD { Tile = new Av1TileInfo(0, 0, frameHeader) };
    }

    public static TheoryData<int> GetTransformTypes()
    {
        TheoryData<int> result = [];
        for (Av1TransformType transformType = Av1TransformType.DctDct; transformType < Av1TransformType.VerticalDct; transformType++)
        {
            result.Add((int)transformType);
        }

        return result;
    }
}
