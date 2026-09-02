// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies live intra superblock mode decisions, traversal, and reconstruction.
/// </summary>
[Trait("Format", "Avif")]
public class Av1IntraSuperblockEncoderTests
{
    /// <summary>
    /// Gets the normative eight-sample weights used to build independent smooth-mode fixtures.
    /// </summary>
    private static ReadOnlySpan<int> Smooth8Weights => [255, 197, 146, 105, 73, 50, 37, 32];

    [Fact]
    public void EncodesClipped128SuperblockInWriterPreorderWithoutAllocation()
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

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1);

        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.Y), (byte)128);
        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.U), (byte)128);
        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.V), (byte)128);
        ClearPlane(reconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaRed));

        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet picture = CreatePicture(modeInfo, colorConfig, use128x128Superblock: true, qIndex: 73);
        picture.Sequence.SequenceHeader.EnableFilterIntra = true;
        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            Index = 0
        };

        Av1IntraSuperblockEncoder.Encode(
            source.Frame,
            reconstruction.Frame,
            picture,
            superblock,
            coefficients,
            blockWorkspace);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 8; iteration++)
        {
            Av1IntraSuperblockEncoder.Encode(
                source.Frame,
                reconstruction.Frame,
                picture,
                superblock,
                coefficients,
                blockWorkspace);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Av1PartitionType[] expectedPartitions =
        [
            Av1PartitionType.Split,
            Av1PartitionType.Split,
            Av1PartitionType.Split,
            Av1PartitionType.Split,
            Av1PartitionType.None,
            Av1PartitionType.None,
            Av1PartitionType.None,
            Av1PartitionType.None
        ];

        for (int index = 0; index < expectedPartitions.Length; index++)
        {
            Assert.Equal(expectedPartitions[index], (Av1PartitionType)superblock.CodingUnitPartitionTypes[index]);
        }

        for (int index = 0; index < 4; index++)
        {
            Assert.True(superblock.FinalBlocks[index].HasChroma);
            Assert.Equal(73, superblock.FinalBlocks[index].QuantizationIndex);
            Assert.Equal(Av1FilterIntraMode.AllFilterIntraModes, superblock.FinalBlocks[index].FilterIntraMode);
        }

        Point[] modeInfoPositions = [new(0, 0), new(2, 0), new(0, 2), new(2, 2)];
        foreach (Point position in modeInfoPositions)
        {
            ref Av1MacroBlockModeInfo block = ref picture.GetMacroBlockModeInfo(position);
            Assert.Equal(Av1BlockSize.Block8x8, block.Block.BlockSize);
            Assert.Equal(Av1TransformSize.Size8x8, block.Block.TransformSize);
            Assert.Equal(Av1PredictionMode.DC, block.Block.Mode);
            Assert.Equal(Av1ChromaPredictionMode.DC, block.Block.UvMode);
            Assert.True(block.Block.Skip);
        }

        Span<Av1EncoderTransformBlockState> lumaStates = coefficients.GetTransformBlockSpan(0, Av1Plane.Y);
        Span<Av1EncoderTransformBlockState> blueStates = coefficients.GetTransformBlockSpan(0, Av1Plane.U);
        Span<Av1EncoderTransformBlockState> redStates = coefficients.GetTransformBlockSpan(0, Av1Plane.V);
        int[] lumaStateIndices = [0, 4, 8, 12];
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal((ushort)0, lumaStates[lumaStateIndices[index]].EndOfBlock);
            Assert.Equal(Av1TransformType.DctDct, lumaStates[lumaStateIndices[index]].TransformType);
            Assert.Equal((ushort)0, blueStates[index].EndOfBlock);
            Assert.Equal((ushort)0, redStates[index].EndOfBlock);
        }

        AssertContainsNonzero(reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y));
        AssertContainsNonzero(reconstruction.Frame.CodedView.GetPlane(Av1Plane.U));
        AssertContainsNonzero(reconstruction.Frame.CodedView.GetPlane(Av1Plane.V));

        // Edge contexts cover the complete 128x128 superblock because partition updates retain the coded geometry
        // even when most of the superblock lies beyond this deliberately clipped frame.
        const int ContextUnitCount = 128 >> Av1Constants.ModeInfoSizeLog2;
        using Av1NeighborArrayUnit<Av1PartitionContext> partitions = new(
            Configuration.Default,
            ContextUnitCount,
            ContextUnitCount)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        using Av1NeighborArrayUnit<byte> lumaContexts = new(
            Configuration.Default,
            ContextUnitCount,
            ContextUnitCount)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        using Av1NeighborArrayUnit<byte> blueContexts = new(
            Configuration.Default,
            ContextUnitCount,
            ContextUnitCount)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        using Av1NeighborArrayUnit<byte> redContexts = new(
            Configuration.Default,
            ContextUnitCount,
            ContextUnitCount)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        using Av1NeighborArrayUnit<byte> transformContexts = new(
            Configuration.Default,
            ContextUnitCount,
            ContextUnitCount)
        {
            GranularityNormalLog2 = Av1Constants.ModeInfoSizeLog2
        };

        picture.PartitionContexts = [partitions];
        picture.LuminanceDcSignLevelCoefficientNeighbors = [lumaContexts];
        picture.CbDcSignLevelCoefficientNeighbors = [blueContexts];
        picture.CrDcSignLevelCoefficientNeighbors = [redContexts];
        picture.TransformFunctionContexts = [transformContexts];
        Av1TileWriter.Av1EntropyCodingContext entropyContext = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = superblock.TileInfo },
            MacroBlockModeInfo = picture.GetMacroBlockModeInfo(default),
            SuperblockOrigin = default
        };

        using Av1SymbolEncoder writer = new(Configuration.Default, 512, 73);
        Av1TileWriter.WriteSuperblock(
            picture,
            entropyContext,
            writer,
            superblock,
            coefficients,
            tileIndex: 0);

        using IMemoryOwner<byte> encoded = writer.Exit();

        // The writer must consume exactly the transform areas populated above, proving both traversals stay synchronized.
        Assert.Equal(256, entropyContext.CodedAreaSuperblock);
        Assert.Equal(64, entropyContext.CodedAreaSuperblockUv);
        Assert.NotEqual(0, encoded.GetSpan().Length);

        using Av1EncoderFrameBuffer<byte> tileReconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1);

        ClearPlane(tileReconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<byte>>(tileReconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<byte>>(tileReconstruction.ChromaRed));
        using Av1EncoderPictureBuffer tilePicture = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            picture.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer tileCoefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace tileSuperblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace tileBlockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter tileWriter = new(
            Configuration.Default,
            source.Frame,
            tileReconstruction.Frame,
            tilePicture.Picture,
            tileCoefficients,
            tileSuperblockWorkspace,
            tileBlockWorkspace,
            initialSize: 512);

        // The production tile traversal must be byte-identical to the explicit analyze-then-write composition above.
        Assert.True(encoded.GetSpan().SequenceEqual(tileWriter.GetTileData(0)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MarksAllZeroTransformBlockAsSkipped(bool isMonochrome)
    {
        const int Width = 8;
        const int Height = 8;
        Av1ColorFormat colorFormat = isMonochrome ? Av1ColorFormat.Yuv400 : Av1ColorFormat.Yuv420;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = isMonochrome,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            colorFormat,
            1,
            1);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            colorFormat,
            1,
            1);

        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.Y), (byte)128);
        ClearPlane(reconstruction.Luma);
        if (!isMonochrome)
        {
            FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.U), (byte)128);
            FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.V), (byte)128);
            ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaBlue));
            ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaRed));
        }

        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, qIndex: 37);
        using Av1EncoderPictureBuffer pictureBuffer = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height);

        Av1PictureControlSet picture = pictureBuffer.Picture;
        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            Index = 0
        };

        Av1IntraSuperblockEncoder.Encode(
            source.Frame,
            reconstruction.Frame,
            picture,
            superblock,
            coefficients,
            blockWorkspace);

        ref Av1MacroBlockModeInfo block = ref picture.GetMacroBlockModeInfo(default);
        Assert.True(block.Block.Skip);
        Assert.Equal((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[0].EndOfBlock);
        if (!isMonochrome)
        {
            Assert.Equal((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.U)[0].EndOfBlock);
            Assert.Equal((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.V)[0].EndOfBlock);
        }

        Av1TileWriter.Av1EntropyCodingContext entropyContext = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = superblock.TileInfo },
            MacroBlockModeInfo = picture.GetMacroBlockModeInfo(default),
            SuperblockOrigin = default
        };

        using Av1SymbolEncoder writer = new(Configuration.Default, 256, 37);
        Av1TileWriter.WriteSuperblock(
            picture,
            entropyContext,
            writer,
            superblock,
            coefficients,
            tileIndex: 0);

        using IMemoryOwner<byte> precomputedTile = writer.Exit();
        using Av1EncoderFrameBuffer<byte> liveReconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            colorFormat,
            1,
            1);

        ClearPlane(liveReconstruction.Luma);
        if (!isMonochrome)
        {
            ClearPlane(Assert.IsType<Buffer2D<byte>>(liveReconstruction.ChromaBlue));
            ClearPlane(Assert.IsType<Buffer2D<byte>>(liveReconstruction.ChromaRed));
        }

        using Av1EncoderPictureBuffer livePicture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer liveCoefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace liveSuperblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace liveBlockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter liveTileWriter = new(
            Configuration.Default,
            source.Frame,
            liveReconstruction.Frame,
            livePicture.Picture,
            liveCoefficients,
            liveSuperblockWorkspace,
            liveBlockWorkspace,
            initialSize: 256);

        Assert.True(precomputedTile.GetSpan().SequenceEqual(liveTileWriter.GetTileData(0)));
    }

    [Fact]
    public void PreservesTwelveBitMonochromeReconstructionPrecision()
    {
        const int Width = 8;
        const int Height = 8;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.TwelveBit
        };

        using Av1EncoderFrameBuffer<ushort> source = new(
            Configuration.Default,
            Width,
            Height,
            12,
            Av1ColorFormat.Yuv400,
            0,
            0);

        using Av1EncoderFrameBuffer<ushort> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            12,
            Av1ColorFormat.Yuv400,
            0,
            0);

        Buffer2DRegion<ushort> sourcePlane = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < sourcePlane.Height; y++)
        {
            Span<ushort> row = sourcePlane.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = (ushort)(3000 + (((x * 71) + (y * 113)) % 1000));
            }
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet picture = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, qIndex: 37);
        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = new Av1TileInfo(0, 0, picture.Parent.FrameHeader),
            Index = 0
        };

        Av1IntraSuperblockEncoder.Encode(
            source.Frame,
            reconstruction.Frame,
            picture,
            superblock,
            coefficients,
            blockWorkspace);

        Buffer2DRegion<ushort> reconstructionPlane = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        ushort maximum = 0;
        for (int y = 0; y < reconstructionPlane.Height; y++)
        {
            foreach (ushort sample in reconstructionPlane.DangerousGetRowSpan(y))
            {
                maximum = Math.Max(maximum, sample);
                Assert.InRange(sample, (ushort)0, (ushort)4095);
            }
        }

        Assert.InRange(maximum, (ushort)(byte.MaxValue + 1), (ushort)4095);
        Assert.False(superblock.FinalBlocks[0].HasChroma);
        Assert.Equal(37, superblock.FinalBlocks[0].QuantizationIndex);
        Assert.NotEqual((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[0].EndOfBlock);
        Assert.Equal(0, coefficients.GetPlaneSpan(0, Av1Plane.U).Length);
        Assert.Equal(0, coefficients.GetPlaneSpan(0, Av1Plane.V).Length);

        using Av1EncoderFrameBuffer<ushort> tileReconstruction = new(
            Configuration.Default,
            Width,
            Height,
            12,
            Av1ColorFormat.Yuv400,
            0,
            0);

        ClearPlane(tileReconstruction.Luma);
        using Av1EncoderPictureBuffer tilePicture = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            picture.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer tileCoefficients = new(
            Configuration.Default,
            picture.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace tileSuperblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace tileBlockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter tileWriter = new(
            Configuration.Default,
            source.Frame,
            tileReconstruction.Frame,
            tilePicture.Picture,
            tileCoefficients,
            tileSuperblockWorkspace,
            tileBlockWorkspace,
            initialSize: 256);

        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
        ushort reconstructedSample = tileReconstruction.Frame.CodedView
            .GetPlane(Av1Plane.Y)
            .DangerousGetRowSpan(0)[0];

        Assert.InRange(reconstructedSample, (ushort)(byte.MaxValue + 1), (ushort)4095);
    }

    [Fact]
    public void BlockDecisionObservesLiveCdfInWriterOrder()
    {
        const int Width = 16;
        const int Height = 8;
        const int QIndex = 37;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit,
        };

        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(
            modeInfo,
            colorConfig,
            use128x128Superblock: false,
            QIndex);

        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        Av1Superblock superblock = new()
        {
            Workspace = superblockWorkspace,
            TileInfo = new Av1TileInfo(0, 0, picture.Picture.Parent.FrameHeader),
            Index = 0,
        };

        Av1IntraSuperblockEncoder.Prepare(picture.Picture, superblock, Point.Empty);
        Av1TileWriter.Av1EntropyCodingContext entropyContext = new()
        {
            MacroBlock = new Av1MacroBlockD { Tile = superblock.TileInfo },
            MacroBlockModeInfo = picture.Picture.GetMacroBlockModeInfo(default),
            SuperblockOrigin = default,
        };

        int[] costs = new int[2];
        BlockCostRecorder blockEncoder = new(costs, QIndex);
        using Av1SymbolEncoder writer = new(Configuration.Default, 256, QIndex);
        Av1TileWriter.WriteSuperblock(
            picture.Picture,
            entropyContext,
            writer,
            superblock,
            coefficients,
            tileIndex: 0,
            ref blockEncoder);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Assert.Equal(2, blockEncoder.Count);
        Assert.True(costs[1] < costs[0]);
        Assert.NotEqual(0, encoded.GetSpan().Length);
    }

    [Fact]
    public void ProductionWriterConsumesPaletteMapAndPublishesPaletteEdges()
    {
        const int Width = 8;
        const int Height = 8;
        const int QIndex = 23;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
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
            ColorConfig = colorConfig
        };

        ObuFrameHeader frameHeader = new()
        {
            AllowScreenContentTools = true,
            ModeInfoColumnCount = Width >> Av1Constants.ModeInfoSizeLog2,
            ModeInfoRowCount = Height >> Av1Constants.ModeInfoSizeLog2,
            FrameSize = new ObuFrameSize
            {
                FrameWidth = Width,
                FrameHeight = Height
            },
            TilesInfo = tiles
        };

        frameHeader.QuantizationParameters.BaseQIndex = QIndex;
        frameHeader.QuantizationParameters.QIndex.Fill(QIndex);
        byte[][] payloads = new byte[2][];
        for (int mapVariant = 0; mapVariant < payloads.Length; mapVariant++)
        {
            using Av1EncoderPictureBuffer pictureBuffer = new(
                Configuration.Default,
                sequenceHeader,
                frameHeader,
                Width,
                Height);

            using Av1EncoderCoefficientBuffer coefficients = new(
                Configuration.Default,
                sequenceHeader,
                Width,
                Height);

            using Av1EncoderSuperblockWorkspace workspace = new(Configuration.Default);
            Av1Superblock superblock = new()
            {
                Workspace = workspace,
                TileInfo = new Av1TileInfo(0, 0, frameHeader),
                Index = 0
            };

            Av1PictureControlSet picture = pictureBuffer.Picture;
            Av1IntraSuperblockEncoder.Prepare(picture, superblock, Point.Empty);
            Av1TileWriter.Av1EntropyCodingContext entropyContext = new()
            {
                MacroBlock = new Av1MacroBlockD { Tile = superblock.TileInfo },
                MacroBlockModeInfo = picture.GetMacroBlockModeInfo(default),
                SuperblockOrigin = default
            };

            PaletteBlockEncoder blockEncoder = new(workspace, QIndex, mapVariant);
            using Av1SymbolEncoder writer = new(Configuration.Default, 128, QIndex);
            Av1TileWriter.WriteSuperblock(
                picture,
                entropyContext,
                writer,
                superblock,
                coefficients,
                tileIndex: 0,
                ref blockEncoder);

            using IMemoryOwner<byte> encoded = writer.Exit();
            payloads[mapVariant] = encoded.GetSpan().ToArray();
            Assert.Equal(1, blockEncoder.Count);
            Av1NeighborArrayUnit<Av1EncoderPaletteInfo> paletteContext = Assert.Single(picture.PaletteContexts);
            for (int index = 0; index < 2; index++)
            {
                Assert.Equal(3, paletteContext.Top[index].PaletteSizes[0]);
                Assert.Equal(3, paletteContext.Left[index].PaletteSizes[0]);
                Assert.Equal([16, 128, 240], paletteContext.Top[index].GetColors(Av1Plane.Y).ToArray());
                Assert.Equal([16, 128, 240], paletteContext.Left[index].GetColors(Av1Plane.Y).ToArray());
            }

            Assert.Equal(0, paletteContext.Top[2].PaletteSizes[0]);
            Assert.Equal(0, paletteContext.Left[2].PaletteSizes[0]);
        }

        // Changing only the selected color indices must change the range-coded tile payload.
        Assert.False(payloads[0].SequenceEqual(payloads[1]));
    }

    [Fact]
    public void ProductionTileSelectsExactLumaPaletteAtFullAndClippedSizes()
    {
        AssertProductionTileSelectsExactLumaPalette(
            Av1BitDepth.EightBit,
            8,
            8,
            8,
            (byte)32,
            (byte)224,
            32,
            224,
            static (source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1IntraTileWriter(
                    Configuration.Default,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    initialSize: 256));

        AssertProductionTileSelectsExactLumaPalette(
            Av1BitDepth.TwelveBit,
            12,
            8,
            8,
            (ushort)512,
            (ushort)3584,
            512,
            3584,
            static (source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1IntraTileWriter(
                    Configuration.Default,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    initialSize: 256));

        AssertProductionTileSelectsExactLumaPalette(
            Av1BitDepth.EightBit,
            8,
            5,
            3,
            (byte)48,
            (byte)208,
            48,
            208,
            static (source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new Av1IntraTileWriter(
                    Configuration.Default,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    initialSize: 256));
    }

    [Theory]
    [InlineData((int)Av1PredictionMode.Vertical, 0)]
    [InlineData((int)Av1PredictionMode.Horizontal, 0)]
    [InlineData((int)Av1PredictionMode.Smooth, 0)]
    [InlineData((int)Av1PredictionMode.Paeth, 0)]
    [InlineData((int)Av1PredictionMode.SmoothVertical, 0)]
    [InlineData((int)Av1PredictionMode.SmoothHorizontal, 0)]
    [InlineData((int)Av1PredictionMode.Directional135Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional203Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional157Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional67Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional113Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional45Degrees, 0)]
    [InlineData((int)Av1PredictionMode.Directional45Degrees, -3)]
    [InlineData((int)Av1PredictionMode.Directional45Degrees, 3)]
    [InlineData((int)Av1PredictionMode.Directional135Degrees, -3)]
    [InlineData((int)Av1PredictionMode.Directional135Degrees, 3)]
    [InlineData((int)Av1PredictionMode.Directional203Degrees, -3)]
    [InlineData((int)Av1PredictionMode.Directional203Degrees, 3)]
    public void ProductionTileSelectsModeFromCurrentReconstruction(int expectedModeValue, int expectedAngleDelta)
    {
        const int Width = 16;
        const int Height = 16;
        const byte TopReference = 48;
        const byte LeftReference = 208;
        const int QIndex = 1;
        Av1PredictionMode expectedMode = (Av1PredictionMode)expectedModeValue;
        bool isDiagonal = expectedMode is >= Av1PredictionMode.Directional45Degrees and <= Av1PredictionMode.Directional67Degrees;
        int cornerReference = expectedMode == Av1PredictionMode.Horizontal
            ? LeftReference
            : expectedMode == Av1PredictionMode.Vertical ? TopReference : 128;

        Span<byte> directionalTarget = stackalloc byte[64];
        if (isDiagonal)
        {
            Span<byte> aboveStorage = stackalloc byte[17];
            Span<byte> above = aboveStorage[1..];
            Span<byte> leftStorage = stackalloc byte[17];
            Span<byte> left = leftStorage[1..];
            aboveStorage[0] = 128;
            leftStorage[0] = 128;
            for (int i = 0; i < 8; i++)
            {
                above[i] = (byte)(32 + (i * 24));
                left[i] = (byte)(224 - (i * 24));
            }

            above[8..].Fill(above[7]);
            left[8..].Fill(left[7]);

            // Directional arithmetic has separate byte-exact reference coverage. This fixture uses its scalar
            // path only to isolate production mode traversal, reference gathering, and rate-distortion selection.
            Av1DirectionalIntraPredictor.PredictScalar(
                directionalTarget,
                8,
                Av1TransformSize.Size8x8,
                above,
                left,
                false,
                false,
                expectedMode.ToAngle() + (expectedAngleDelta * Av1Constants.AngleStep));
        }

        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        Buffer2DRegion<byte> sourcePlane = source.Frame.CodedView.GetPlane(Av1Plane.Y);

        // The first three 8x8 blocks establish the corner, top, and left reconstruction consumed by
        // the bottom-right target. This makes the assertion exercise production traversal and live state.
        for (int y = 0; y < Height; y++)
        {
            Span<byte> row = sourcePlane.DangerousGetRowSpan(y);
            for (int x = 0; x < Width; x++)
            {
                int rowIndex = y - 8;
                int columnIndex = x - 8;
                int value;
                if (y < 8)
                {
                    value = x < 8
                        ? cornerReference
                        : isDiagonal
                            ? 32 + (columnIndex * 24)
                            : expectedMode == Av1PredictionMode.Paeth ? 40 + (columnIndex * 20) : TopReference;
                }
                else if (x < 8)
                {
                    value = isDiagonal
                        ? 224 - (rowIndex * 24)
                        : expectedMode == Av1PredictionMode.Paeth ? 200 - (rowIndex * 20) : LeftReference;
                }
                else if (isDiagonal)
                {
                    value = directionalTarget[(rowIndex * 8) + columnIndex];
                }
                else if (expectedMode == Av1PredictionMode.Paeth)
                {
                    // Build the target from the nearest of left, top, and corner without calling the production predictor.
                    int top = 40 + (columnIndex * 20);
                    int left = 200 - (rowIndex * 20);
                    int predictor = top + left - 128;
                    int leftDistance = Math.Abs(predictor - left);
                    int topDistance = Math.Abs(predictor - top);
                    int cornerDistance = Math.Abs(predictor - 128);

                    value = leftDistance <= topDistance && leftDistance <= cornerDistance
                        ? left
                        : topDistance <= cornerDistance ? top : 128;
                }
                else
                {
                    // Apply the normative interpolation directly so a production predictor cannot generate its own fixture.
                    int rowWeight = Smooth8Weights[rowIndex];
                    int columnWeight = Smooth8Weights[columnIndex];
                    value = expectedMode switch
                    {
                        Av1PredictionMode.Horizontal => LeftReference,
                        Av1PredictionMode.Vertical => TopReference,
                        Av1PredictionMode.SmoothVertical => ((rowWeight * TopReference) + ((256 - rowWeight) * LeftReference) + 128) >> 8,
                        Av1PredictionMode.SmoothHorizontal => ((columnWeight * LeftReference) + ((256 - columnWeight) * TopReference) + 128) >> 8,
                        _ => ((rowWeight * TopReference) + ((256 - rowWeight) * LeftReference) +
                            (columnWeight * LeftReference) + ((256 - columnWeight) * TopReference) + 256) >> 9
                    };
                }

                row[x] = (byte)value;
            }
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter tileWriter = new(
            Configuration.Default,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            initialSize: 512);

        ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(2, 2));
        Assert.Equal(expectedMode, targetBlock.Block.Mode);
        Assert.Equal(
            expectedAngleDelta,
            superblockWorkspace.FinalBlocks[3].PredictionUnit.AngleDelta[(int)Av1PlaneType.Y]);

        Av1EncoderTransformBlockState targetState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[12];

        // Every transform has the same skip cost for this exact-prediction target, so reference enum order
        // requires DCT-DCT to win even when the mode-derived first pass used another transform.
        Assert.Equal((ushort)0, targetState.EndOfBlock);
        Assert.Equal(Av1TransformType.DctDct, targetState.TransformType);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Theory]
    [InlineData((int)Av1ChromaPredictionMode.Vertical, 0, (int)Av1TransformType.AdstDct, (int)Av1ColorFormat.Yuv444)]
    [InlineData((int)Av1ChromaPredictionMode.Horizontal, 0, (int)Av1TransformType.DctAdst, (int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ChromaPredictionMode.Paeth, 0, (int)Av1TransformType.AdstAdst, (int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ChromaPredictionMode.Directional45Degrees, -3, (int)Av1TransformType.DctDct, (int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ChromaPredictionMode.Directional135Degrees, 3, (int)Av1TransformType.AdstAdst, (int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ChromaPredictionMode.Directional203Degrees, -3, (int)Av1TransformType.DctAdst, (int)Av1ColorFormat.Yuv444)]
    public void ProductionTileSelectsChromaModeFromCurrentReconstruction(
        int expectedModeValue,
        int expectedAngleDelta,
        int expectedTransformTypeValue,
        int colorFormatValue)
    {
        const int Width = 16;
        const int Height = 16;
        const int QIndex = 1;
        Av1ChromaPredictionMode expectedMode = (Av1ChromaPredictionMode)expectedModeValue;
        Av1TransformType expectedTransformType = (Av1TransformType)expectedTransformTypeValue;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        bool subsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422;
        bool subsamplingY = colorFormat == Av1ColorFormat.Yuv420;
        int chromaSubsamplingX = subsamplingX ? 1 : 0;
        int chromaSubsamplingY = subsamplingY ? 1 : 0;
        Av1TransformSize transformSize = Av1BlockSize.Block8x8.GetMaxUvTransformSize(
            subsamplingX,
            subsamplingY);

        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = subsamplingX,
            SubSamplingY = subsamplingY,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY);

        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.Y), (byte)128);
        FillChromaModeSelectionPlane(
            source.Frame.CodedView.GetPlane(Av1Plane.U),
            transformSize,
            expectedMode,
            expectedAngleDelta);

        FillChromaModeSelectionPlane(
            source.Frame.CodedView.GetPlane(Av1Plane.V),
            transformSize,
            expectedMode,
            expectedAngleDelta);

        ClearPlane(reconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaRed));
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter tileWriter = new(
            Configuration.Default,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            initialSize: 512);

        ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(2, 2));
        Assert.Equal(expectedMode, targetBlock.Block.UvMode);
        Assert.Equal(
            expectedAngleDelta,
            superblockWorkspace.FinalBlocks[3].PredictionUnit.AngleDelta[(int)Av1PlaneType.Uv]);

        int targetTransformIndex = (3 * transformSize.GetSize2d()) /
            Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

        Av1EncoderTransformBlockState blueState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.U)[targetTransformIndex];

        Av1EncoderTransformBlockState redState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.V)[targetTransformIndex];

        Assert.NotEqual((ushort)0, blueState.EndOfBlock);
        Assert.NotEqual((ushort)0, redState.EndOfBlock);
        Assert.Equal(expectedTransformType, blueState.TransformType);
        Assert.Equal(expectedTransformType, redState.TransformType);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv420)]
    [InlineData((int)Av1ColorFormat.Yuv422)]
    [InlineData((int)Av1ColorFormat.Yuv444)]
    public void ProductionTileSelectsChromaFromReconstructedLuma(int colorFormatValue)
        => VerifyProductionTileSelectsChromaFromReconstructedLuma<byte>(
            colorFormatValue,
            8,
            static (source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    Configuration.Default,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    initialSize: 512));

    [Theory]
    [InlineData((int)Av1ColorFormat.Yuv420, 10)]
    [InlineData((int)Av1ColorFormat.Yuv420, 12)]
    [InlineData((int)Av1ColorFormat.Yuv422, 10)]
    [InlineData((int)Av1ColorFormat.Yuv422, 12)]
    [InlineData((int)Av1ColorFormat.Yuv444, 10)]
    [InlineData((int)Av1ColorFormat.Yuv444, 12)]
    public void ProductionTileSelectsChromaFromReconstructedLumaHighBitDepth(
        int colorFormatValue,
        int bitDepth)
        => VerifyProductionTileSelectsChromaFromReconstructedLuma<ushort>(
            colorFormatValue,
            bitDepth,
            static (source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    Configuration.Default,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    initialSize: 512));

    private static void VerifyProductionTileSelectsChromaFromReconstructedLuma<TSample>(
        int colorFormatValue,
        int bitDepth,
        TileWriterFactory<TSample> createWriter)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        const int Width = 16;
        const int Height = 16;
        const int QIndex = 1;
        const int AlphaU = 16;
        const int AlphaV = -16;
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        bool subsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422;
        bool subsamplingY = colorFormat == Av1ColorFormat.Yuv420;
        int chromaSubsamplingX = subsamplingX ? 1 : 0;
        int chromaSubsamplingY = subsamplingY ? 1 : 0;
        int sampleScale = 1 << (bitDepth - 8);
        int midpoint = 1 << (bitDepth - 1);
        int maxSample = (1 << bitDepth) - 1;
        Av1TransformSize transformSize = Av1BlockSize.Block8x8.GetMaxUvTransformSize(
            subsamplingX,
            subsamplingY);

        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = false,
            SubSamplingX = subsamplingX,
            SubSamplingY = subsamplingY,
            BitDepth = (Av1BitDepth)((bitDepth - 8) / 2)
        };

        using Av1EncoderFrameBuffer<TSample> pilotSource = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY);

        using Av1EncoderFrameBuffer<TSample> pilotReconstruction = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY);

        Buffer2DRegion<TSample> pilotLuma = pilotSource.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < pilotLuma.Height; y++)
        {
            Span<TSample> row = pilotLuma.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = TSample.CreateChecked(
                    (96 + (((x * 29) + (y * 47) + (((x ^ y) & 1) * 53)) & 63)) * sampleScale);
            }
        }

        FillPlane(pilotSource.Frame.CodedView.GetPlane(Av1Plane.U), TSample.CreateChecked(midpoint));
        FillPlane(pilotSource.Frame.CodedView.GetPlane(Av1Plane.V), TSample.CreateChecked(midpoint));
        ClearPlane(pilotReconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<TSample>>(pilotReconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<TSample>>(pilotReconstruction.ChromaRed));
        using Av1EncoderModeInfoBuffer pilotModeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pilotTemplate = CreatePicture(pilotModeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer pilotPicture = new(
            Configuration.Default,
            pilotTemplate.Sequence.SequenceHeader,
            pilotTemplate.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer pilotCoefficients = new(
            Configuration.Default,
            pilotTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace pilotSuperblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace pilotBlockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter pilotWriter = createWriter(
            pilotSource.Frame,
            pilotReconstruction.Frame,
            pilotPicture.Picture,
            pilotCoefficients,
            pilotSuperblockWorkspace,
            pilotBlockWorkspace);

        Buffer2DRegion<TSample> reconstructedLuma = pilotReconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        int chromaWidth = transformSize.GetWidth();
        int chromaHeight = transformSize.GetHeight();
        int sampleCount = transformSize.GetSize2d();
        int lumaScaleShift = 3 - chromaSubsamplingX - chromaSubsamplingY;
        Span<short> lumaQ3 = stackalloc short[64];
        int sumQ3 = sampleCount >> 1;
        for (int row = 0; row < chromaHeight; row++)
        {
            for (int column = 0; column < chromaWidth; column++)
            {
                int lumaSum = 0;
                int lumaX = 8 + (column << chromaSubsamplingX);
                int lumaY = 8 + (row << chromaSubsamplingY);
                for (int offsetY = 0; offsetY <= chromaSubsamplingY; offsetY++)
                {
                    ReadOnlySpan<TSample> lumaRow = reconstructedLuma.DangerousGetRowSpan(lumaY + offsetY);
                    for (int offsetX = 0; offsetX <= chromaSubsamplingX; offsetX++)
                    {
                        lumaSum += int.CreateChecked(lumaRow[lumaX + offsetX]);
                    }
                }

                short sampleQ3 = (short)(lumaSum << lumaScaleShift);
                lumaQ3[(row * chromaWidth) + column] = sampleQ3;
                sumQ3 += sampleQ3;
            }
        }

        int averageQ3 = sumQ3 >> (transformSize.GetBlockWidthLog2() + transformSize.GetBlockHeightLog2());
        using Av1EncoderFrameBuffer<TSample> source = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY);

        using Av1EncoderFrameBuffer<TSample> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            colorFormat,
            chromaSubsamplingX,
            chromaSubsamplingY);

        for (int y = 0; y < pilotLuma.Height; y++)
        {
            pilotLuma.DangerousGetRowSpan(y).CopyTo(source.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(y));
        }

        Buffer2DRegion<TSample> blue = source.Frame.CodedView.GetPlane(Av1Plane.U);
        Buffer2DRegion<TSample> red = source.Frame.CodedView.GetPlane(Av1Plane.V);
        FillPlane(blue, TSample.CreateChecked(midpoint));
        FillPlane(red, TSample.CreateChecked(midpoint));
        for (int row = 0; row < chromaHeight; row++)
        {
            Span<TSample> blueRow = blue.DangerousGetRowSpan(chromaHeight + row);
            Span<TSample> redRow = red.DangerousGetRowSpan(chromaHeight + row);
            for (int column = 0; column < chromaWidth; column++)
            {
                int acQ3 = lumaQ3[(row * chromaWidth) + column] - averageQ3;
                int blueProduct = AlphaU * acQ3;
                int redProduct = AlphaV * acQ3;
                int blueAdjustment = (blueProduct + 32 + (blueProduct >> 31)) >> 6;
                int redAdjustment = (redProduct + 32 + (redProduct >> 31)) >> 6;
                blueRow[chromaWidth + column] = TSample.CreateChecked(Math.Clamp(midpoint + blueAdjustment, 0, maxSample));
                redRow[chromaWidth + column] = TSample.CreateChecked(Math.Clamp(midpoint + redAdjustment, 0, maxSample));
            }
        }

        ClearPlane(reconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<TSample>>(reconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<TSample>>(reconstruction.ChromaRed));
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter tileWriter = createWriter(
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace);

        Buffer2DRegion<TSample> actualLuma = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < reconstructedLuma.Height; y++)
        {
            Assert.Equal(reconstructedLuma.DangerousGetRowSpan(y), actualLuma.DangerousGetRowSpan(y));
        }

        ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(2, 2));
        Assert.Equal(Av1ChromaPredictionMode.ChromaFromLuma, targetBlock.Block.UvMode);
        Assert.Equal(
            Av1ChromaFromLumaMath.JointSign(
                Av1ChromaFromLumaMath.SignPositive,
                Av1ChromaFromLumaMath.SignNegative),
            superblockWorkspace.FinalBlocks[3].PredictionUnit.ChromaFromLumaSigns);

        Assert.Equal(
            Av1ChromaFromLumaMath.PackIndices(
                Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(AlphaU),
                Av1ChromaFromLumaMath.AlphaToMagnitudeIndex(AlphaV)),
            superblockWorkspace.FinalBlocks[3].PredictionUnit.ChromaFromLumaIndex);

        int targetTransformIndex = (3 * sampleCount) /
            Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

        Av1EncoderTransformBlockState blueState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.U)[targetTransformIndex];

        Av1EncoderTransformBlockState redState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.V)[targetTransformIndex];

        Assert.Equal((ushort)0, blueState.EndOfBlock);
        Assert.Equal((ushort)0, redState.EndOfBlock);
        Assert.Equal(Av1TransformType.DctDct, blueState.TransformType);
        Assert.Equal(Av1TransformType.DctDct, redState.TransformType);
        Assert.NotEqual(0, pilotWriter.GetTileData(0).Length);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Theory]
    [InlineData((int)Av1FilterIntraMode.DC)]
    [InlineData((int)Av1FilterIntraMode.Vertical)]
    [InlineData((int)Av1FilterIntraMode.Horizontal)]
    [InlineData((int)Av1FilterIntraMode.Directional157)]
    [InlineData((int)Av1FilterIntraMode.Paeth)]
    public void ProductionTileSelectsFilterIntraMode(int filterIntraModeValue)
        => VerifyProductionTileSelectsFilterIntraMode<byte>(
            filterIntraModeValue,
            8,
            static (source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    Configuration.Default,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    initialSize: 512),
            static (mode, destination, above, left, _, scratch) =>
                Av1FilterIntraPredictorBase.GetPredictor(mode)
                    .Predict(destination, 8, above, left, 8, 8, scratch));

    [Theory]
    [InlineData((int)Av1FilterIntraMode.DC, 10)]
    [InlineData((int)Av1FilterIntraMode.DC, 12)]
    [InlineData((int)Av1FilterIntraMode.Vertical, 10)]
    [InlineData((int)Av1FilterIntraMode.Vertical, 12)]
    [InlineData((int)Av1FilterIntraMode.Horizontal, 10)]
    [InlineData((int)Av1FilterIntraMode.Horizontal, 12)]
    [InlineData((int)Av1FilterIntraMode.Directional157, 10)]
    [InlineData((int)Av1FilterIntraMode.Directional157, 12)]
    [InlineData((int)Av1FilterIntraMode.Paeth, 10)]
    [InlineData((int)Av1FilterIntraMode.Paeth, 12)]
    public void ProductionTileSelectsFilterIntraModeHighBitDepth(
        int filterIntraModeValue,
        int bitDepth)
        => VerifyProductionTileSelectsFilterIntraMode<ushort>(
            filterIntraModeValue,
            bitDepth,
            static (source, reconstruction, picture, coefficients, superblockWorkspace, blockWorkspace) =>
                new(
                    Configuration.Default,
                    source,
                    reconstruction,
                    picture,
                    coefficients,
                    superblockWorkspace,
                    blockWorkspace,
                    initialSize: 512),
            static (mode, destination, above, left, sampleBitDepth, scratch) =>
                Av1FilterIntraPredictorBase.GetPredictor(mode)
                    .Predict(
                        MemoryMarshal.Cast<ushort, short>(destination),
                        8,
                        MemoryMarshal.Cast<ushort, short>(above),
                        MemoryMarshal.Cast<ushort, short>(left),
                        8,
                        8,
                        sampleBitDepth,
                        MemoryMarshal.Cast<ushort, short>(scratch)));

    private static void VerifyProductionTileSelectsFilterIntraMode<TSample>(
        int filterIntraModeValue,
        int bitDepth,
        TileWriterFactory<TSample> createWriter,
        FilterPrediction<TSample> predictFilter)
        where TSample : unmanaged, IBinaryInteger<TSample>
    {
        const int Width = 16;
        const int Height = 16;
        const int QIndex = 1;
        const int TargetX = 8;
        const int TargetY = 8;
        const Av1TransformSize TransformSize = Av1TransformSize.Size8x8;
        Av1FilterIntraMode filterIntraMode = (Av1FilterIntraMode)filterIntraModeValue;
        int sampleScale = 1 << (bitDepth - 8);
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = (Av1BitDepth)((bitDepth - 8) / 2)
        };

        using Av1EncoderFrameBuffer<TSample> pilotSource = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            Av1ColorFormat.Yuv400,
            1,
            1);

        using Av1EncoderFrameBuffer<TSample> pilotReconstruction = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            Av1ColorFormat.Yuv400,
            1,
            1);

        Buffer2DRegion<TSample> pilotLuma = pilotSource.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < pilotLuma.Height; y++)
        {
            Span<TSample> row = pilotLuma.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = TSample.CreateChecked(
                    (64 + (((x * 71) + (y * 109) + (((x ^ y) & 3) * 37)) & 127)) * sampleScale);
            }
        }

        ClearPlane(pilotReconstruction.Luma);
        using Av1EncoderModeInfoBuffer pilotModeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pilotTemplate = CreatePicture(pilotModeInfo, colorConfig, use128x128Superblock: false, QIndex);
        pilotTemplate.Sequence.SequenceHeader.EnableFilterIntra = true;
        using Av1EncoderPictureBuffer pilotPicture = new(
            Configuration.Default,
            pilotTemplate.Sequence.SequenceHeader,
            pilotTemplate.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer pilotCoefficients = new(
            Configuration.Default,
            pilotTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace pilotSuperblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace pilotBlockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter pilotWriter = createWriter(
            pilotSource.Frame,
            pilotReconstruction.Frame,
            pilotPicture.Picture,
            pilotCoefficients,
            pilotSuperblockWorkspace,
            pilotBlockWorkspace);

        Buffer2DRegion<TSample> reconstructedLuma = pilotReconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        Span<TSample> aboveStorage = stackalloc TSample[9];
        Span<TSample> above = aboveStorage[1..];
        Span<TSample> left = stackalloc TSample[8];
        ReadOnlySpan<TSample> reconstructedAbove = reconstructedLuma.DangerousGetRowSpan(TargetY - 1);
        aboveStorage[0] = reconstructedAbove[TargetX - 1];
        reconstructedAbove.Slice(TargetX, 8).CopyTo(above);
        for (int row = 0; row < 8; row++)
        {
            left[row] = reconstructedLuma.DangerousGetRowSpan(TargetY + row)[TargetX - 1];
        }

        Span<TSample> target = stackalloc TSample[TransformSize.GetSize2d()];
        Span<TSample> filterScratch = stackalloc TSample[Av1FilterIntraPredictorBase.ScratchLength];
        predictFilter(filterIntraMode, target, above, left, bitDepth, filterScratch);

        using Av1EncoderFrameBuffer<TSample> source = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            Av1ColorFormat.Yuv400,
            1,
            1);

        using Av1EncoderFrameBuffer<TSample> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            bitDepth,
            Av1ColorFormat.Yuv400,
            1,
            1);

        Buffer2DRegion<TSample> sourceLuma = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int y = 0; y < pilotLuma.Height; y++)
        {
            pilotLuma.DangerousGetRowSpan(y).CopyTo(sourceLuma.DangerousGetRowSpan(y));
        }

        for (int row = 0; row < 8; row++)
        {
            target.Slice(row * 8, 8).CopyTo(sourceLuma.DangerousGetRowSpan(TargetY + row).Slice(TargetX, 8));
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        pictureTemplate.Sequence.SequenceHeader.EnableFilterIntra = true;
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter tileWriter = createWriter(
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace);

        Buffer2DRegion<TSample> actualLuma = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        Assert.Equal(above, actualLuma.DangerousGetRowSpan(TargetY - 1).Slice(TargetX, 8));
        for (int row = 0; row < 8; row++)
        {
            Assert.Equal(left[row], actualLuma.DangerousGetRowSpan(TargetY + row)[TargetX - 1]);
            Assert.Equal(
                target.Slice(row * 8, 8),
                actualLuma.DangerousGetRowSpan(TargetY + row).Slice(TargetX, 8));
        }

        ref Av1MacroBlockModeInfo targetBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(2, 2));
        Assert.Equal(Av1PredictionMode.DC, targetBlock.Block.Mode);
        Assert.Equal(filterIntraMode, superblockWorkspace.FinalBlocks[3].FilterIntraMode);

        int targetTransformIndex = (3 * TransformSize.GetSize2d()) /
            Av1EncoderCoefficientBuffer.TransformBlockUnitCoefficientCount;

        Av1EncoderTransformBlockState targetState =
            coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[targetTransformIndex];

        Assert.Equal((ushort)0, targetState.EndOfBlock);
        Assert.Equal(Av1TransformType.DctDct, targetState.TransformType);
        Assert.NotEqual(0, pilotWriter.GetTileData(0).Length);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Fact]
    public void ProductionDirectionalModesConsumeAvailableExtendedEdges()
    {
        const int Width = 72;
        const int Height = 16;
        const int QIndex = 1;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        Buffer2DRegion<byte> sourcePlane = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        FillPlane(sourcePlane, (byte)128);

        Span<byte> aboveStorage = stackalloc byte[17];
        Span<byte> above = aboveStorage[1..];
        Span<byte> leftStorage = stackalloc byte[17];
        Span<byte> left = leftStorage[1..];
        aboveStorage[0] = 128;
        leftStorage[0] = 128;
        for (int i = 0; i < 16; i++)
        {
            above[i] = (byte)(32 + (i * 12));
            left[i] = (byte)(224 - (i * 12));
        }

        Span<byte> topRightTarget = stackalloc byte[64];
        Span<byte> bottomLeftTarget = stackalloc byte[64];
        Span<byte> predictionScratch = stackalloc byte[64];
        Av1DirectionalIntraPredictor.Predict(
            topRightTarget,
            8,
            Av1TransformSize.Size8x8,
            above,
            left,
            false,
            false,
            45,
            predictionScratch);

        Av1DirectionalIntraPredictor.Predict(
            bottomLeftTarget,
            8,
            Av1TransformSize.Size8x8,
            above,
            left,
            false,
            false,
            203,
            predictionScratch);

        // The lower-left target consumes top-right samples from the already reconstructed row above.
        // The upper-right superblock target consumes bottom-left samples from the completed superblock to its left.
        for (int y = 0; y < Height; y++)
        {
            Span<byte> row = sourcePlane.DangerousGetRowSpan(y);
            if (y < 8)
            {
                above.CopyTo(row[..16]);
                bottomLeftTarget.Slice(y * 8, 8).CopyTo(row.Slice(64, 8));
            }
            else
            {
                topRightTarget.Slice((y - 8) * 8, 8).CopyTo(row[..8]);
            }

            row.Slice(56, 8).Fill(left[y]);
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet pictureTemplate = CreatePicture(modeInfo, colorConfig, use128x128Superblock: false, QIndex);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter tileWriter = new(
            Configuration.Default,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            initialSize: 2048);

        ref Av1MacroBlockModeInfo topRightBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(0, 2));
        ref Av1MacroBlockModeInfo bottomLeftBlock = ref picture.Picture.GetMacroBlockModeInfo(new Point(16, 0));
        Assert.Equal(Av1PredictionMode.Directional45Degrees, topRightBlock.Block.Mode);
        Assert.Equal(Av1PredictionMode.Directional203Degrees, bottomLeftBlock.Block.Mode);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    [Fact]
    public void TileWriterMapsClippedRasterTraversalToEverySuperblockCoefficientSegment()
    {
        const int Width = 72;
        const int Height = 72;
        const int QIndex = 53;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = Av1BitDepth.EightBit
        };

        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        int modeInfoColumnCount = Width >> Av1Constants.ModeInfoSizeLog2;
        int modeInfoRowCount = Height >> Av1Constants.ModeInfoSizeLog2;
        tiles.TileColumnStartModeInfo[1] = modeInfoColumnCount;
        tiles.TileRowStartModeInfo[1] = modeInfoRowCount;
        ObuSequenceHeader sequenceHeader = new()
        {
            Use128x128Superblock = false,
            ColorConfig = colorConfig
        };

        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = modeInfoColumnCount,
            ModeInfoRowCount = modeInfoRowCount,
            TilesInfo = tiles
        };

        frameHeader.QuantizationParameters.BaseQIndex = QIndex;
        frameHeader.QuantizationParameters.QIndex.Fill(QIndex);
        using Av1EncoderFrameBuffer<byte> source = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        using Av1EncoderFrameBuffer<byte> reconstruction = new(
            Configuration.Default,
            Width,
            Height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.Y), 251, 29);
        ClearPlane(reconstruction.Luma);
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            sequenceHeader,
            frameHeader,
            Width,
            Height);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            sequenceHeader,
            Width,
            Height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter tileWriter = new(
            Configuration.Default,
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace,
            initialSize: 4096);

        Assert.Equal(4, coefficients.SuperblockCount);
        bool usesNonDctTransform = false;
        for (int superblockIndex = 0; superblockIndex < coefficients.SuperblockCount; superblockIndex++)
        {
            Span<Av1EncoderTransformBlockState> transformBlocks =
                coefficients.GetTransformBlockSpan(superblockIndex, Av1Plane.Y);

            Assert.NotEqual((ushort)0, transformBlocks[0].EndOfBlock);
            foreach (Av1EncoderTransformBlockState transformBlock in transformBlocks)
            {
                usesNonDctTransform |=
                    transformBlock.EndOfBlock > 0 && transformBlock.TransformType != Av1TransformType.DctDct;
            }
        }

        Assert.True(usesNonDctTransform);
        Assert.NotEqual(
            (byte)0,
            reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y).DangerousGetRowSpan(Height - 1)[Width - 1]);

        ref Av1MacroBlockModeInfo bottomRight = ref picture.Picture.GetMacroBlockModeInfo(new Point(16, 16));
        Assert.Equal(Av1BlockSize.Block8x8, bottomRight.Block.BlockSize);
        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    private static Av1PictureControlSet CreatePicture(
        Av1EncoderModeInfoBuffer modeInfo,
        ObuColorConfig colorConfig,
        bool use128x128Superblock,
        int qIndex)
    {
        ObuTileGroupHeader tiles = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        tiles.TileColumnStartModeInfo[1] = modeInfo.ModeInfoColumnCount;
        tiles.TileRowStartModeInfo[1] = modeInfo.ModeInfoRowCount;
        ObuSequenceHeader sequenceHeader = new()
        {
            Use128x128Superblock = use128x128Superblock,
            ColorConfig = colorConfig
        };

        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = modeInfo.ModeInfoColumnCount,
            ModeInfoRowCount = modeInfo.ModeInfoRowCount,
            TilesInfo = tiles
        };

        frameHeader.QuantizationParameters.BaseQIndex = qIndex;
        frameHeader.QuantizationParameters.QIndex.Fill(qIndex);
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
                    ModeInfoColumnCount = modeInfo.ModeInfoColumnCount,
                    ModeInfoRowCount = modeInfo.ModeInfoRowCount,
                    ModeInfoStride = modeInfo.ModeInfoStride,
                    TilesInfo = tiles,
                    FrameSize = new ObuFrameSize()
                },
                FrameHeader = frameHeader,
                PreviousQIndex = [qIndex]
            },
            SegmentationNeighborMap = new byte[modeInfo.ModeInfoColumnCount * modeInfo.ModeInfoRowCount],
            ModeInfoGrid = modeInfo.Grid,
            ModeInfoAllocation = modeInfo.Allocation,
            ModeInfoStride = modeInfo.ModeInfoStride,
            Disallow4x4AllFrames = modeInfo.Disallow4x4AllFrames,
            CdefPreset = [[-1, -1, -1, -1]]
        };
    }

    private static void AssertProductionTileSelectsExactLumaPalette<TSample>(
        Av1BitDepth bitDepth,
        int bitDepthValue,
        int width,
        int height,
        TSample lowerColor,
        TSample upperColor,
        ushort expectedLowerColor,
        ushort expectedUpperColor,
        TileWriterFactory<TSample> createTileWriter)
        where TSample : unmanaged
    {
        const int QIndex = 37;
        ObuColorConfig colorConfig = new()
        {
            IsMonochrome = true,
            SubSamplingX = true,
            SubSamplingY = true,
            BitDepth = bitDepth
        };

        using Av1EncoderFrameBuffer<TSample> source = new(
            Configuration.Default,
            width,
            height,
            bitDepthValue,
            Av1ColorFormat.Yuv400,
            0,
            0);

        using Av1EncoderFrameBuffer<TSample> reconstruction = new(
            Configuration.Default,
            width,
            height,
            bitDepthValue,
            Av1ColorFormat.Yuv400,
            0,
            0);

        Buffer2DRegion<TSample> sourcePlane = source.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int row = 0; row < sourcePlane.Height; row++)
        {
            int visibleRow = Math.Min(row, height - 1);
            sourcePlane.DangerousGetRowSpan(row).Fill(visibleRow < height / 2 ? lowerColor : upperColor);
        }

        ClearPlane(reconstruction.Luma);
        using Av1EncoderModeInfoBuffer modeInfo = new(
            Configuration.Default,
            width,
            height,
            disallow4x4AllFrames: true);

        Av1PictureControlSet pictureTemplate = CreatePicture(
            modeInfo,
            colorConfig,
            use128x128Superblock: false,
            QIndex);

        pictureTemplate.Parent.FrameHeader.AllowScreenContentTools = true;
        pictureTemplate.Parent.FrameHeader.FrameSize.FrameWidth = width;
        pictureTemplate.Parent.FrameHeader.FrameSize.FrameHeight = height;
        using Av1EncoderPictureBuffer picture = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            pictureTemplate.Parent.FrameHeader,
            width,
            height);

        using Av1EncoderCoefficientBuffer coefficients = new(
            Configuration.Default,
            pictureTemplate.Sequence.SequenceHeader,
            width,
            height);

        using Av1EncoderSuperblockWorkspace superblockWorkspace = new(Configuration.Default);
        using Av1EncoderBlockWorkspace blockWorkspace = new(Configuration.Default);
        using Av1IntraTileWriter tileWriter = createTileWriter(
            source.Frame,
            reconstruction.Frame,
            picture.Picture,
            coefficients,
            superblockWorkspace,
            blockWorkspace);

        ref Av1MacroBlockModeInfo mode = ref picture.Picture.GetMacroBlockModeInfo(default);
        Assert.Equal(Av1PredictionMode.DC, mode.Block.Mode);
        Assert.Equal(Av1FilterIntraMode.AllFilterIntraModes, superblockWorkspace.FinalBlocks[0].FilterIntraMode);
        Assert.Equal((ushort)0, coefficients.GetTransformBlockSpan(0, Av1Plane.Y)[0].EndOfBlock);
        Assert.Equal(2, superblockWorkspace.PaletteInfo.PaletteSizes[0]);
        Assert.Equal(
            [expectedLowerColor, expectedUpperColor],
            superblockWorkspace.PaletteInfo.GetColors(Av1Plane.Y).ToArray());

        Buffer2DRegion<byte> colorIndexMap = superblockWorkspace
            .GetPaletteMaps()
            .GetMap(Av1PlaneType.Y, 8, 8);

        Buffer2DRegion<TSample> reconstructionPlane = reconstruction.Frame.CodedView.GetPlane(Av1Plane.Y);
        for (int row = 0; row < reconstructionPlane.Height; row++)
        {
            int visibleRow = Math.Min(row, height - 1);
            byte expectedIndex = (byte)(visibleRow < height / 2 ? 0 : 1);
            foreach (byte index in colorIndexMap.DangerousGetRowSpan(row))
            {
                Assert.Equal(expectedIndex, index);
            }

            Assert.True(sourcePlane.DangerousGetRowSpan(row).SequenceEqual(reconstructionPlane.DangerousGetRowSpan(row)));
        }

        Assert.NotEqual(0, tileWriter.GetTileData(0).Length);
    }

    private static void FillChromaModeSelectionPlane(
        Buffer2DRegion<byte> plane,
        Av1TransformSize transformSize,
        Av1ChromaPredictionMode expectedMode,
        int expectedAngleDelta)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        Span<byte> aboveStorage = stackalloc byte[17];
        Span<byte> above = aboveStorage.Slice(1, width * 2);
        Span<byte> leftStorage = stackalloc byte[17];
        Span<byte> left = leftStorage.Slice(1, height * 2);
        aboveStorage[0] = 128;
        leftStorage[0] = 128;
        for (int column = 0; column < width; column++)
        {
            above[column] = (byte)(32 + ((192 * column) / (width - 1)));
        }

        for (int row = 0; row < height; row++)
        {
            left[row] = (byte)(224 - ((192 * row) / (height - 1)));
        }

        above[width..].Fill(above[width - 1]);
        left[height..].Fill(left[height - 1]);
        Span<byte> target = stackalloc byte[64];
        int sampleCount = transformSize.GetSize2d();
        if (expectedMode.IsDirectional())
        {
            // Directional arithmetic has separate byte-exact reference coverage. This fixture uses its scalar
            // path only to isolate chroma traversal, joint U/V rate-distortion selection, and packed mode state.
            Av1DirectionalIntraPredictor.PredictScalar(
                target[..sampleCount],
                width,
                transformSize,
                above,
                left,
                false,
                false,
                expectedMode.ToLumaMode().ToAngle() + (expectedAngleDelta * Av1Constants.AngleStep));
        }
        else
        {
            // Build the supported non-directional targets directly so production prediction cannot self-validate.
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int top = above[column];
                    int leftSample = left[row];
                    int predictor = top + leftSample - 128;
                    int leftDistance = Math.Abs(predictor - leftSample);
                    int topDistance = Math.Abs(predictor - top);
                    int cornerDistance = Math.Abs(predictor - 128);
                    target[(row * width) + column] = expectedMode switch
                    {
                        Av1ChromaPredictionMode.Vertical => (byte)top,
                        Av1ChromaPredictionMode.Horizontal => (byte)leftSample,
                        _ => (byte)(leftDistance <= topDistance && leftDistance <= cornerDistance
                            ? leftSample
                            : topDistance <= cornerDistance ? top : 128)
                    };
                }
            }
        }

        // The first three transform-sized quadrants establish the references consumed by the bottom-right
        // target. Its checkerboard offset keeps coefficients nonzero so the implicit transform affects the stream.
        for (int row = 0; row < plane.Height; row++)
        {
            Span<byte> destination = plane.DangerousGetRowSpan(row);
            for (int column = 0; column < plane.Width; column++)
            {
                destination[column] = row < height
                    ? column < width ? (byte)128 : above[column - width]
                    : column < width
                        ? left[row - height]
                        : (byte)Math.Clamp(
                            target[((row - height) * width) + column - width] +
                                ((((row - height) + column - width) & 1) == 0 ? 5 : -5),
                            0,
                            255);
            }
        }
    }

    private static void FillPlane(Buffer2DRegion<byte> plane, int modulus, int seed)
    {
        for (int y = 0; y < plane.Height; y++)
        {
            Span<byte> row = plane.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                row[x] = (byte)(1 + ((seed + (x * 43) + (y * 79)) % modulus));
            }
        }
    }

    private static void FillPlane<TSample>(Buffer2DRegion<TSample> plane, TSample value)
        where TSample : unmanaged
    {
        for (int y = 0; y < plane.Height; y++)
        {
            plane.DangerousGetRowSpan(y).Fill(value);
        }
    }

    private delegate Av1IntraTileWriter TileWriterFactory<TSample>(
        Av1EncoderFrame<TSample> source,
        Av1EncoderFrame<TSample> reconstruction,
        Av1PictureControlSet picture,
        Av1EncoderCoefficientBuffer coefficients,
        Av1EncoderSuperblockWorkspace superblockWorkspace,
        Av1EncoderBlockWorkspace blockWorkspace)
        where TSample : unmanaged;

    private delegate void FilterPrediction<TSample>(
        Av1FilterIntraMode mode,
        Span<TSample> destination,
        ReadOnlySpan<TSample> above,
        ReadOnlySpan<TSample> left,
        int bitDepth,
        Span<TSample> scratch)
        where TSample : unmanaged;

    private static void ClearPlane<TSample>(Buffer2D<TSample> plane)
        where TSample : unmanaged
    {
        for (int y = 0; y < plane.Height; y++)
        {
            plane.DangerousGetRowSpan(y).Clear();
        }
    }

    private static void AssertContainsNonzero<TSample>(Buffer2DRegion<TSample> plane)
        where TSample : unmanaged, IEquatable<TSample>
    {
        bool containsNonzero = false;
        for (int y = 0; y < plane.Height; y++)
        {
            foreach (TSample sample in plane.DangerousGetRowSpan(y))
            {
                containsNonzero |= !sample.Equals(default);
            }
        }

        Assert.True(containsNonzero);
    }

    /// <summary>
    /// Records the live luma-mode cost while supplying an all-skipped final block.
    /// </summary>
    private struct BlockCostRecorder : Av1TileWriter.IBlockEncodingHandler
    {
        private readonly int[] costs;
        private readonly int qIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockCostRecorder"/> struct.
        /// </summary>
        /// <param name="costs">The destination for costs observed in writer order.</param>
        /// <param name="qIndex">The block quantizer index.</param>
        public BlockCostRecorder(int[] costs, int qIndex)
        {
            this.costs = costs;
            this.qIndex = qIndex;
            this.Count = 0;
        }

        /// <summary>
        /// Gets the number of final blocks visited by the writer.
        /// </summary>
        public int Count { get; private set; }

        /// <inheritdoc/>
        public void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            this.costs[this.Count++] = Av1TileWriter.GetLumaModeCost(
                writer,
                macroBlock,
                Av1BlockSize.Block8x8,
                Av1PredictionMode.DC,
                0);

            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = Av1BlockSize.Block8x8,
                PartitionType = Av1PartitionType.None,
                SegmentId = 0,
                Skip = true,
                TransformSize = Av1TransformSize.Size8x8,
                Mode = Av1PredictionMode.DC,
                UvMode = Av1ChromaPredictionMode.DC,
            };

            block.HasChroma = false;
            block.QuantizationIndex = this.qIndex;
            block.SegmentId = 0;
        }
    }

    /// <summary>
    /// Supplies one skipped monochrome palette block to the production tile writer.
    /// </summary>
    private struct PaletteBlockEncoder : Av1TileWriter.IBlockEncodingHandler
    {
        private readonly Av1EncoderSuperblockWorkspace workspace;
        private readonly int qIndex;
        private readonly int mapVariant;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteBlockEncoder"/> struct.
        /// </summary>
        /// <param name="workspace">The workspace that owns the palette index map.</param>
        /// <param name="qIndex">The block quantizer index.</param>
        /// <param name="mapVariant">The map pattern selected by the test.</param>
        public PaletteBlockEncoder(
            Av1EncoderSuperblockWorkspace workspace,
            int qIndex,
            int mapVariant)
        {
            this.workspace = workspace;
            this.qIndex = qIndex;
            this.mapVariant = mapVariant;
            this.Count = 0;
        }

        /// <summary>
        /// Gets the number of final blocks visited by the writer.
        /// </summary>
        public int Count { get; private set; }

        /// <inheritdoc/>
        public void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
            this.Count++;
            modeInfo.Block = new Av1EncoderBlockModeInfo
            {
                BlockSize = Av1BlockSize.Block8x8,
                PartitionType = Av1PartitionType.None,
                SegmentId = 0,
                Skip = true,
                TransformSize = Av1TransformSize.Size8x8,
                Mode = Av1PredictionMode.DC,
                UvMode = Av1ChromaPredictionMode.DC
            };

            block.HasChroma = false;
            block.QuantizationIndex = this.qIndex;
            block.SegmentId = 0;
            paletteInfo.PaletteSizes[0] = 3;
            paletteInfo.SetColors(Av1Plane.Y, [16, 128, 240]);
            Buffer2DRegion<byte> map = this.workspace
                .GetPaletteMaps()
                .GetMap(Av1PlaneType.Y, 8, 8);

            for (int row = 0; row < map.Height; row++)
            {
                Span<byte> mapRow = map.DangerousGetRowSpan(row);
                for (int column = 0; column < map.Width; column++)
                {
                    mapRow[column] = this.mapVariant == 0
                        ? (byte)0
                        : (byte)((row + column) % 3);
                }
            }
        }
    }
}
