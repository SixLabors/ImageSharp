// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1TilingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ConstructionFailureReturnsEveryAllocation(bool use128x128Superblock, bool monochrome)
    {
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 128,
            MaxFrameHeight = 128,
            Use128x128Superblock = use128x128Superblock,
            ColorConfig = new ObuColorConfig
            {
                BitDepth = Av1BitDepth.EightBit,
                IsMonochrome = monochrome,
                SubSamplingX = true,
                SubSamplingY = true
            }
        };
        ObuFrameHeader frameHeader = new()
        {
            FrameSize = new ObuFrameSize
            {
                FrameWidth = 128,
                FrameHeight = 128,
                SuperResolutionUpscaledWidth = 128,
                RenderWidth = 128,
                RenderHeight = 128
            },
            ModeInfoColumnCount = 32,
            ModeInfoRowCount = 32,
            ModeInfoStride = 32
        };

        Configuration configuration = Configuration.Default.Clone();
        TestMemoryAllocator successfulAllocator = new();
        successfulAllocator.EnableNonThreadSafeLogging();
        configuration.MemoryAllocator = successfulAllocator;
        using (Av1TileReader reader = new(configuration, sequenceHeader, frameHeader))
        {
            Assert.NotEmpty(successfulAllocator.AllocationLog);
        }

        Assert.Equal(successfulAllocator.AllocationLog.Count, successfulAllocator.ReturnLog.Count);
        for (int failureIndex = 0; failureIndex < successfulAllocator.AllocationLog.Count; failureIndex++)
        {
            FailingTileAllocator allocator = new(failureIndex);
            configuration.MemoryAllocator = allocator;

            // Fail each actual rent, including nested frame-state and neighbor-context constructors. No reader
            // reaches the caller's using statement on failure, so construction must return every completed owner.
            InvalidMemoryOperationException exception = Assert.Throws<InvalidMemoryOperationException>(() =>
            {
                using Av1TileReader reader = new(configuration, sequenceHeader, frameHeader);
            });

            Assert.Equal("Tile allocation failure.", exception.Message);
            Assert.Equal(failureIndex, allocator.AllocationLog.Count);
            Assert.All(
                allocator.AllocationLog,
                allocation => Assert.Single(allocator.ReturnLog, returned => returned.AllocationId == allocation.AllocationId));

            Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        }
    }

    /// <summary>
    /// Verifies that frame mode-information indices do not wrap at the unsigned 16-bit boundary.
    /// </summary>
    [Fact]
    public void ModeInfoMapSupportsMoreThanUShortMaxBlocks()
    {
        const int blockCount = ushort.MaxValue + 2;
        Av1FrameInfo.Av1FrameModeInfoMap map = new(new Size(blockCount, 1));
        for (int index = 0; index < blockCount; index++)
        {
            map.Update(new Point(index, 0), Av1BlockSize.Block4x4);
        }

        Assert.Equal(blockCount, map.NextIndex);
        Assert.Equal(blockCount - 1, map[new Point(blockCount - 1, 0)]);
    }

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
        using Image<Rgba32> image = decoder.Decode<Rgba32>(content.AsSpan(codedItemOffset, codedItemLength));
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

    [Fact]
    public void DecoderReadsFirstTile()
    {
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Heif.Orange4x4);
        byte[] content = File.ReadAllBytes(filePath);
        Av1Decoder decoder = new(Configuration.Default);

        using Image<Rgba32> image = decoder.Decode<Rgba32>(content.AsSpan(0x010E, 0x001D));

        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        Assert.True(image.Frames.RootFrame.PixelBuffer.DangerousGetSingleSpan().ContainsAnyExcept(default(Rgba32)));
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

        using Av1SymbolWriter writer = new(Configuration.Default, 1, updateCdf: false);
        Av1Distribution[] partitionTypes = Av1DefaultDistributions.PartitionTypes;
        Av1BlockSize blockSize = sequenceHeader.SuperblockSize;
        while (blockSize > Av1BlockSize.Block8x8)
        {
            int blockSizeLog = blockSize.Get4x4WidthLog2() - Av1BlockSize.Block8x8.Get4x4WidthLog2();
            int context = blockSizeLog * Av1Constants.PartitionProbabilitySet;
            writer.WriteSymbol((int)Av1PartitionType.Split, partitionTypes[context]);
            blockSize = Av1PartitionType.Split.GetBlockSubSize(blockSize);
        }

        writer.WriteSymbol((int)Av1PartitionType.Horizontal, partitionTypes[0]);
        using IMemoryOwner<byte> encoded = writer.Exit();
        using Av1TileReader tileReader = new(Configuration.Default, sequenceHeader, frameHeader);

        Assert.Throws<InvalidImageContentException>(() => tileReader.ReadTile(encoded.GetSpan(), 0));
    }

    [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC, 18, 16)]
    [InlineData(TestImages.Heif.Orange4x4, 0x010E, 0x001d, 21, 1)]
    public void DecodePixelsFirstTile(string filename, int dataOffset, int dataSize, int tileOffset, int superblockCount)
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
        using Av1ReferenceFrameStore referenceFrames = new();
        using Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            obuReader.SequenceHeader,
            Av1ColorFormat.Yuv444,
            false);

        using Av1FrameInfo frameInfo = new(obuReader.SequenceHeader);
        using Av1FrameDecoder frameDecoder = new(
            obuReader.SequenceHeader,
            obuReader.FrameHeader,
            frameInfo,
            frameBuffer,
            referenceFrames);

        using Av1TileReader tileReader = new(
            Configuration.Default,
            obuReader.SequenceHeader,
            obuReader.FrameHeader,
            frameDecoder);

        // Act
        tileReader.ReadTile(tileSpan, 0);

        // Assert
        Assert.Equal(dataSize * 8, bitStreamReader.BitPosition);
        Assert.False(frameBuffer.BufferY.Size.IsEmpty);
        Assert.True(frameBuffer.BufferY.DangerousGetSingleSpan().ContainsAnyExcept<byte>(0));
    }

    [Theory]
    [InlineData((int)Av1BitDepth.TenBit, 1023)]
    [InlineData((int)Av1BitDepth.TwelveBit, 4095)]
    public void DecodePixelsFirstTileThroughHighBitDepthPipeline(int bitDepthIndex, ushort maximum)
    {
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Heif.Orange4x4);
        byte[] content = File.ReadAllBytes(filePath);
        const int dataOffset = 0x010E;
        const int dataSize = 0x001D;
        const int tileOffset = 21;
        Span<byte> headerSpan = content.AsSpan(dataOffset, dataSize);
        Span<byte> tileSpan = content.AsSpan(dataOffset + tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        IAv1TileReader stub = new Av1TileDecoderStub();
        ObuReader obuReader = new();
        obuReader.ReadAll(ref bitStreamReader, dataSize, () => stub);

        // Reuse known-good tile syntax after parsing so this test isolates native high-bit prediction and reconstruction wiring.
        obuReader.SequenceHeader.ColorConfig.BitDepth = (Av1BitDepth)bitDepthIndex;
        using Av1ReferenceFrameStore referenceFrames = new();
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, obuReader.SequenceHeader, Av1ColorFormat.Yuv444, false);
        using Av1FrameInfo frameInfo = new(obuReader.SequenceHeader);
        using Av1FrameDecoder frameDecoder = new(
            obuReader.SequenceHeader,
            obuReader.FrameHeader,
            frameInfo,
            frameBuffer,
            referenceFrames);

        using Av1TileReader tileReader = new(
            Configuration.Default,
            obuReader.SequenceHeader,
            obuReader.FrameHeader,
            frameDecoder);

        tileReader.ReadTile(tileSpan, 0);

        Span<ushort> yRow = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, 0, 0, 0);
        Assert.True(yRow[..4].ContainsAnyExcept<ushort>(0));
        Assert.All(yRow[..4].ToArray(), value => Assert.InRange(value, (ushort)0, maximum));
    }

    [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC, 18, 16)]
    [InlineData(TestImages.Heif.Orange4x4, 0x010E, 0x001d, 21, 1)]
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
    }

    [Fact]
    public void ParsedSuperblocksExposeEveryModeInfoInBitstreamOrder()
    {
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Heif.XnConvert);
        byte[] content = File.ReadAllBytes(filePath);
        const int dataOffset = 0x010E;
        const int dataSize = 0x03CC;
        const int tileOffset = 18;
        Span<byte> headerSpan = content.AsSpan(dataOffset, dataSize);
        Span<byte> tileSpan = content.AsSpan(dataOffset + tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        IAv1TileReader stub = new Av1TileDecoderStub();
        ObuReader obuReader = new();
        obuReader.ReadAll(ref bitStreamReader, dataSize, () => stub);
        using Av1TileReader tileReader = new(Configuration.Default, obuReader.SequenceHeader, obuReader.FrameHeader);

        tileReader.ReadTile(tileSpan, 0);

        int parsedModeInfoCount = 0;
        int superblockSize = obuReader.SequenceHeader.SuperblockModeInfoSize;
        for (int row = 0; row < obuReader.FrameHeader.ModeInfoRowCount; row += superblockSize)
        {
            for (int column = 0; column < obuReader.FrameHeader.ModeInfoColumnCount; column += superblockSize)
            {
                Point superblockPosition = new(column / superblockSize, row / superblockSize);
                Av1SuperblockInfo superblockInfo = tileReader.FrameInfo.GetSuperblock(superblockPosition);
                Av1FrameInfo.ModeInfoCollection modeInfos = superblockInfo.GetModeInfos();

                Assert.Equal(superblockInfo.BlockCount, modeInfos.Length);
                Assert.Equal(modeInfos[0].ModeInfoIndex, tileReader.FrameInfo.GetModeInfo(superblockPosition).ModeInfoIndex);

                foreach (Av1BlockModeInfo modeInfo in modeInfos)
                {
                    Point modeInfoPosition = new(
                        superblockInfo.ModeInfoPosition.X + modeInfo.PositionInSuperblock.X,
                        superblockInfo.ModeInfoPosition.Y + modeInfo.PositionInSuperblock.Y);

                    for (int y = 0; y < modeInfo.BlockSize.Get4x4HighCount(); y++)
                    {
                        for (int x = 0; x < modeInfo.BlockSize.Get4x4WideCount(); x++)
                        {
                            Assert.Equal(
                                modeInfo.ModeInfoIndex,
                                tileReader.FrameInfo.GetModeInfoAt(new Point(modeInfoPosition.X + x, modeInfoPosition.Y + y)).ModeInfoIndex);
                        }
                    }
                }

                parsedModeInfoCount += modeInfos.Length;
            }
        }

        Assert.True(parsedModeInfoCount > 16);
    }

    [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC, 18, 16)]
    [InlineData(TestImages.Heif.Orange4x4, 0x010E, 0x001d, 21, 1)]
    public void ParseHeaderForFirstTile(string filename, int dataOffset, int dataSize, int tileOffset, int superblockCount)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> headerSpan = content.AsSpan(dataOffset, dataSize);
        Span<byte> tileSpan = content.AsSpan(dataOffset + tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        ObuReader obuReader = new();
        Av1FrameDecoderStub frameDecoder = new();

        // Act
        obuReader.ReadAll(ref bitStreamReader, dataSize, () => new Av1TileReader(Configuration.Default, obuReader.SequenceHeader, obuReader.FrameHeader, frameDecoder));

        // Assert
        Assert.Equal(dataSize * 8, bitStreamReader.BitPosition);
        Assert.Equal(superblockCount, frameDecoder.SuperblockCount);
    }

    private sealed class FailingTileAllocator : TestMemoryAllocator
    {
        private readonly int failureIndex;

        public FailingTileAllocator(int failureIndex)
        {
            this.failureIndex = failureIndex;
            this.EnableNonThreadSafeLogging();
        }

        protected override AllocationTrackedMemoryManager<T> AllocateCore<T>(int length, AllocationOptions options)
        {
            if (this.AllocationLog.Count == this.failureIndex)
            {
                throw new InvalidMemoryOperationException("Tile allocation failure.");
            }

            return base.AllocateCore<T>(length, options);
        }
    }
}
