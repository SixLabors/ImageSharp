// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

public class Av1EncoderFrameTests
{
    [Theory]
    [InlineData(8, 8, false, 0)]
    [InlineData(8, 8, true, 0)]
    [InlineData(16, 16, false, 0)]
    [InlineData(16, 16, true, 0)]
    [InlineData(8, 8, false, 1)]
    [InlineData(8, 8, true, 1)]
    [InlineData(8, 8, false, 2)]
    [InlineData(8, 8, true, 2)]
    public void EncodeWritesReducedStillPictureConsumedByProductionDecoder(int width, int height, bool hasGradient, int bitDepthValue)
    {
        Av1BitDepth bitDepth = (Av1BitDepth)bitDepthValue;
        using Image<Rgba32> source = new(width, height);
        for (int y = 0; y < height; y++)
        {
            Span<Rgba32> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                byte value = hasGradient ? (byte)((x * 13) + (y * 17)) : (byte)128;
                row[x] = new Rgba32(value, value, value);
            }
        }

        ObuColorConfig colorConfig = CreateMonochromeColorConfig(bitDepth);
        using MemoryStream stream = new();
        ObuSequenceHeader encodedHeader = Av1FrameEncoder.Encode(
            Configuration.Default,
            source.Frames.RootFrame,
            stream,
            colorConfig,
            qIndex: 37);

        byte[] payload = stream.ToArray();
        string outputDirectory = Path.Combine(
            TestEnvironment.ActualOutputDirectoryFullPath,
            "Formats",
            "Heif",
            "Av1");

        Directory.CreateDirectory(outputDirectory);
        string contentName = hasGradient ? "gradient" : "constant";
        int bitCount = bitDepth.GetBitCount();
        string fileName = $"encoder-frame-{width}x{height}-{bitCount}b-400-{contentName}.obu";
        File.WriteAllBytes(Path.Combine(outputDirectory, fileName), payload);

        using Av1Decoder decoder = new(Configuration.Default);
        using Image<Rgba32> decoded = decoder.Decode<Rgba32>(payload);

        Assert.Equal(width, decoded.Width);
        Assert.Equal(height, decoded.Height);
        ObuSequenceProfile expectedProfile = bitDepth == Av1BitDepth.TwelveBit
            ? ObuSequenceProfile.Professional
            : ObuSequenceProfile.Main;

        Assert.Equal(expectedProfile, encodedHeader.SequenceProfile);
        Assert.True(encodedHeader.IsReducedStillPictureHeader);

