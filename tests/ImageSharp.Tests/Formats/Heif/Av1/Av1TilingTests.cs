// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1TilingTests
{
    /// <summary>
    /// Verifies the decoded block geometry and prediction modes against libaom inspection output for a real AVIF image item.
    /// </summary>
    [Fact]
    public void ParsedRealAvifModeMapMatchesLibaom()
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

        // These counts come from the 102 by 76 mode-info maps emitted by libaom 3.14.1's inspect tool.
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

    [Theory]
    [InlineData(TestImages.Heif.XnConvert, 0x010E, 0x03CC, 18, 16)]
    [InlineData(TestImages.Heif.Orange4x4, 0x010E, 0x001d, 21, 1)]
    public void DecodePixelsFirstTile(string filename, int dataOffset, int dataSize, int tileOffset, int superblockCount)
    {
        // Assign
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, filename);
        byte[] content = File.ReadAllBytes(filePath);
        Span<byte> headerSpan = content.AsSpan(dataOffset, dataSize);
        Span<byte> tileSpan = content.AsSpan(tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        IAv1TileReader stub = new Av1TileDecoderStub();
        ObuReader obuReader = new();
        obuReader.ReadAll(ref bitStreamReader, dataSize, () => stub);
        Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, obuReader.SequenceHeader, Av1ColorFormat.Yuv444, false);
        Av1FrameInfo frameInfo = new(obuReader.SequenceHeader);
        Av1FrameDecoder frameDecoder = new(obuReader.SequenceHeader, obuReader.FrameHeader, frameInfo, frameBuffer);
        Av1TileReader tileReader = new(Configuration.Default, obuReader.SequenceHeader, obuReader.FrameHeader, frameDecoder);

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
        Span<byte> tileSpan = content.AsSpan(tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        IAv1TileReader stub = new Av1TileDecoderStub();
        ObuReader obuReader = new();
        obuReader.ReadAll(ref bitStreamReader, dataSize, () => stub);

        // Reuse known-good tile syntax after parsing so this test isolates native high-bit prediction and reconstruction wiring.
        obuReader.SequenceHeader.ColorConfig.BitDepth = (Av1BitDepth)bitDepthIndex;
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, obuReader.SequenceHeader, Av1ColorFormat.Yuv444, false);
        Av1FrameInfo frameInfo = new(obuReader.SequenceHeader);
        Av1FrameDecoder frameDecoder = new(obuReader.SequenceHeader, obuReader.FrameHeader, frameInfo, frameBuffer);
        Av1TileReader tileReader = new(Configuration.Default, obuReader.SequenceHeader, obuReader.FrameHeader, frameDecoder);

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
        Span<byte> tileSpan = content.AsSpan(tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        IAv1TileReader stub = new Av1TileDecoderStub();
        ObuReader obuReader = new();
        obuReader.ReadAll(ref bitStreamReader, dataSize, () => stub);
        Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, obuReader.SequenceHeader, Av1ColorFormat.Yuv444, false);
        Av1FrameInfo frameInfo = new(obuReader.SequenceHeader);
        Av1FrameDecoderStub frameDecoder = new();
        Av1TileReader tileReader = new(Configuration.Default, obuReader.SequenceHeader, obuReader.FrameHeader, frameDecoder);

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
        Span<byte> tileSpan = content.AsSpan(tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        IAv1TileReader stub = new Av1TileDecoderStub();
        ObuReader obuReader = new();
        obuReader.ReadAll(ref bitStreamReader, dataSize, () => stub);
        Av1TileReader tileReader = new(Configuration.Default, obuReader.SequenceHeader, obuReader.FrameHeader);

        tileReader.ReadTile(tileSpan, 0);

        int parsedModeInfoCount = 0;
        int superblockSize = obuReader.SequenceHeader.SuperblockModeInfoSize;
        for (int row = 0; row < obuReader.FrameHeader.ModeInfoRowCount; row += superblockSize)
        {
            for (int column = 0; column < obuReader.FrameHeader.ModeInfoColumnCount; column += superblockSize)
            {
                Point superblockPosition = new(column / superblockSize, row / superblockSize);
                Av1SuperblockInfo superblockInfo = tileReader.FrameInfo.GetSuperblock(superblockPosition);
                Span<Av1BlockModeInfo> modeInfos = superblockInfo.GetModeInfos();

                Assert.Equal(superblockInfo.BlockCount, modeInfos.Length);
                Assert.DoesNotContain(modeInfos.ToArray(), modeInfo => modeInfo is null);
                Assert.Same(modeInfos[0], tileReader.FrameInfo.GetModeInfo(superblockPosition));

                foreach (Av1BlockModeInfo modeInfo in modeInfos)
                {
                    Point modeInfoPosition = new(
                        superblockInfo.ModeInfoPosition.X + modeInfo.PositionInSuperblock.X,
                        superblockInfo.ModeInfoPosition.Y + modeInfo.PositionInSuperblock.Y);

                    for (int y = 0; y < modeInfo.BlockSize.Get4x4HighCount(); y++)
                    {
                        for (int x = 0; x < modeInfo.BlockSize.Get4x4WideCount(); x++)
                        {
                            Assert.Same(modeInfo, tileReader.FrameInfo.GetModeInfoAt(new Point(modeInfoPosition.X + x, modeInfoPosition.Y + y)));
                        }
                    }
                }

                parsedModeInfoCount += modeInfos.Length;
            }
        }

        Assert.True(parsedModeInfoCount > 16);
    }

    [Fact]
    public void ParsedCoefficientsRemainAvailablePerSuperblockAndPlane()
    {
        string filePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Heif.XnConvert);
        byte[] content = File.ReadAllBytes(filePath);
        const int dataOffset = 0x010E;
        const int dataSize = 0x03CC;
        const int tileOffset = 18;
        Span<byte> headerSpan = content.AsSpan(dataOffset, dataSize);
        Span<byte> tileSpan = content.AsSpan(tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        IAv1TileReader stub = new Av1TileDecoderStub();
        ObuReader obuReader = new();
        obuReader.ReadAll(ref bitStreamReader, dataSize, () => stub);
        Av1TileReader tileReader = new(Configuration.Default, obuReader.SequenceHeader, obuReader.FrameHeader);

        tileReader.ReadTile(tileSpan, 0);

        int codedTransformCount = 0;
        int superblockSize = obuReader.SequenceHeader.SuperblockModeInfoSize;
        for (int row = 0; row < obuReader.FrameHeader.ModeInfoRowCount; row += superblockSize)
        {
            for (int column = 0; column < obuReader.FrameHeader.ModeInfoColumnCount; column += superblockSize)
            {
                Point superblockPosition = new(column / superblockSize, row / superblockSize);
                Av1SuperblockInfo superblockInfo = tileReader.FrameInfo.GetSuperblock(superblockPosition);
                int[] coefficientIndices = new int[Av1Constants.MaxPlanes];

                foreach (Av1BlockModeInfo modeInfo in superblockInfo.GetModeInfos())
                {
                    Point modeInfoPosition = new(column + modeInfo.PositionInSuperblock.X, row + modeInfo.PositionInSuperblock.Y);
                    bool hasChroma = Av1TileReader.HasChroma(obuReader.SequenceHeader, modeInfoPosition, modeInfo.BlockSize);

                    for (int plane = 0; plane < obuReader.SequenceHeader.ColorConfig.PlaneCount; plane++)
                    {
                        if (plane != 0 && !hasChroma)
                        {
                            continue;
                        }

                        int transformUnitCount = modeInfo.TransformUnitsCount[Math.Min(plane, 1)];
                        int transformInfoIndex = modeInfo.FirstTransformLocation[Math.Min(plane, 1)];
                        if (plane == (int)Av1Plane.V)
                        {
                            transformInfoIndex += transformUnitCount;
                        }

                        Span<Av1TransformInfo> transformInfos = superblockInfo.GetTransformInfo(plane)[transformInfoIndex..];
                        Span<int> coefficients = superblockInfo.GetCoefficients((Av1Plane)plane);
                        for (int i = 0; i < transformUnitCount; i++)
                        {
                            if (!transformInfos[i].CodeBlockFlag)
                            {
                                continue;
                            }

                            int endOfBlock = coefficients[coefficientIndices[plane]];
                            Assert.InRange(endOfBlock, 1, transformInfos[i].Size.GetWidth() * transformInfos[i].Size.GetHeight());
                            coefficientIndices[plane] += endOfBlock + 1;
                            codedTransformCount++;
                        }
                    }
                }
            }
        }

        Assert.True(codedTransformCount > 3);
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
        Span<byte> tileSpan = content.AsSpan(tileOffset, dataSize - tileOffset);
        Av1BitStreamReader bitStreamReader = new(headerSpan);
        ObuReader obuReader = new();
        Av1FrameDecoderStub frameDecoder = new();

        // Act
        obuReader.ReadAll(ref bitStreamReader, dataSize, () => new Av1TileReader(Configuration.Default, obuReader.SequenceHeader, obuReader.FrameHeader, frameDecoder));

        // Assert
        Assert.Equal(dataSize * 8, bitStreamReader.BitPosition);
        Assert.Equal(superblockCount, frameDecoder.SuperblockCount);
    }
}
