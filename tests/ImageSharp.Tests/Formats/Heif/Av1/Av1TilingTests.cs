// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1TilingTests
{
    /// <summary>
    /// Verifies the decoded block geometry and prediction modes against the reference decoder inspection output for a real AVIF image item.
    /// </summary>
    [Fact]
    public void ParsedAvifModeMapMatchesReference()
    {
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Heif.ParisIccExifXmpAvif);
        byte[] content = File.ReadAllBytes(filePath);

        // The fixture's iloc box identifies item 1 as the AV1 payload at offset 0x17A8 with length 0x3AE4.
        const int codedItemOffset = 0x17A8;
        const int codedItemLength = 0x3AE4;
        using Av1Decoder decoder = new(Configuration.Default);
        using Av1FrameBuffer<byte> imagePlanes = decoder.DecodeFrameBuffer(content.AsSpan(codedItemOffset, codedItemLength), null, null, out _);
        using Image<Rgba32> image = new(Configuration.Default, imagePlanes.Width, imagePlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            imagePlanes,
            image.Bounds,
            image.Frames.RootFrame.PixelBuffer.GetRegion(image.Bounds),
            image.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            imagePlanes.ColorConfig.ColorRange);

        Av1FrameInfo frameInfo = Assert.IsType<Av1FrameInfo>(decoder.FrameInfo);
        ObuFrameHeader frameHeader = Assert.IsType<ObuFrameHeader>(decoder.FrameHeader);
        Span<int> blockSizeCounts = stackalloc int[(int)Av1BlockSize.AllSizes];
        Span<int> modeCounts = stackalloc int[(int)Av1PredictionMode.IntraModes];

        for (int row = 0; row < frameHeader.ModeInfoRowCount; row++)
        {
            for (int column = 0; column < frameHeader.ModeInfoColumnCount; column++)
            {
                Av1BlockModeInfo modeInfo = frameInfo.GetModeInfoAt(new Point(column, row));
                blockSizeCounts[(int)modeInfo.BlockSize]++;
                modeCounts[(int)modeInfo.YMode]++;
            }
        }

        // These counts come from independently inspected 102 by 76 mode-info maps.
        int[] expectedBlockSizeCounts = new int[(int)Av1BlockSize.AllSizes];
        expectedBlockSizeCounts[(int)Av1BlockSize.Block8x8] = 3176;
        expectedBlockSizeCounts[(int)Av1BlockSize.Block8x16] = 48;
        expectedBlockSizeCounts[(int)Av1BlockSize.Block16x16] = 4080;
        expectedBlockSizeCounts[(int)Av1BlockSize.Block32x32] = 448;

        int[] expectedModeCounts = new int[(int)Av1PredictionMode.IntraModes];
        expectedModeCounts[(int)Av1PredictionMode.DC] = 3020;
        expectedModeCounts[(int)Av1PredictionMode.Vertical] = 228;
        expectedModeCounts[(int)Av1PredictionMode.Horizontal] = 2360;
        expectedModeCounts[(int)Av1PredictionMode.Smooth] = 2144;

        Assert.Equal(expectedBlockSizeCounts, blockSizeCounts.ToArray());
        Assert.Equal(expectedModeCounts, modeCounts.ToArray());
    }

    /// <summary>
    /// Verifies that partition syntax cannot produce a luma block with no valid 4:2:0 chroma representation.
    /// </summary>
    [Fact]
    public void RejectsPartitionThatCannotRepresentSubsampledChroma()
    {
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                BitDepth = Av1BitDepth.EightBit,
                SubSamplingX = true,
                SubSamplingY = true
            }
        };
        ObuTileGroupHeader tileInfo = new()
        {
            TileColumnCount = 1,
            TileRowCount = 1
        };

        tileInfo.TileColumnStartModeInfo[1] = sequenceHeader.SuperblockModeInfoSize;
        tileInfo.TileRowStartModeInfo[1] = sequenceHeader.SuperblockModeInfoSize;
        ObuFrameHeader frameHeader = new()
        {
            FrameSize = new ObuFrameSize
            {
                FrameWidth = 64,
                FrameHeight = 64,
                SuperResolutionUpscaledWidth = 64,
                RenderWidth = 64,
                RenderHeight = 64
            },
            ModeInfoColumnCount = sequenceHeader.SuperblockModeInfoSize,
            ModeInfoRowCount = sequenceHeader.SuperblockModeInfoSize,
            ModeInfoStride = sequenceHeader.SuperblockModeInfoSize,
            TilesInfo = tileInfo,
            DisableCdfUpdate = true,
            DisableFrameEndUpdateCdf = true
        };

        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf: false);
        Span<byte> output = writer.GetTileBuffer();
        Av1Distribution[] partitionTypes = Av1DefaultDistributions.PartitionTypes;
        Av1BlockSize blockSize = sequenceHeader.SuperblockSize;
        while (blockSize > Av1BlockSize.Block8x8)
        {
            int blockSizeLog = blockSize.Get4x4WidthLog2() - Av1BlockSize.Block8x8.Get4x4WidthLog2();
            int context = blockSizeLog * Av1Constants.PartitionProbabilitySet;
            writer.WriteSymbol(ref output, (int)Av1PartitionType.Split, partitionTypes[context]);
            blockSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
        }

        writer.WriteSymbol(ref output, (int)Av1PartitionType.Horizontal, partitionTypes[0]);
        using IMemoryOwner<byte> encoded = writer.Exit();
        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);

        Assert.Throws<InvalidImageContentException>(() => tileReader.ReadTile(encoded.GetSpan(), 0));
    }

    [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC, 18, 16)]
    public void DecodePartitionsFirstTile(string filename, int dataOffset, int dataSize, int tileOffset, int superblockCount)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> headerSpan = content.AsSpan(dataOffset, dataSize);
        Span<byte> tileSpan = content.AsSpan(dataOffset + tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        IAv1TileReader stub = new Av1TileDecoderStub();
        ObuReader obuReader = new();
        obuReader.ReadAll(ref bitStreamReader, dataSize, () => stub);
        Av1FrameDecoderStub frameDecoder = new();
        using Av1TileReader tileReader = new(
            Configuration.Default,
            obuReader.SequenceHeader,
            obuReader.FrameHeader,
            frameDecoder);

        // Act
        tileReader.ReadTile(tileSpan, 0);

        // Assert
        Assert.Equal(dataSize * 8, bitStreamReader.BitPosition);
        Assert.Equal(superblockCount, frameDecoder.SuperblockCount);
        int parsedBlockCount = 0;
        int superblockSize = obuReader.SequenceHeader.SuperblockModeInfoSize;
        ObuTileGroupHeader tiles = obuReader.FrameHeader.TilesInfo;
        for (int row = tiles.TileRowStartModeInfo[0]; row < tiles.TileRowStartModeInfo[1]; row += superblockSize)
        {
            for (int column = tiles.TileColumnStartModeInfo[0]; column < tiles.TileColumnStartModeInfo[1]; column += superblockSize)
            {
                parsedBlockCount += tileReader.FrameInfo.GetModeInfoCount(new Point(column / superblockSize, row / superblockSize));
            }
        }

        Assert.True(parsedBlockCount >= superblockCount);
        Assert.Equal(parsedBlockCount, frameDecoder.BlockCount);
        Assert.True(frameDecoder.TransformCount >= parsedBlockCount);
        Assert.Equal(0, frameDecoder.RemainingTransforms);
    }
}