        Rgba32 first = decoded[0, 0];
        Assert.Equal(first.R, first.G);
        Assert.Equal(first.R, first.B);
        Assert.Equal(byte.MaxValue, first.A);
        if (hasGradient)
        {
            Assert.True(first.R < decoded[width - 1, height - 1].R);
        }
        else
        {
            Assert.InRange(first.R, 120, 136);

            for (int y = 0; y < height; y++)
            {
                foreach (Rgba32 pixel in decoded.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y))
                {
                    Assert.Equal(first, pixel);
                }
            }
        }
    }

    [Fact]
    public void PrepareSourceConvertsRgba32DirectlyIntoBorderedEightBitPlane()
    {
        const int width = 4;
        const int height = 1;
        const int border = Av1EncoderFrame<byte>.LumaBorder;

        using Image<Rgba32> image = new(width, height);
        image[0, 0] = new Rgba32(byte.MaxValue, 0, 0, 0);
        image[1, 0] = new Rgba32(0, byte.MaxValue, 0);
        image[2, 0] = new Rgba32(0, 0, byte.MaxValue);
        image[3, 0] = new Rgba32(byte.MaxValue, byte.MaxValue, byte.MaxValue);

        using Av1EncoderFrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            width,
            height,
            8,
            Av1ColorFormat.Yuv400,
            0,
            0);

        ObuColorConfig colorConfig = CreateMonochromeColorConfig(Av1BitDepth.EightBit);

        Av1FrameEncoder.PrepareSource(Configuration.Default, image.Frames.RootFrame, frameBuffer.Frame, colorConfig);

        byte[] expected = [76, 150, 29, 255];
        AssertReplicatedSingleRow(frameBuffer.Luma, border, expected);
    }

    [Fact]
    public void PrepareSourcePreservesHighBitDepthPrecision()
    {
        const int width = 4;
        const int height = 1;
        const int border = Av1EncoderFrame<ushort>.LumaBorder;

        using Image<Rgba64> image = new(width, height);
        image[0, 0] = new Rgba64(ushort.MaxValue, 0, 0, 0);
        image[1, 0] = new Rgba64(0, ushort.MaxValue, 0, ushort.MaxValue);
        image[2, 0] = new Rgba64(0, 0, ushort.MaxValue, ushort.MaxValue);
        image[3, 0] = new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);

        using Av1EncoderFrameBuffer<ushort> frameBuffer = new(
            Configuration.Default,
            width,
            height,
            10,
            Av1ColorFormat.Yuv400,
            0,
            0);

        ObuColorConfig colorConfig = CreateMonochromeColorConfig(Av1BitDepth.TenBit);

        Av1FrameEncoder.PrepareSource(Configuration.Default, image.Frames.RootFrame, frameBuffer.Frame, colorConfig);

        ushort[] expected = [306, 601, 117, 1023];
        AssertReplicatedSingleRow(frameBuffer.Luma, border, expected);
    }

    [Fact]
    public void ExtendBordersReplicatesEveryPhysicalPlaneEdge()
    {
        const int visibleWidth = 5;
        const int visibleHeight = 3;
        const int lumaBorder = Av1EncoderFrame<byte>.LumaBorder;
        const int chromaBorder = lumaBorder / 2;

        using Av1EncoderFrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            visibleWidth,
            visibleHeight,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1);

        Buffer2D<byte> luma = frameBuffer.Luma;
        Buffer2D<byte> chromaBlue = Assert.IsType<Buffer2D<byte>>(frameBuffer.ChromaBlue);
        Buffer2D<byte> chromaRed = Assert.IsType<Buffer2D<byte>>(frameBuffer.ChromaRed);

        FillVisible(luma, lumaBorder, lumaBorder, visibleWidth, visibleHeight, 10);
        FillVisible(chromaBlue, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 80);
        FillVisible(chromaRed, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 120);

        frameBuffer.Frame.ExtendBorders();

        AssertReplicatedPlane(luma, lumaBorder, lumaBorder, visibleWidth, visibleHeight, 10);
        AssertReplicatedPlane(chromaBlue, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 80);
        AssertReplicatedPlane(chromaRed, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 120);
    }

    [Theory]
    [InlineData(5, 3, 0, 0, 160, 136)]
    [InlineData(5, 3, 1, 0, 80, 136)]
    [InlineData(5, 3, 1, 1, 80, 68)]
    [InlineData(1921, 1081, 0, 0, 2080, 1216)]
    [InlineData(1921, 1081, 1, 1, 1040, 608)]
    public void GetPlaneBufferSizeMatchesLibaomLayout(
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        int expectedWidth,
        int expectedHeight)
    {
        Size actual = Av1EncoderFrame<byte>.GetPlaneBufferSize(width, height, subsamplingX, subsamplingY);

        Assert.Equal(new Size(expectedWidth, expectedHeight), actual);
    }

    [Fact]
    public void FrameBufferUsesOneExactSizeOwnerForAllPlanes()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;

        TestMemoryAllocator.AllocationRequest allocation;
        using (Av1EncoderFrameBuffer<byte> frameBuffer = new(
            configuration,
            64,
            64,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1))
        {
            allocation = Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(typeof(byte), allocation.ElementType);
            Assert.Equal(55_296, allocation.Length);
            Assert.Single(frameBuffer.Luma.MemoryGroup);
            Assert.Single(Assert.IsType<Buffer2D<byte>>(frameBuffer.ChromaBlue).MemoryGroup);
            Assert.Single(Assert.IsType<Buffer2D<byte>>(frameBuffer.ChromaRed).MemoryGroup);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.AllocationId, returned.AllocationId);
    }

    private static ObuColorConfig CreateMonochromeColorConfig(Av1BitDepth bitDepth)
        => new()
        {
            IsColorDescriptionPresent = true,
            IsMonochrome = true,
            ColorPrimaries = ObuColorPrimaries.Bt601,
            TransferCharacteristics = ObuTransferCharacteristics.Bt601,
            MatrixCoefficients = ObuMatrixCoefficients.Bt601,
            ColorRange = true,
            SubSamplingX = true,
            SubSamplingY = true,
            ChromaSamplePosition = ObuChromoSamplePosition.Unknown,
            BitDepth = bitDepth
        };

    private static void FillVisible(Buffer2D<byte> plane, int originX, int originY, int width, int height, int seed)
    {
        for (int y = 0; y < height; y++)
        {
            Span<byte> row = plane.DangerousGetRowSpan(originY + y);
            for (int x = 0; x < width; x++)
            {
                row[originX + x] = (byte)(seed + (y * width) + x);
            }
        }
    }

    private static void AssertReplicatedPlane(Buffer2D<byte> plane, int originX, int originY, int width, int height, int seed)
    {
        for (int y = 0; y < plane.Height; y++)
        {
            ReadOnlySpan<byte> row = plane.DangerousGetRowSpan(y);
            int sourceY = Math.Clamp(y - originY, 0, height - 1);
            for (int x = 0; x < row.Length; x++)
            {
                int sourceX = Math.Clamp(x - originX, 0, width - 1);
                Assert.Equal((byte)(seed + (sourceY * width) + sourceX), row[x]);
            }
        }
    }

    private static void AssertReplicatedSingleRow<TSample>(
        Buffer2D<TSample> plane,
        int originX,
        ReadOnlySpan<TSample> expected)
        where TSample : unmanaged, IEquatable<TSample>
    {
        for (int y = 0; y < plane.Height; y++)
        {
            ReadOnlySpan<TSample> row = plane.DangerousGetRowSpan(y);
            for (int x = 0; x < row.Length; x++)
            {
                int sourceX = Math.Clamp(x - originX, 0, expected.Length - 1);
                Assert.Equal(expected[sourceX], row[x]);
            }
        }
    }
}
