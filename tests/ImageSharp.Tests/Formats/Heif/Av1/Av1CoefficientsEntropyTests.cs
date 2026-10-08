// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1CoefficientsEntropyTests
{
    private const int BaseQIndex = 23;

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
            Height,
            1 << sequenceHeader.SuperblockSizeLog2,
            disallow4x4AllFrames: false);

        Av1PictureControlSet picture = pictureBuffer.Picture;
        Av1NeighborEdges<Av1EncoderPaletteInfo> paletteContexts = Assert.Single(picture.PaletteContexts).GetEdges();
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

        using Av1SymbolEncoder encoder = new(Configuration.Default, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        Av1TileWriter.WritePaletteModeInfo<Av1SymbolEncoder.SymbolWriteOperation>(
            ref output,
            picture.Sequence,
            encoder,
            macroBlock,
            modeInfo,
            ref current,
            Av1BlockSize.Block8x8,
            blockOrigin,
            in paletteContexts,
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

        Av1NeighborEdges<byte> edges = transforms.GetEdges();
        int topIndex = edges.GetTopIndex(blockOrigin);
        int leftIndex = edges.GetLeftIndex(blockOrigin);
        edges.Top[topIndex] = 16;
        edges.Left[leftIndex] = 16;
        picture.TransformFunctionContexts = [transforms];

        using Av1SymbolEncoder writer = new(Configuration.Default, BaseQIndex, updateCdf: true);
        Span<byte> output = writer.GetTileBuffer();
        Av1TileWriter.WriteTransformSize<Av1SymbolEncoder.SymbolWriteOperation>(
            ref output,
            picture,
            writer,
            ref modeInfo,
            macroBlock,
            modeInfo.Block.BlockSize,
            blockOrigin,
            in edges);

        using IMemoryOwner<byte> encoded = writer.Exit();
        writer.Dispose();

        Av1SymbolDecoder reader = new(Configuration.Default, encoded.GetSpan(), BaseQIndex);
        Assert.Equal(
            Av1TransformSize.Size8x8,
            reader.ReadTransformSize(Av1BlockSize.Block16x32, context: 1));

        for (int index = 0; index < edges.Top.Length; index++)
        {
            byte expected = index >= topIndex && index < topIndex + 4 ? (byte)8 : (byte)0;
            Assert.Equal(expected, edges.Top[index]);
        }

        for (int index = 0; index < edges.Left.Length; index++)
        {
            byte expected = index >= leftIndex && index < leftIndex + 8 ? (byte)8 : (byte)0;
            Assert.Equal(expected, edges.Left[index]);
        }
    }

    [Fact]
    public void RoundTripFullCoefficientsSize4x4()
    {
        foreach (ITheoryDataRow row in GetTransformTypes())
        {
            object[] values = row.GetData();
            RoundTripFullCoefficientsYSize4x4Case((int)values[0]);
            RoundTripFullCoefficientsUvSize4x4Case((int)values[0]);
        }
    }

    private static void RoundTripFullCoefficientsYSize4x4Case(int txType)
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

    private static void RoundTripFullCoefficientsUvSize4x4Case(int txType)
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

    private static Av1InverseQuantizer CreateInverseQuantizer()
    {
        ObuSequenceHeader sequenceHeader = new()
        {
            ColorConfig = new ObuColorConfig { BitDepth = Av1BitDepth.EightBit }
        };

        ObuFrameHeader frameHeader = new();
        frameHeader.QuantizationParameters.BaseQIndex = BaseQIndex;
        return new Av1InverseQuantizer(sequenceHeader, frameHeader);
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
        byte[] aboveContexts = new byte[transformSize.Get4x4WideCount()];
        byte[] leftContexts = new byte[transformSize.Get4x4HighCount()];
        Av1TransformBlockContext transformBlockContext = default;
        Configuration configuration = Configuration.Default;
        using Av1SymbolEncoder encoder = new(configuration, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
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

        Span<int> actuals = new int[coefficientCount];

        // Act
        encoder.WriteCoefficients<Av1SymbolEncoder.SymbolWriteOperation>(
            ref output,
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
            actuals,
            CreateInverseQuantizer());

        decoder.ValidateTrailingBits();

        // Assert
        Assert.Equal(endOfBlock, transformInfo.EndOfBlock);

        // At 8-bit quantizer index 23, the dequantization steps are 26 for DC and 30 for AC. Check every raster
        // position, including zero runs and the tail beyond EOB, independently of the entropy scan traversal.
        int maximumCoefficientIndex = 0;
        for (int coefficientIndex = 0; coefficientIndex < coefficientCount; coefficientIndex++)
        {
            int dequant = coefficientIndex == 0 ? 26 : 30;
            Assert.Equal(coefficientsBuffer[coefficientIndex] * dequant, actuals[coefficientIndex]);
            if (coefficientsBuffer[coefficientIndex] != 0)
            {
                maximumCoefficientIndex = coefficientIndex;
            }
        }

        Assert.Equal(maximumCoefficientIndex, transformInfo.MaximumCoefficientIndex);

        // Derive the stored edge byte from the original quantized values, independently of decoded magnitudes.
        int levelSum = 0;
        foreach (int coefficient in coefficientsBuffer)
        {
            levelSum += Math.Abs(coefficient);
        }

        int dcClass = coefficientsBuffer[0] < 0 ? 1 : coefficientsBuffer[0] > 0 ? 2 : 0;
        byte expectedContext = (byte)((dcClass * 8) + Math.Min(7, levelSum));
        Assert.All(aboveContexts, value => Assert.Equal(expectedContext, value));
        Assert.All(leftContexts, value => Assert.Equal(expectedContext, value));
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
                PreviousQIndex = Memory<int>.Empty
            },
            SegmentationNeighborMap = Memory<byte>.Empty,
            ModeInfoGrid = modeInfoGrid,
            ModeInfoAllocation = modeInfoAllocation,
            ModeInfoStride = modeInfoColumnCount,
            CdefPreset = new int[] { -1, -1, -1, -1 },
            TileDataLengths = Memory<int>.Empty
        };
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
