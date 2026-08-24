// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1YuvConverterTests
{
    [Theory]
    [InlineData(255, 255, 255, 255, 128, 128, true, ObuMatrixCoefficients.Bt709)]
    [InlineData(0, 0, 0, 0, 128, 128, true, ObuMatrixCoefficients.Bt709)]
    [InlineData(42, 42, 42, 42, 128, 128, true, ObuMatrixCoefficients.Bt709)]
    [InlineData(150, 100, 50, 107, 97, 155, true, ObuMatrixCoefficients.Bt709)]
    [InlineData(150, 100, 50, 100, 50, 150, true, ObuMatrixCoefficients.Identity)]
    [InlineData(150, 100, 50, 110, 95, 157, true, ObuMatrixCoefficients.Fcc)]
    [InlineData(150, 100, 50, 109, 95, 157, true, ObuMatrixCoefficients.Bt470BG)]
    [InlineData(150, 100, 50, 109, 95, 157, true, ObuMatrixCoefficients.Bt601)]
    [InlineData(150, 100, 50, 109, 95, 157, true, ObuMatrixCoefficients.Unspecified)]
    [InlineData(150, 100, 50, 106, 97, 156, true, ObuMatrixCoefficients.Smpte240)]
    [InlineData(150, 100, 50, 100, 128, 178, true, ObuMatrixCoefficients.SmpteYCgCo)]
    [InlineData(150, 100, 50, 110, 96, 155, true, ObuMatrixCoefficients.Bt2020NonConstantLuminance)]
    [InlineData(255, 255, 255, 235, 128, 128, false, ObuMatrixCoefficients.Bt709)]
    [InlineData(0, 0, 0, 16, 128, 128, false, ObuMatrixCoefficients.Bt709)]
    [InlineData(42, 42, 42, 52, 128, 128, false, ObuMatrixCoefficients.Bt709)]
    [InlineData(150, 100, 50, 108, 101, 152, false, ObuMatrixCoefficients.Bt709)]
    public void RgbToYuvSinglePixel(byte r, byte g, byte b, int y, int u, int v, bool fullRange, int matrixCoefficients)
    {
        // Assign
        using Image<Rgb24> image = new(1, 1);
        ImageFrame<Rgb24> frame = image.Frames.RootFrame;
        frame.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> memory);
        memory.Span[0] = new Rgb24(r, g, b);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(1, 1, fullRange, (ObuMatrixCoefficients)matrixCoefficients);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, frame, frameBuffer);

        // Assert
        byte actualY = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(0)[0];
        byte actualU = frameBuffer.DeriveBlockPointer(Av1Plane.U, 0, 0).DangerousGetRowSpan(0)[0];
        byte actualV = frameBuffer.DeriveBlockPointer(Av1Plane.V, 0, 0).DangerousGetRowSpan(0)[0];
        Assert.Equal(y, actualY);
        Assert.Equal(u, actualU);
        Assert.Equal(v, actualV);
    }

    [Theory]
    [InlineData(255, 255, 255, 255, 128, 128, true, ObuMatrixCoefficients.Bt709)]
    [InlineData(0, 0, 0, 0, 128, 128, true, ObuMatrixCoefficients.Bt709)]
    [InlineData(42, 42, 42, 42, 128, 128, true, ObuMatrixCoefficients.Bt709)]
    [InlineData(150, 100, 50, 107, 97, 155, true, ObuMatrixCoefficients.Bt709)]
    [InlineData(150, 100, 50, 100, 50, 150, true, ObuMatrixCoefficients.Identity)]
    [InlineData(150, 100, 50, 110, 95, 157, true, ObuMatrixCoefficients.Fcc)]
    [InlineData(150, 100, 50, 109, 95, 157, true, ObuMatrixCoefficients.Bt470BG)]
    [InlineData(150, 100, 50, 109, 95, 157, true, ObuMatrixCoefficients.Bt601)]
    [InlineData(150, 100, 50, 109, 95, 157, true, ObuMatrixCoefficients.Unspecified)]
    [InlineData(150, 100, 50, 106, 97, 156, true, ObuMatrixCoefficients.Smpte240)]
    [InlineData(150, 100, 50, 100, 128, 178, true, ObuMatrixCoefficients.SmpteYCgCo)]
    [InlineData(150, 100, 50, 110, 96, 155, true, ObuMatrixCoefficients.Bt2020NonConstantLuminance)]
    [InlineData(255, 255, 255, 235, 128, 128, false, ObuMatrixCoefficients.Bt709)]
    [InlineData(0, 0, 0, 16, 128, 128, false, ObuMatrixCoefficients.Bt709)]
    [InlineData(42, 42, 42, 52, 128, 128, false, ObuMatrixCoefficients.Bt709)]
    [InlineData(150, 100, 50, 108, 101, 152, false, ObuMatrixCoefficients.Bt709)]
    public void YuvToRgbSinglePixel(byte r, byte g, byte b, int y, int u, int v, bool fullRange, int matrixCoefficients)
    {
        // Assign
        using Image<Rgb24> image = new(1, 1);
        ImageFrame<Rgb24> frame = image.Frames.RootFrame;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(1, 1, fullRange, (ObuMatrixCoefficients)matrixCoefficients);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(0)[0] = (byte)y;
        frameBuffer.DeriveBlockPointer(Av1Plane.U, 0, 0).DangerousGetRowSpan(0)[0] = (byte)u;
        frameBuffer.DeriveBlockPointer(Av1Plane.V, 0, 0).DangerousGetRowSpan(0)[0] = (byte)v;

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
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(image.Width, image.Height);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, frame, frameBuffer);
        Span<Rgb24> referenceOutput = Av1ReferenceYuvConverter.RgbToYuv(memory.Span, true);

        // Assert
        Span<Rgb24> actual = new Rgb24[frameBuffer.Width];
        Span<byte> yRow = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(0);
        Span<byte> uRow = frameBuffer.DeriveBlockPointer(Av1Plane.U, 0, 0).DangerousGetRowSpan(0);
        Span<byte> vRow = frameBuffer.DeriveBlockPointer(Av1Plane.V, 0, 0).DangerousGetRowSpan(0);
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
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(image.Width, image.Height);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        Random rnd = new(42);
        CreateTestData(rnd, frameBuffer, Av1Plane.Y);
        CreateTestData(rnd, frameBuffer, Av1Plane.U);
        CreateTestData(rnd, frameBuffer, Av1Plane.V);

        // Act
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, frame);
        Span<Rgb24> referenceOutput = Av1ReferenceYuvConverter.YuvToRgb(frameBuffer, true);

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

    private static void CreateTestData(Random rnd, Av1FrameBuffer<byte> frameBuffer, Av1Plane plane)
    {
        const int bitCount = 8;
        Buffer2DRegion<byte> region = frameBuffer.DeriveBlockPointer(plane, 0, 0);
        for (int y = 0; y < region.Height; y++)
        {
            CreateTestData(rnd, region.DangerousGetRowSpan(y), bitCount);
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
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(1, 1);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
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

    // [Theory]
    // [WithFile(TestImages.Jpeg.Baseline.Winter444_Interleaved, PixelTypes.Rgb24)]
    public void RoundTrip(TestImageProvider<Rgb24> provider)
    {
        // Assign
        using Image<Rgb24> image = provider.GetImage();
        ImageFrame<Rgb24> frame = image.Frames.RootFrame;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(image.Width, image.Height);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        using Image<Rgb24> actual = new(image.Width, image.Height);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, frame, frameBuffer);
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, actual.Frames.RootFrame);

        // Assert
        ImageComparer.Tolerant(0.002F).VerifySimilarity(image, actual);
    }

    private static ObuSequenceHeader CreateSequenceHeader(
        int width,
        int height,
        bool fullRange = true,
        ObuMatrixCoefficients matrixCoefficients = ObuMatrixCoefficients.Bt709)
        => new()
        {
            MaxFrameWidth = width,
            MaxFrameHeight = height,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = false,
                BitDepth = Av1BitDepth.EightBit,
                MatrixCoefficients = matrixCoefficients,
                ColorRange = fullRange,
            },
        };
}
