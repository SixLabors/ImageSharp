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
    public void Yuv400ToRgbExpandsLimitedRangeLuma()
    {
        // Assign
        using Image<Rgb24> image = new(2, 1);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            2,
            1,
            false,
            ObuMatrixCoefficients.Identity,
            Av1ColorFormat.Yuv400);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false);
        Span<byte> yRow = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(0);
        yRow[0] = 16;
        yRow[1] = 235;

        // Act
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, image.Frames.RootFrame);

        // Assert
        Span<Rgb24> actual = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        Assert.Equal(new Rgb24(0, 0, 0), actual[0]);
        Assert.Equal(new Rgb24(255, 255, 255), actual[1]);
    }

    [Theory]
    [InlineData(Av1BitDepth.TenBit, true, 0, 1023, 512)]
    [InlineData(Av1BitDepth.TenBit, false, 64, 940, 512)]
    [InlineData(Av1BitDepth.TwelveBit, true, 0, 4095, 2048)]
    [InlineData(Av1BitDepth.TwelveBit, false, 256, 3760, 2048)]
    public void HighBitDepthYuvToRgbExpandsSignaledRange(
        int bitDepth,
        bool fullRange,
        ushort black,
        ushort white,
        ushort neutralChroma)
    {
        // Assign
        using Image<Rgb24> image = new(2, 1);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(2, 1, fullRange, bitDepth: (Av1BitDepth)bitDepth);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        Span<ushort> yRow = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, 0, 0, 0);
        yRow[0] = black;
        yRow[1] = white;
        frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, 0, 0, 0).Fill(neutralChroma);
        frameBuffer.GetHighBitDepthRowSpan(Av1Plane.V, 0, 0, 0).Fill(neutralChroma);

        // Act
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, image.Frames.RootFrame);

        // Assert
        Span<Rgb24> actual = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        Assert.Equal(new Rgb24(0, 0, 0), actual[0]);
        Assert.Equal(new Rgb24(255, 255, 255), actual[1]);
    }

    [Theory]
    [InlineData(Av1BitDepth.TenBit)]
    [InlineData(Av1BitDepth.TwelveBit)]
    public void HighBitDepthFrameBufferUsesSampleUnitStrides(int bitDepth)
    {
        // Assign
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            3,
            3,
            colorFormat: Av1ColorFormat.Yuv420,
            bitDepth: (Av1BitDepth)bitDepth);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv420, false);

        // Act
        Span<short> block = frameBuffer.DeriveBlockPointer16(Av1Plane.Y, Point.Empty, 0, 0, out int stride);
        block[stride] = 321;
        Span<ushort> chromaRow = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, 0, 1, 1);

        // Assert
        Assert.Equal(2, frameBuffer.BytesPerSample);
        Assert.Equal(3 + 144, stride);
        Assert.Equal(stride * 2, frameBuffer.BufferY!.Width);
        Assert.Equal(321, frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, 0, 0, 0)[0]);
        Assert.Equal(2, chromaRow.Length);
    }

    [Fact]
    public void Yuv422ToRgbBilinearlyUpsamplesCenteredChroma()
    {
        // Assign
        using Image<Rgb24> image = new(4, 1);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(4, 1, colorFormat: Av1ColorFormat.Yuv422);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv422, false);
        frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(0).Fill(128);
        Span<byte> uRow = frameBuffer.DeriveBlockPointer(Av1Plane.U, 1, 0).DangerousGetRowSpan(0);
        uRow[0] = 128;
        uRow[1] = 192;
        frameBuffer.DeriveBlockPointer(Av1Plane.V, 1, 0).DangerousGetRowSpan(0).Fill(128);

        // Act
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, image.Frames.RootFrame);

        // Assert
        Span<Rgb24> actual = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        Assert.Equal(new Rgb24(128, 128, 128), actual[0]);
        Assert.Equal(new Rgb24(128, 125, 158), actual[1]);
        Assert.Equal(new Rgb24(128, 119, 217), actual[2]);
        Assert.Equal(new Rgb24(128, 116, 247), actual[3]);
    }

    [Theory]
    [InlineData(ObuChromoSamplePosition.Unknown, 158, 98)]
    [InlineData(ObuChromoSamplePosition.Vertical, 187, 98)]
    [InlineData(ObuChromoSamplePosition.Colocated, 187, 69)]
    public void Yuv420ToRgbUsesChromaSamplePosition(int chromaSamplePosition, byte expectedTopBlue, byte expectedLeftBlue)
    {
        // Assign
        using Image<Rgb24> image = new(4, 4);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            4,
            4,
            colorFormat: Av1ColorFormat.Yuv420,
            chromaSamplePosition: (ObuChromoSamplePosition)chromaSamplePosition);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv420, false);
        Buffer2DRegion<byte> yPlane = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        Buffer2DRegion<byte> uPlane = frameBuffer.DeriveBlockPointer(Av1Plane.U, 1, 1);
        Buffer2DRegion<byte> vPlane = frameBuffer.DeriveBlockPointer(Av1Plane.V, 1, 1);
        for (int y = 0; y < yPlane.Height; y++)
        {
            yPlane.DangerousGetRowSpan(y).Fill(128);
        }

        uPlane.DangerousGetRowSpan(0)[0] = 128;
        uPlane.DangerousGetRowSpan(0)[1] = 192;
        uPlane.DangerousGetRowSpan(1)[0] = 64;
        uPlane.DangerousGetRowSpan(1)[1] = 255;
        vPlane.DangerousGetRowSpan(0).Fill(128);
        vPlane.DangerousGetRowSpan(1).Fill(128);

        // Act
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, image.Frames.RootFrame);

        // Assert
        Assert.Equal(expectedTopBlue, image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[1].B);
        Assert.Equal(expectedLeftBlue, image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(1)[0].B);
    }

    [Fact]
    public void Yuv420UsesCeilingChromaPlaneDimensions()
    {
        // Assign
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(3, 3, colorFormat: Av1ColorFormat.Yuv420);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv420, false);

        // Act
        Buffer2DRegion<byte> uPlane = frameBuffer.DeriveBlockPointer(Av1Plane.U, 1, 1);
        Buffer2DRegion<byte> vPlane = frameBuffer.DeriveBlockPointer(Av1Plane.V, 1, 1);

        // Assert
        Assert.Equal(new Size(2, 2), uPlane.Size);
        Assert.Equal(new Size(2, 2), vPlane.Size);
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

    [Theory]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.Bt709)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.Identity)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.SmpteYCgCo)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.Bt709)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.Identity)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.SmpteYCgCo)]
    public void HighBitDepthRoundTrip(int bitDepth, int matrixCoefficients)
    {
        // Assign
        using Image<Rgb24> image = new(3, 1);
        Span<Rgb24> source = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        source[0] = new Rgb24(0, 0, 0);
        source[1] = new Rgb24(150, 100, 50);
        source[2] = new Rgb24(255, 255, 255);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            image.Width,
            image.Height,
            matrixCoefficients: (ObuMatrixCoefficients)matrixCoefficients,
            bitDepth: (Av1BitDepth)bitDepth);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        using Image<Rgb24> actual = new(image.Width, image.Height);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, image.Frames.RootFrame, frameBuffer);
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, actual.Frames.RootFrame);

        // Assert
        Span<Rgb24> actualPixels = actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        for (int x = 0; x < source.Length; x++)
        {
            Assert.Equal(source[x].R, actualPixels[x].R, 1D);
            Assert.Equal(source[x].G, actualPixels[x].G, 1D);
            Assert.Equal(source[x].B, actualPixels[x].B, 1D);
        }
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
        ObuMatrixCoefficients matrixCoefficients = ObuMatrixCoefficients.Bt709,
        Av1ColorFormat colorFormat = Av1ColorFormat.Yuv444,
        ObuChromoSamplePosition chromaSamplePosition = ObuChromoSamplePosition.Unknown,
        Av1BitDepth bitDepth = Av1BitDepth.EightBit)
        => new()
        {
            MaxFrameWidth = width,
            MaxFrameHeight = height,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
                BitDepth = bitDepth,
                MatrixCoefficients = matrixCoefficients,
                ColorRange = fullRange,
                SubSamplingX = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422,
                SubSamplingY = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420,
                ChromaSamplePosition = chromaSamplePosition,
            },
        };
}
