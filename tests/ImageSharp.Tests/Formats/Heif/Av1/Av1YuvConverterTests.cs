// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1YuvConverterTests
{
    [Theory]
    [InlineData(255, 255, 255, 255, 127, 127)]
    [InlineData(0, 0, 0, 0, 127, 127)]
    [InlineData(42, 42, 42, 42, 127, 127)]
    [InlineData(150, 100, 50, 107, 97, 154)]
    public void RgbToYuvSinglePixel(byte r, byte g, byte b, int y, int u, int v)
    {
        // Assign
        using Image<Rgb24> image = new(1, 1);
        ImageFrame<Rgb24> frame = image.Frames.RootFrame;
        frame.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> memory);
        memory.Span[0] = new Rgb24(r, g, b);
        ObuSequenceHeader sequenceHeader = new();
        sequenceHeader.MaxFrameWidth = 1;
        sequenceHeader.MaxFrameHeight = 1;
        Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, frame, frameBuffer);

        // Assert
        byte actualY = frameBuffer.BufferY.DangerousGetRowSpan(0)[0];
        byte actualU = frameBuffer.BufferCb.DangerousGetRowSpan(0)[0];
        byte actualV = frameBuffer.BufferCr.DangerousGetRowSpan(0)[0];
        Assert.Equal(y, actualY);
        Assert.Equal(u, actualU);
        Assert.Equal(v, actualV);
    }

    [Theory]
    [InlineData(255, 255, 255, 255, 127, 127)]
    [InlineData(0, 0, 0, 0, 127, 127)]
    [InlineData(42, 42, 42, 42, 127, 127)]
    [InlineData(150, 100, 50, 107, 97, 154)]
    public void YuvToRgbSinglePixel(byte r, byte g, byte b, int y, int u, int v)
    {
        // Assign
        using Image<Rgb24> image = new(1, 1);
        ImageFrame<Rgb24> frame = image.Frames.RootFrame;
        ObuSequenceHeader sequenceHeader = new();
        sequenceHeader.MaxFrameWidth = 1;
        sequenceHeader.MaxFrameHeight = 1;
        Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        frameBuffer.BufferY.DangerousGetRowSpan(0)[0] = (byte)y;
        frameBuffer.BufferCb.DangerousGetRowSpan(0)[0] = (byte)u;
        frameBuffer.BufferCr.DangerousGetRowSpan(0)[0] = (byte)v;

        // Act
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, frame);

        // Assert
        frame.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> memory);
        Rgb24 actual = memory.Span[0];
        Assert.Equal(r, actual.R, 1d);
        Assert.Equal(g, actual.G, 1d);
        Assert.Equal(b, actual.B, 1d);
    }

    [Fact]
    public void RgbToYuvCompareToReferenceRandomPixels()
    {
        const int sampleCount = 1000;

        // Assign
        using Image<Rgb24> image = new(sampleCount, 1);
        ImageFrame<Rgb24> frame = image.Frames.RootFrame;
        frame.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> memory);
        Random rnd = new(42);
        Span<byte> input = new byte[sampleCount * 3];
        CreateTestData(rnd, input);
        PixelOperations<Rgb24>.Instance.FromBgr24Bytes(Configuration.Default, input, memory.Span, image.Width);
        ObuSequenceHeader sequenceHeader = new();
        sequenceHeader.MaxFrameWidth = image.Width;
        sequenceHeader.MaxFrameHeight = image.Height;
        Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, frame, frameBuffer);
        Span<Rgb24> referenceOutput = Av1ReferenceYuvConverter.RgbToYuv(memory.Span, true);

        // Assert
        Span<Rgb24> actual = new Rgb24[frameBuffer.Width];
        Span<byte> yRow = frameBuffer.BufferY!.DangerousGetSingleSpan();
        Span<byte> uRow = frameBuffer.BufferCb!.DangerousGetSingleSpan();
        Span<byte> vRow = frameBuffer.BufferCr!.DangerousGetSingleSpan();
        for (int i = 0; i < frameBuffer.Width; i++)
        {
            Rgb24 pixel = new();
            pixel.R = yRow[i];
            pixel.G = uRow[i];
            pixel.B = vRow[i];
            actual[i] = pixel;
        }

        Compare(referenceOutput, actual, 3);
    }

    [Fact]
    public void YuvToRgbCompareToReferenceRandomPixels()
    {
        const int sampleCount = 1000;

        // Assign
        using Image<Rgb24> image = new(sampleCount, 1);
        ImageFrame<Rgb24> frame = image.Frames.RootFrame;
        ObuSequenceHeader sequenceHeader = new();
        sequenceHeader.MaxFrameWidth = image.Width;
        sequenceHeader.MaxFrameHeight = image.Height;
        Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        Random rnd = new(42);
        CreateTestData(rnd, frameBuffer.BufferY.DangerousGetRowSpan(0));
        CreateTestData(rnd, frameBuffer.BufferCb.DangerousGetRowSpan(0));
        CreateTestData(rnd, frameBuffer.BufferCr.DangerousGetRowSpan(0));

        // Act
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, frame);
        Span<Rgb24> referenceOutput = Av1ReferenceYuvConverter.YuvToRgb(frameBuffer, false);

        // Assert
        frame.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> memory);
        Span<Rgb24> actual = memory.Span;
        Compare(referenceOutput, actual, 3);
    }

    private static void Compare(Span<Rgb24> referenceOutput, Span<Rgb24> actual, int allowedDifference)
    {
        for (int i = 0; i < actual.Length; i++)
        {
            if (Math.Abs(referenceOutput[i].R - actual[i].R) > allowedDifference ||
                Math.Abs(referenceOutput[i].G - actual[i].G) > allowedDifference ||
                Math.Abs(referenceOutput[i].B - actual[i].B) > allowedDifference)
            {
                Assert.Fail($"Difference at index {i}, expected: {referenceOutput[i]} but was {actual[i]}");
            }
        }
    }

    private static void CreateTestData(Random rnd, Span<byte> span, int bitCount = 8)
    {
        int max = (1 << bitCount) - 1;
        for (int i = 0; i < span.Length; i++)
        {
            byte current = (byte)rnd.Next(max);
            span[i] = current;
        }
    }

    private static void CreateTestData(Random rnd, Span<ushort> span, int bitCount)
    {
        int max = (1 << bitCount) - 1;
        for (int i = 0; i < span.Length; i++)
        {
            ushort current = (ushort)rnd.Next(max);
            span[i] = current;
        }
    }

    [Theory]
    [InlineData(255, 255, 255)]
    [InlineData(0, 0, 0)]
    [InlineData(42, 42, 42)]
    [InlineData(42, 0, 0)]
    [InlineData(42, 42, 0)]
    [InlineData(42, 0, 42)]
    [InlineData(0, 42, 42)]
    [InlineData(0, 0, 42)]
    [InlineData(150, 100, 50)]
    public void RoundTripSinglePixel(byte r, byte g, byte b)
    {
        // Assign
        using Image<Rgb24> image = new(1, 1);
        ImageFrame<Rgb24> frame = image.Frames.RootFrame;
        frame.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> memory);
        memory.Span[0] = new Rgb24(r, g, b);
        ObuSequenceHeader sequenceHeader = new();
        sequenceHeader.MaxFrameWidth = 1;
        sequenceHeader.MaxFrameHeight = 1;
        Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        using Image<Rgb24> actual = new(image.Width, image.Height);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, frame, frameBuffer);
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, actual.Frames.RootFrame);

        // Assert
        actual.Frames.RootFrame.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> actualMemory);
        Rgb24 actualPixel = actualMemory.Span[0];
        Assert.Equal(r, actualPixel.R, 2d);
        Assert.Equal(g, actualPixel.G, 2d);
        Assert.Equal(b, actualPixel.B, 2d);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Winter444_Interleaved, PixelTypes.Rgb24)]
    public void RoundTrip(TestImageProvider<Rgb24> provider)
    {
        // Assign
        using Image<Rgb24> image = provider.GetImage();
        ImageFrame<Rgb24> frame = image.Frames.RootFrame;
        ObuSequenceHeader sequenceHeader = new();
        sequenceHeader.MaxFrameWidth = image.Width;
        sequenceHeader.MaxFrameHeight = image.Height;
        Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        using Image<Rgb24> actual = new(image.Width, image.Height);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, frame, frameBuffer);
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, actual.Frames.RootFrame);

        // Assert
        ImageComparer.Tolerant(0.002F).VerifySimilarity(image, actual);
    }
}
