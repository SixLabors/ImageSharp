// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

public class Av1EncoderFrameTests
{
    [Fact]
    public void PrepareSourceConvertsRgba32DirectlyIntoBorderedEightBitPlane()
    {
        const int width = 4;
        const int height = 1;
        const int codedWidth = 8;
        const int codedHeight = 8;
        const int border = Av1EncoderFrame<byte>.LumaBorder;
        MemoryAllocator allocator = Configuration.Default.MemoryAllocator;

        using Image<Rgba32> image = new(width, height);
        image[0, 0] = new Rgba32(byte.MaxValue, 0, 0, 0);
        image[1, 0] = new Rgba32(0, byte.MaxValue, 0);
        image[2, 0] = new Rgba32(0, 0, byte.MaxValue);
        image[3, 0] = new Rgba32(byte.MaxValue, byte.MaxValue, byte.MaxValue);

        Size bufferSize = Av1EncoderFrame<byte>.GetPlaneBufferSize(width, height, 0, 0);
        using Buffer2D<byte> luma = allocator.Allocate2D<byte>(bufferSize.Width, bufferSize.Height);
        Buffer2DRegion<byte> lumaRegion = luma.GetRegion(border, border, codedWidth, codedHeight);
        Av1EncoderFrame<byte> frame = new(lumaRegion, width, height, 8);
        ObuColorConfig colorConfig = CreateMonochromeColorConfig(Av1BitDepth.EightBit);

        Av1FrameEncoder.PrepareSource(Configuration.Default, image.Frames.RootFrame, frame, colorConfig);

        byte[] expected = [76, 150, 29, 255];
        AssertReplicatedSingleRow(luma, border, expected);
    }

    [Fact]
    public void PrepareSourcePreservesHighBitDepthPrecision()
    {
        const int width = 4;
        const int height = 1;
        const int codedWidth = 8;
        const int codedHeight = 8;
        const int border = Av1EncoderFrame<ushort>.LumaBorder;
        MemoryAllocator allocator = Configuration.Default.MemoryAllocator;

        using Image<Rgba64> image = new(width, height);
        image[0, 0] = new Rgba64(ushort.MaxValue, 0, 0, 0);
        image[1, 0] = new Rgba64(0, ushort.MaxValue, 0, ushort.MaxValue);
        image[2, 0] = new Rgba64(0, 0, ushort.MaxValue, ushort.MaxValue);
        image[3, 0] = new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);

        Size bufferSize = Av1EncoderFrame<ushort>.GetPlaneBufferSize(width, height, 0, 0);
        using Buffer2D<ushort> luma = allocator.Allocate2D<ushort>(bufferSize.Width, bufferSize.Height);
        Buffer2DRegion<ushort> lumaRegion = luma.GetRegion(border, border, codedWidth, codedHeight);
        Av1EncoderFrame<ushort> frame = new(lumaRegion, width, height, 10);
        ObuColorConfig colorConfig = CreateMonochromeColorConfig(Av1BitDepth.TenBit);

        Av1FrameEncoder.PrepareSource(Configuration.Default, image.Frames.RootFrame, frame, colorConfig);

        ushort[] expected = [306, 601, 117, 1023];
        AssertReplicatedSingleRow(luma, border, expected);
    }

    [Fact]
    public void ExtendBordersReplicatesEveryPhysicalPlaneEdge()
    {
        const int visibleWidth = 5;
        const int visibleHeight = 3;
        const int codedWidth = 8;
        const int codedHeight = 8;
        const int lumaBorder = Av1EncoderFrame<byte>.LumaBorder;
        const int chromaBorder = lumaBorder / 2;
        MemoryAllocator allocator = Configuration.Default.MemoryAllocator;

        Size lumaBufferSize = Av1EncoderFrame<byte>.GetPlaneBufferSize(visibleWidth, visibleHeight, 0, 0);
        Size chromaBufferSize = Av1EncoderFrame<byte>.GetPlaneBufferSize(visibleWidth, visibleHeight, 1, 1);
        using Buffer2D<byte> luma = allocator.Allocate2D<byte>(lumaBufferSize.Width, lumaBufferSize.Height);
        using Buffer2D<byte> chromaBlue = allocator.Allocate2D<byte>(chromaBufferSize.Width, chromaBufferSize.Height);
        using Buffer2D<byte> chromaRed = allocator.Allocate2D<byte>(chromaBufferSize.Width, chromaBufferSize.Height);
        Buffer2DRegion<byte> lumaRegion = luma.GetRegion(lumaBorder, lumaBorder, codedWidth, codedHeight);
        Buffer2DRegion<byte> chromaBlueRegion = chromaBlue.GetRegion(chromaBorder, chromaBorder, codedWidth / 2, codedHeight / 2);
        Buffer2DRegion<byte> chromaRedRegion = chromaRed.GetRegion(chromaBorder, chromaBorder, codedWidth / 2, codedHeight / 2);

        FillVisible(luma, lumaBorder, lumaBorder, visibleWidth, visibleHeight, 10);
        FillVisible(chromaBlue, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 80);
        FillVisible(chromaRed, chromaBorder, chromaBorder, (visibleWidth + 1) / 2, (visibleHeight + 1) / 2, 120);

        Av1EncoderFrame<byte> frame = new(
            lumaRegion,
            chromaBlueRegion,
            chromaRedRegion,
            visibleWidth,
            visibleHeight,
            8,
            Av1ColorFormat.Yuv420,
            1,
            1);

        frame.ExtendBorders();

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
