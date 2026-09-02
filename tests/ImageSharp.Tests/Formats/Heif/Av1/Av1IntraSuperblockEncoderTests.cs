// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies fixed DC intra superblock traversal and reconstruction.
/// </summary>
[Trait("Format", "Avif")]
public class Av1IntraSuperblockEncoderTests
{
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

        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.Y), 251, 17);
        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.U), 239, 31);
        FillPlane(source.Frame.CodedView.GetPlane(Av1Plane.V), 233, 47);
        ClearPlane(reconstruction.Luma);
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaBlue));
        ClearPlane(Assert.IsType<Buffer2D<byte>>(reconstruction.ChromaRed));

        using Av1EncoderModeInfoBuffer modeInfo = new(Configuration.Default, Width, Height, disallow4x4AllFrames: true);
        Av1PictureControlSet picture = CreatePicture(modeInfo, colorConfig, use128x128Superblock: true, qIndex: 73);
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
            Assert.False(block.Block.Skip);
        }

        Span<Av1EncoderTransformBlockState> lumaStates = coefficients.GetTransformBlockSpan(0, Av1Plane.Y);
        Span<Av1EncoderTransformBlockState> blueStates = coefficients.GetTransformBlockSpan(0, Av1Plane.U);
        Span<Av1EncoderTransformBlockState> redStates = coefficients.GetTransformBlockSpan(0, Av1Plane.V);
        int[] lumaStateIndices = [0, 4, 8, 12];
        for (int index = 0; index < 4; index++)
        {
            Assert.NotEqual((ushort)0, lumaStates[lumaStateIndices[index]].EndOfBlock);
            Assert.Equal(Av1TransformType.DctDct, lumaStates[lumaStateIndices[index]].TransformType);
            Assert.NotEqual((ushort)0, blueStates[index].EndOfBlock);
            Assert.NotEqual((ushort)0, redStates[index].EndOfBlock);
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
}
