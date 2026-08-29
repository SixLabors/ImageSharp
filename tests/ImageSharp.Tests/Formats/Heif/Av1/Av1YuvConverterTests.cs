// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 color conversion, sample-range handling, chroma reconstruction, and encoder downsampling.
/// </summary>
[Trait("Format", "Avif")]
public class Av1YuvConverterTests
{
    /// <summary>
    /// The hardware configurations covering 512-bit, 256-bit, 128-bit, and scalar conversion paths.
    /// </summary>
    private const HwIntrinsics AlphaConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies known RGB-to-YUV values across coefficient, identity, and YCgCo matrices and sample ranges.
    /// </summary>
    /// <param name="r">The source red component.</param>
    /// <param name="g">The source green component.</param>
    /// <param name="b">The source blue component.</param>
    /// <param name="y">The expected luma or first encoded component.</param>
    /// <param name="u">The expected first chroma or second encoded component.</param>
    /// <param name="v">The expected second chroma or third encoded component.</param>
    /// <param name="fullRange">Whether the encoded samples use the full range.</param>
    /// <param name="matrixCoefficients">The matrix coefficients used for conversion.</param>
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

    /// <summary>
    /// Verifies known YUV-to-RGB values across coefficient, identity, and YCgCo matrices and sample ranges.
    /// </summary>
    /// <param name="r">The expected red component.</param>
    /// <param name="g">The expected green component.</param>
    /// <param name="b">The expected blue component.</param>
    /// <param name="y">The source luma or first encoded component.</param>
    /// <param name="u">The source first chroma or second encoded component.</param>
    /// <param name="v">The source second chroma or third encoded component.</param>
    /// <param name="fullRange">Whether the encoded samples use the full range.</param>
    /// <param name="matrixCoefficients">The matrix coefficients used for conversion.</param>
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

    /// <summary>
    /// Verifies that limited-range monochrome samples expand to the complete RGB output range.
    /// </summary>
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

    /// <summary>
    /// Verifies RGB-to-monochrome conversion and range quantization for every supported AV1 bit depth.
    /// </summary>
    /// <param name="bitDepth">The encoded AV1 bit depth.</param>
    /// <param name="fullRange">Whether the luma samples use the full range.</param>
    /// <param name="expectedLuma">The expected encoded luma sample.</param>
    [Theory]
    [InlineData(Av1BitDepth.EightBit, true, 107)]
    [InlineData(Av1BitDepth.EightBit, false, 108)]
    [InlineData(Av1BitDepth.TenBit, true, 429)]
    [InlineData(Av1BitDepth.TenBit, false, 432)]
    [InlineData(Av1BitDepth.TwelveBit, true, 1719)]
    [InlineData(Av1BitDepth.TwelveBit, false, 1727)]
    public void RgbToYuv400WritesQuantizedLuma(int bitDepth, bool fullRange, int expectedLuma)
    {
        // Rgb48 values scaled from eight-bit components exercise the precision-preserving high-bit-depth path.
        using Image<Rgb48> image = new(1, 1);
        image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[0] = new Rgb48(150 * 257, 100 * 257, 50 * 257);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            1,
            1,
            fullRange,
            colorFormat: Av1ColorFormat.Yuv400,
            bitDepth: (Av1BitDepth)bitDepth);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false);

        Av1YuvConverter.ConvertFromRgb(Configuration.Default, image.Frames.RootFrame, frameBuffer);

        Assert.Equal(expectedLuma, GetPlaneSample(frameBuffer, Av1Plane.Y, 0, 0, 0, 0));
        Assert.Null(frameBuffer.BufferCb);
        Assert.Null(frameBuffer.BufferCr);
    }

    /// <summary>
    /// Verifies full- and limited-range expansion for 10-bit and 12-bit reconstructed samples.
    /// </summary>
    /// <param name="bitDepth">The reconstructed AV1 bit depth.</param>
    /// <param name="fullRange">Whether the samples use the full range.</param>
    /// <param name="black">The encoded black luma sample.</param>
    /// <param name="white">The encoded white luma sample.</param>
    /// <param name="neutralChroma">The neutral encoded chroma sample.</param>
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

    /// <summary>
    /// Verifies that high-bit-depth frame strides and row access use 16-bit sample units consistently.
    /// </summary>
    /// <param name="bitDepth">The reconstructed AV1 bit depth.</param>
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
        Assert.Equal(3 + (frameBuffer.OriginX * 2), stride);
        Assert.Equal(stride * 2, frameBuffer.BufferY!.Width);
        Assert.Equal(321, frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, 0, 0, 0)[0]);
        Assert.Equal(2, chromaRow.Length);
    }

    /// <summary>
    /// Verifies libyuv's native two-times presentation filter at byte and twelve-bit precision across every
    /// available intrinsic width and the scalar fallback.
    /// </summary>
    [Fact]
    public void ScaleSelectedSpatialLayerMatchesPinnedLibyuvAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSelectedSpatialLayerScaling,
            AlphaConfigurations);

    /// <summary>
    /// Verifies the exact edge extension, quarter-sample weights, and rounding of the native presentation scaler.
    /// </summary>
    private static void ValidateSelectedSpatialLayerScaling()
    {
        byte[][] expectedByteRows =
        [
            [0, 25, 75, 125, 175, 200],
            [13, 38, 88, 138, 188, 213],
            [38, 63, 113, 163, 213, 238],
            [50, 75, 125, 175, 225, 250],
        ];

        ushort[][] expectedHighBitDepthRows =
        [
            [0, 250, 750, 1250, 1750, 2000],
            [125, 375, 875, 1375, 1875, 2125],
            [375, 625, 1125, 1625, 2125, 2375],
            [500, 750, 1250, 1750, 2250, 2500],
        ];

        ObuSequenceHeader byteSequenceHeader = CreateSequenceHeader(
            3,
            2,
            colorFormat: Av1ColorFormat.Yuv400);

        using (Av1FrameBuffer<byte> frameBuffer = new(
            Configuration.Default,
            byteSequenceHeader,
            Av1ColorFormat.Yuv400,
            false))
        {
            byte[] sourceSamples = [0, 100, 200, 50, 150, 250];
            for (int y = 0; y < byteSequenceHeader.MaxFrameHeight; y++)
            {
                sourceSamples.AsSpan(y * byteSequenceHeader.MaxFrameWidth, byteSequenceHeader.MaxFrameWidth).CopyTo(
                    frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(y));
            }

            Av1PlanarSampleBuffer<byte> source = new(frameBuffer);
            using Av1PresentationSampleBuffer<byte, Av1PlanarSampleBuffer<byte>> presentation = new(
                Configuration.Default,
                source,
                6,
                4);

            for (int y = 0; y < expectedByteRows.Length; y++)
            {
                Assert.True(presentation.View.GetLumaRowSpan(y).SequenceEqual(expectedByteRows[y]));
            }
        }

        ObuSequenceHeader highBitDepthSequenceHeader = CreateSequenceHeader(
            3,
            2,
            colorFormat: Av1ColorFormat.Yuv400,
            bitDepth: Av1BitDepth.TwelveBit);

        using Av1FrameBuffer<byte> highBitDepthFrameBuffer = new(
            Configuration.Default,
            highBitDepthSequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        ushort[] highBitDepthSourceSamples = [0, 1000, 2000, 500, 1500, 2500];
        for (int y = 0; y < highBitDepthSequenceHeader.MaxFrameHeight; y++)
        {
            highBitDepthSourceSamples.AsSpan(
                y * highBitDepthSequenceHeader.MaxFrameWidth,
                highBitDepthSequenceHeader.MaxFrameWidth).CopyTo(
                highBitDepthFrameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, y, 0, 0));
        }

        Av1PlanarSampleBuffer<ushort> highBitDepthSource = new(highBitDepthFrameBuffer);
        using Av1PresentationSampleBuffer<ushort, Av1PlanarSampleBuffer<ushort>> highBitDepthPresentation = new(
            Configuration.Default,
            highBitDepthSource,
            6,
            4);

        for (int y = 0; y < expectedHighBitDepthRows.Length; y++)
        {
            Assert.True(highBitDepthPresentation.View.GetLumaRowSpan(y).SequenceEqual(expectedHighBitDepthRows[y]));
        }
    }

    /// <summary>
    /// Verifies centered horizontal chroma reconstruction for a YUV 4:2:2 frame.
    /// </summary>
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

    /// <summary>
    /// Verifies vertical and horizontal YUV 4:2:0 reconstruction at every AV1 chroma sample position.
    /// </summary>
    /// <param name="chromaSamplePosition">The signaled AV1 chroma sample position.</param>
    /// <param name="expectedTopBlue">The expected blue component in the top-row probe pixel.</param>
    /// <param name="expectedLeftBlue">The expected blue component in the left-column probe pixel.</param>
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

    /// <summary>
    /// Verifies that encoding selects the horizontal and vertical sample coordinates defined by each AV1 chroma position.
    /// </summary>
    /// <param name="chromaSamplePosition">The signaled AV1 chroma sample position.</param>
    /// <param name="expectedChromaBlue">The expected encoded blue-difference sample.</param>
    /// <param name="expectedChromaRed">The expected encoded red-difference sample.</param>
    [Theory]
    [InlineData(ObuChromoSamplePosition.Unknown, 128, 128)]
    [InlineData(ObuChromoSamplePosition.Vertical, 96, 192)]
    [InlineData(ObuChromoSamplePosition.Colocated, 128, 128)]
    public void RgbToYuv420UsesChromaSamplePosition(int chromaSamplePosition, byte expectedChromaBlue, byte expectedChromaRed)
    {
        using Image<Rgba32> source = new(2, 2);

        // The four distinct YCgCo samples make left, centered, and vertically averaged selection observable as
        // exact integer chroma values without introducing transfer-function or coefficient-rounding tolerances.
        source[0, 0] = new Rgba32(0, 0, 0);
        source[1, 0] = new Rgba32(0, byte.MaxValue, 0);
        source[0, 1] = new Rgba32(byte.MaxValue, 0, 0);
        source[1, 1] = new Rgba32(0, 0, byte.MaxValue);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            2,
            2,
            matrixCoefficients: ObuMatrixCoefficients.SmpteYCgCo,
            colorFormat: Av1ColorFormat.Yuv420,
            chromaSamplePosition: (ObuChromoSamplePosition)chromaSamplePosition);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv420, false);
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, source.Frames.RootFrame, frameBuffer);

        Assert.Equal(expectedChromaBlue, frameBuffer.DeriveBlockPointer(Av1Plane.U, 1, 1).DangerousGetRowSpan(0)[0]);
        Assert.Equal(expectedChromaRed, frameBuffer.DeriveBlockPointer(Av1Plane.V, 1, 1).DangerousGetRowSpan(0)[0]);
    }

    /// <summary>
    /// Verifies that odd image dimensions retain the final YUV 4:2:0 chroma row and column.
    /// </summary>
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

    /// <summary>
    /// Compares SIMD-first RGB-to-YUV conversion with the independent scalar reference over randomized pixels.
    /// </summary>
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

    /// <summary>
    /// Compares SIMD-first YUV-to-RGB conversion with the independent scalar reference over randomized samples.
    /// </summary>
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

    /// <summary>
    /// Compares packed RGB rows within the permitted per-component tolerance.
    /// </summary>
    /// <param name="referenceOutput">The independently converted reference pixels.</param>
    /// <param name="actual">The pixels produced by the implementation under test.</param>
    /// <param name="allowedDifference">The permitted absolute component difference.</param>
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

    /// <summary>
    /// Fills one reconstructed plane with deterministic pseudo-random test samples.
    /// </summary>
    /// <param name="rnd">The deterministic random number generator.</param>
    /// <param name="frameBuffer">The frame containing the destination plane.</param>
    /// <param name="plane">The destination plane.</param>
    private static void CreateTestData(Random rnd, Av1FrameBuffer<byte> frameBuffer, Av1Plane plane)
    {
        const int bitCount = 8;
        Buffer2DRegion<byte> region = frameBuffer.DeriveBlockPointer(plane, 0, 0);
        for (int y = 0; y < region.Height; y++)
        {
            CreateTestData(rnd, region.DangerousGetRowSpan(y), bitCount);
        }
    }

    /// <summary>
    /// Fills an eight-bit sample span with deterministic pseudo-random values.
    /// </summary>
    /// <param name="rnd">The deterministic random number generator.</param>
    /// <param name="span">The destination sample span.</param>
    /// <param name="bitCount">The number of significant sample bits.</param>
    private static void CreateTestData(Random rnd, Span<byte> span, int bitCount = 8)
    {
        int max = (1 << bitCount) - 1;
        for (int i = 0; i < span.Length; i++)
        {
            byte current = (byte)rnd.Next(max);
            span[i] = current;
        }
    }

    /// <summary>
    /// Fills a high-bit-depth sample span with deterministic pseudo-random values.
    /// </summary>
    /// <param name="rnd">The deterministic random number generator.</param>
    /// <param name="span">The destination sample span.</param>
    /// <param name="bitCount">The number of significant sample bits.</param>
    private static void CreateTestData(Random rnd, Span<ushort> span, int bitCount)
    {
        int max = (1 << bitCount) - 1;
        for (int i = 0; i < span.Length; i++)
        {
            ushort current = (ushort)rnd.Next(max);
            span[i] = current;
        }
    }

    /// <summary>
    /// Verifies RGB-to-YUV-to-RGB conversion for representative single-pixel colors.
    /// </summary>
    /// <param name="r">The source red component.</param>
    /// <param name="g">The source green component.</param>
    /// <param name="b">The source blue component.</param>
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

    /// <summary>
    /// Verifies 10-bit and 12-bit round trips for coefficient, identity, and YCgCo matrices.
    /// </summary>
    /// <param name="bitDepth">The encoded AV1 bit depth.</param>
    /// <param name="matrixCoefficients">The matrix coefficients used for conversion.</param>
    [Theory]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.Bt709)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.Identity)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.SmpteYCgCo)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.IptC2)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.YCgCoRe)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.YCgCoRo)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.Bt709)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.Identity)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.SmpteYCgCo)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.IptC2)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.YCgCoRe)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.YCgCoRo)]
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

    /// <summary>
    /// Verifies the H.273 IPT-C2 matrices in both directions against independently calculated code values.
    /// </summary>
    [Fact]
    public void IptC2MatchesKnownLinearTransferValuesInBothDirections()
    {
        // Assign
        // The linear transfer characteristic isolates the two normative IPT-C2 matrices from transfer-curve error.
        using Image<Rgb24> source = new(1, 1);
        source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[0] = new Rgb24(150, 100, 50);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            1,
            1,
            matrixCoefficients: ObuMatrixCoefficients.IptC2,
            transferCharacteristics: ObuTransferCharacteristics.Linear);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, source.Frames.RootFrame, frameBuffer);

        // Assert
        Assert.Equal(100, frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(0)[0]);
        Assert.Equal(178, frameBuffer.DeriveBlockPointer(Av1Plane.U, 0, 0).DangerousGetRowSpan(0)[0]);
        Assert.Equal(198, frameBuffer.DeriveBlockPointer(Av1Plane.V, 0, 0).DangerousGetRowSpan(0)[0]);

        using Image<Rgb24> destination = new(1, 1);
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, destination.Frames.RootFrame);

        Assert.Equal(new Rgb24(150, 100, 51), destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[0]);
    }

    /// <summary>
    /// Verifies the reversible YCgCo lifting stages against known pure-red code values in both directions.
    /// </summary>
    /// <param name="bitDepth">The encoded AV1 bit depth.</param>
    /// <param name="matrixCoefficients">The reversible YCgCo variant.</param>
    /// <param name="expectedY">The expected encoded luma value.</param>
    /// <param name="expectedU">The expected encoded Cg value.</param>
    /// <param name="expectedV">The expected encoded Co value.</param>
    [Theory]
    [InlineData(Av1BitDepth.EightBit, ObuMatrixCoefficients.YCgCoRe, 15, 97, 191)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.YCgCoRe, 63, 385, 767)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.YCgCoRe, 255, 1537, 3071)]
    [InlineData(Av1BitDepth.EightBit, ObuMatrixCoefficients.YCgCoRo, 31, 65, 255)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.YCgCoRo, 127, 257, 1023)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.YCgCoRo, 511, 1025, 4095)]
    public void ReversibleYCgCoMatchesKnownPureRedValuesInBothDirections(
        int bitDepth,
        int matrixCoefficients,
        int expectedY,
        int expectedU,
        int expectedV)
    {
        // Assign
        // Pure red exercises positive odd Co and negative odd Cg. The expected samples come directly from
        // the H.273 integer lifting equations at the logical RGB precision selected by each matrix code point.
        using Image<Rgb48> source = new(1, 1);
        source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[0] = new Rgb48(ushort.MaxValue, 0, 0);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            1,
            1,
            matrixCoefficients: (ObuMatrixCoefficients)matrixCoefficients,
            bitDepth: (Av1BitDepth)bitDepth);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, source.Frames.RootFrame, frameBuffer);

        // Assert
        int actualY;
        int actualU;
        int actualV;
        if ((Av1BitDepth)bitDepth == Av1BitDepth.EightBit)
        {
            actualY = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(0)[0];
            actualU = frameBuffer.DeriveBlockPointer(Av1Plane.U, 0, 0).DangerousGetRowSpan(0)[0];
            actualV = frameBuffer.DeriveBlockPointer(Av1Plane.V, 0, 0).DangerousGetRowSpan(0)[0];
        }
        else
        {
            actualY = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, 0, 0, 0)[0];
            actualU = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, 0, 0, 0)[0];
            actualV = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.V, 0, 0, 0)[0];
        }

        Assert.Equal(expectedY, actualY);
        Assert.Equal(expectedU, actualU);
        Assert.Equal(expectedV, actualV);

        using Image<Rgb48> destination = new(1, 1);
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, destination.Frames.RootFrame);

        Assert.Equal(new Rgb48(ushort.MaxValue, 0, 0), destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[0]);
    }

    /// <summary>
    /// Verifies that limited-range reversible YCgCo applies range adjustment to RGB code values before lifting.
    /// </summary>
    /// <param name="bitDepth">The encoded AV1 bit depth.</param>
    /// <param name="matrixCoefficients">The reversible YCgCo variant.</param>
    /// <param name="expectedBlack">The expected black luma code value.</param>
    /// <param name="expectedWhite">The expected white luma code value.</param>
    [Theory]
    [InlineData(Av1BitDepth.EightBit, ObuMatrixCoefficients.YCgCoRe, 4, 59)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.YCgCoRe, 16, 235)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.YCgCoRe, 64, 940)]
    [InlineData(Av1BitDepth.EightBit, ObuMatrixCoefficients.YCgCoRo, 8, 118)]
    [InlineData(Av1BitDepth.TenBit, ObuMatrixCoefficients.YCgCoRo, 32, 470)]
    [InlineData(Av1BitDepth.TwelveBit, ObuMatrixCoefficients.YCgCoRo, 128, 1880)]
    public void ReversibleYCgCoAppliesLimitedRangeBeforeLifting(
        int bitDepth,
        int matrixCoefficients,
        int expectedBlack,
        int expectedWhite)
    {
        // Assign
        // Black and white have zero Cg and Co, exposing the RGB-domain range mapping without opponent-axis noise.
        using Image<Rgb48> source = new(2, 1);
        Span<Rgb48> sourcePixels = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        sourcePixels[0] = new Rgb48(0, 0, 0);
        sourcePixels[1] = new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            2,
            1,
            fullRange: false,
            matrixCoefficients: (ObuMatrixCoefficients)matrixCoefficients,
            bitDepth: (Av1BitDepth)bitDepth);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);

        // Act
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, source.Frames.RootFrame, frameBuffer);

        // Assert
        int expectedChromaBias = 1 << (((Av1BitDepth)bitDepth).GetBitCount() - 1);
        if ((Av1BitDepth)bitDepth == Av1BitDepth.EightBit)
        {
            Span<byte> y = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(0);
            Span<byte> u = frameBuffer.DeriveBlockPointer(Av1Plane.U, 0, 0).DangerousGetRowSpan(0);
            Span<byte> v = frameBuffer.DeriveBlockPointer(Av1Plane.V, 0, 0).DangerousGetRowSpan(0);
            Assert.Equal(expectedBlack, y[0]);
            Assert.Equal(expectedWhite, y[1]);
            Assert.Equal(expectedChromaBias, u[0]);
            Assert.Equal(expectedChromaBias, u[1]);
            Assert.Equal(expectedChromaBias, v[0]);
            Assert.Equal(expectedChromaBias, v[1]);
        }
        else
        {
            Span<ushort> y = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, 0, 0, 0);
            Span<ushort> u = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.U, 0, 0, 0);
            Span<ushort> v = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.V, 0, 0, 0);
            Assert.Equal(expectedBlack, y[0]);
            Assert.Equal(expectedWhite, y[1]);
            Assert.Equal(expectedChromaBias, u[0]);
            Assert.Equal(expectedChromaBias, u[1]);
            Assert.Equal(expectedChromaBias, v[0]);
            Assert.Equal(expectedChromaBias, v[1]);
        }

        using Image<Rgb48> destination = new(2, 1);
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, destination.Frames.RootFrame);

        Span<Rgb48> destinationPixels = destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        Assert.Equal(new Rgb48(0, 0, 0), destinationPixels[0]);
        Assert.Equal(new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue), destinationPixels[1]);
    }

    /// <summary>
    /// Verifies that reversible YCgCo rejects chroma subsampling in both conversion directions.
    /// </summary>
    /// <param name="matrixCoefficients">The reversible YCgCo variant.</param>
    [Theory]
    [InlineData(ObuMatrixCoefficients.YCgCoRe)]
    [InlineData(ObuMatrixCoefficients.YCgCoRo)]
    public void ReversibleYCgCoRequiresFullChroma(int matrixCoefficients)
    {
        // Assign
        using Image<Rgb24> image = new(2, 2);
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            2,
            2,
            matrixCoefficients: (ObuMatrixCoefficients)matrixCoefficients,
            colorFormat: Av1ColorFormat.Yuv420);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv420, false);

        // Act and assert
        Assert.Throws<InvalidImageContentException>(
            () => Av1YuvConverter.ConvertFromRgb(Configuration.Default, image.Frames.RootFrame, frameBuffer));

        Assert.Throws<InvalidImageContentException>(
            () => Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, image.Frames.RootFrame));
    }

    /// <summary>
    /// Verifies that every H.273 operator produces the same result in SIMD batches and the scalar row tail.
    /// </summary>
    /// <param name="matrixCoefficients">The matrix coefficients selecting the color operator.</param>
    /// <param name="transferCharacteristics">The transfer characteristics used by nonlinear operators.</param>
    [Theory]
    [InlineData(ObuMatrixCoefficients.Bt709, ObuTransferCharacteristics.Bt709)]
    [InlineData(ObuMatrixCoefficients.Identity, ObuTransferCharacteristics.Bt709)]
    [InlineData(ObuMatrixCoefficients.SmpteYCgCo, ObuTransferCharacteristics.Bt709)]
    [InlineData(ObuMatrixCoefficients.Bt2020ConstantLuminance, ObuTransferCharacteristics.Bt202010Bit)]
    [InlineData(ObuMatrixCoefficients.Smpte2085, ObuTransferCharacteristics.Bt709)]
    [InlineData(ObuMatrixCoefficients.ChromaticityDerivedNonConstantLuminance, ObuTransferCharacteristics.Bt709)]
    [InlineData(ObuMatrixCoefficients.ChromaticityDerivedConstantLuminance, ObuTransferCharacteristics.Bt709)]
    [InlineData(ObuMatrixCoefficients.Bt2100ICtCp, ObuTransferCharacteristics.Smpte2084)]
    [InlineData(ObuMatrixCoefficients.Bt2100ICtCp, ObuTransferCharacteristics.Hlg)]
    [InlineData(ObuMatrixCoefficients.IptC2, ObuTransferCharacteristics.Bt709)]
    [InlineData(ObuMatrixCoefficients.YCgCoRe, ObuTransferCharacteristics.Bt709)]
    [InlineData(ObuMatrixCoefficients.YCgCoRo, ObuTransferCharacteristics.Bt709)]
    public void ColorOperatorSimdBatchesMatchScalarTail(int matrixCoefficients, int transferCharacteristics)
    {
        const int width = 31;

        // Thirty-one samples exercise Vector512, Vector256, Vector128, and scalar stages on AVX-512 hardware.
        // The same row still reaches the widest available stages and scalar tail on narrower SIMD hardware.
        using Image<Rgb48> source = new(width, 1);
        source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0).Fill(new Rgb48(39999, 27777, 12345));
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            width,
            1,
            matrixCoefficients: (ObuMatrixCoefficients)matrixCoefficients,
            bitDepth: Av1BitDepth.TwelveBit,
            transferCharacteristics: (ObuTransferCharacteristics)transferCharacteristics,
            colorPrimaries: ObuColorPrimaries.Bt2020);

        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        using Image<Rgb48> destination = new(width, 1);

        Av1YuvConverter.ConvertFromRgb(Configuration.Default, source.Frames.RootFrame, frameBuffer);
        Av1YuvConverter.ConvertToRgb(Configuration.Default, frameBuffer, destination.Frames.RootFrame);

        AssertPlaneContainsRepeatedSample(frameBuffer, Av1Plane.Y, 0, 0);
        AssertPlaneContainsRepeatedSample(frameBuffer, Av1Plane.U, 0, 0);
        AssertPlaneContainsRepeatedSample(frameBuffer, Av1Plane.V, 0, 0);

        Span<Rgb48> pixels = destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
        for (int x = 1; x < pixels.Length; x++)
        {
            Assert.Equal(pixels[0], pixels[x]);
        }
    }

    /// <summary>
    /// Verifies horizontal and vertical chroma downsampling against full-resolution encoded components.
    /// </summary>
    /// <param name="colorFormat">The subsampled AV1 color format.</param>
    /// <param name="bitDepth">The encoded AV1 bit depth.</param>
    [Theory]
    [InlineData(Av1ColorFormat.Yuv422, Av1BitDepth.EightBit)]
    [InlineData(Av1ColorFormat.Yuv422, Av1BitDepth.TwelveBit)]
    [InlineData(Av1ColorFormat.Yuv420, Av1BitDepth.EightBit)]
    [InlineData(Av1ColorFormat.Yuv420, Av1BitDepth.TwelveBit)]
    public void RgbToYuvSubsamplingAveragesFullResolutionChroma(int colorFormat, int bitDepth)
    {
        const int width = 35;
        const int height = 3;

        using Image<Rgb48> source = new(width, height);
        for (int y = 0; y < height; y++)
        {
            Span<Rgb48> row = source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                row[x] = new Rgb48(
                    (ushort)((x * 1879) + (y * 791)),
                    (ushort)((x * 977) + (y * 3251)),
                    (ushort)((x * 613) + (y * 4987)));
            }
        }

        ObuSequenceHeader fullResolutionHeader = CreateSequenceHeader(
            width,
            height,
            colorFormat: Av1ColorFormat.Yuv444,
            bitDepth: (Av1BitDepth)bitDepth);

        ObuSequenceHeader subsampledHeader = CreateSequenceHeader(
            width,
            height,
            colorFormat: (Av1ColorFormat)colorFormat,
            bitDepth: (Av1BitDepth)bitDepth);

        using Av1FrameBuffer<byte> fullResolution = new(Configuration.Default, fullResolutionHeader, Av1ColorFormat.Yuv444, false);
        using Av1FrameBuffer<byte> subsampled = new(Configuration.Default, subsampledHeader, (Av1ColorFormat)colorFormat, false);

        Av1YuvConverter.ConvertFromRgb(Configuration.Default, source.Frames.RootFrame, fullResolution);
        Av1YuvConverter.ConvertFromRgb(Configuration.Default, source.Frames.RootFrame, subsampled);

        AssertSubsampledPlaneMatchesAverage(fullResolution, subsampled, Av1Plane.U);
        AssertSubsampledPlaneMatchesAverage(fullResolution, subsampled, Av1Plane.V);
    }

    /// <summary>
    /// Verifies an image-wide RGB-to-YUV-to-RGB conversion against the configured similarity tolerance.
    /// </summary>
    /// <param name="provider">The source test-image provider.</param>
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

    /// <summary>
    /// Verifies that same-sized AV1 alpha composition preserves color and maps full-range luma exactly with and
    /// without hardware intrinsics.
    /// </summary>
    [Fact]
    public void ComposeAlphaMapsEightBitLumaExactlyAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateEightBitAlphaComposition,
            AlphaConfigurations);

    /// <summary>
    /// Verifies that scaled 10-bit and 12-bit AV1 alpha composition matches ImageSharp's established box resampler
    /// with and without hardware intrinsics.
    /// </summary>
    [Fact]
    public void ComposeAlphaScalesHighBitDepthLumaAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateHighBitDepthAlphaScaling,
            AlphaConfigurations);

    /// <summary>
    /// Verifies that alpha scaling remains exact when the bounded working buffer must advance through multiple
    /// source-row windows.
    /// </summary>
    [Fact]
    public void ComposeAlphaScalesAcrossMultipleWorkingWindows()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateSlidingWindowAlphaScaling,
            AlphaConfigurations);

    /// <summary>
    /// Verifies exact limited-range endpoints and out-of-range clamping for every supported AV1 alpha bit depth.
    /// </summary>
    [Fact]
    public void ComposeAlphaExpandsLimitedRangeAcrossIntrinsicWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateLimitedRangeAlphaComposition,
            AlphaConfigurations);

    /// <summary>
    /// Exercises direct full-range byte alpha composition against exact code-value expansion.
    /// </summary>
    private static void ValidateEightBitAlphaComposition()
    {
        const int width = 19;
        const int height = 5;

        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(width, height, colorFormat: Av1ColorFormat.Yuv400);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false);
        using Image<Rgba64> destination = new(width, height);
        Buffer2DRegion<byte> luma = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        for (int y = 0; y < height; y++)
        {
            Span<byte> sourceRow = luma.DangerousGetRowSpan(y);
            Span<Rgba64> destinationRow = destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                sourceRow[x] = (byte)((x * 11) + (y * 7));
                destinationRow[x] = new Rgba64((ushort)(1000 + x), (ushort)(2000 + y), 3000, ushort.MaxValue);
            }
        }

        Av1YuvConverter.ComposeAlpha(
            Configuration.Default,
            frameBuffer,
            destination.Frames.RootFrame,
            destination.Size,
            destination.Bounds,
            false);

        for (int y = 0; y < height; y++)
        {
            Span<byte> sourceRow = luma.DangerousGetRowSpan(y);
            Span<Rgba64> actualRow = destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                Assert.Equal((ushort)(1000 + x), actualRow[x].R);
                Assert.Equal((ushort)(2000 + y), actualRow[x].G);
                Assert.Equal((ushort)3000, actualRow[x].B);
                Assert.Equal((ushort)(sourceRow[x] * 257), actualRow[x].A);
            }
        }
    }

    /// <summary>
    /// Exercises the direct box-resize path for both supported high-bit-depth sample layouts.
    /// </summary>
    private static void ValidateHighBitDepthAlphaScaling()
    {
        const int sourceWidth = 5;
        const int sourceHeight = 3;
        const int destinationWidth = 9;
        const int destinationHeight = 7;

        foreach (Av1BitDepth bitDepth in new[] { Av1BitDepth.TenBit, Av1BitDepth.TwelveBit })
        {
            ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
                sourceWidth,
                sourceHeight,
                colorFormat: Av1ColorFormat.Yuv400,
                bitDepth: bitDepth);

            using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false);
            using Image<L16> expected = new(sourceWidth, sourceHeight);
            using Image<Rgba64> destination = new(destinationWidth, destinationHeight, new Rgba64(1000, 2000, 3000, ushort.MaxValue));
            ushort maximum = (ushort)((1 << bitDepth.GetBitCount()) - 1);
            for (int y = 0; y < sourceHeight; y++)
            {
                Span<ushort> sourceRow = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, y, 0, 0);
                Span<L16> expectedRow = expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                for (int x = 0; x < sourceWidth; x++)
                {
                    sourceRow[x] = (ushort)(((x * 223) + (y * 151)) & maximum);
                    expectedRow[x] = L16.FromScaledVector4(new Vector4((float)sourceRow[x] / maximum));
                }
            }

            expected.Mutate(context => context.Resize(destinationWidth, destinationHeight, KnownResamplers.Box));
            Av1YuvConverter.ComposeAlpha(
                Configuration.Default,
                frameBuffer,
                destination.Frames.RootFrame,
                destination.Size,
                destination.Bounds,
                false);

            for (int y = 0; y < destinationHeight; y++)
            {
                Span<L16> expectedRow = expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                Span<Rgba64> actualRow = destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                for (int x = 0; x < destinationWidth; x++)
                {
                    Assert.Equal((ushort)1000, actualRow[x].R);
                    Assert.Equal((ushort)2000, actualRow[x].G);
                    Assert.Equal((ushort)3000, actualRow[x].B);
                    Assert.Equal(expectedRow[x].PackedValue, actualRow[x].A);
                }
            }
        }
    }

    /// <summary>
    /// Exercises overlapping box kernels across multiple transposed source-row windows.
    /// </summary>
    private static void ValidateSlidingWindowAlphaScaling()
    {
        const int sourceWidth = 13;
        const int sourceHeight = 41;
        const int destinationWidth = 23;
        const int destinationHeight = 17;

        Configuration configuration = Configuration.CreateDefaultInstance();
        configuration.WorkingBufferSizeHintInBytes = 1;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
            sourceWidth,
            sourceHeight,
            colorFormat: Av1ColorFormat.Yuv400,
            bitDepth: Av1BitDepth.TwelveBit);

        using Av1FrameBuffer<byte> frameBuffer = new(configuration, sequenceHeader, Av1ColorFormat.Yuv400, false);
        using Image<L16> expected = new(configuration, sourceWidth, sourceHeight);
        using Image<Rgba64> destination = new(configuration, destinationWidth, destinationHeight, new Rgba64(1000, 2000, 3000, ushort.MaxValue));
        for (int y = 0; y < sourceHeight; y++)
        {
            Span<ushort> sourceRow = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, y, 0, 0);
            Span<L16> expectedRow = expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < sourceWidth; x++)
            {
                sourceRow[x] = (ushort)(((x * 277) + (y * 193)) & 4095);
                expectedRow[x] = L16.FromScaledVector4(new Vector4(sourceRow[x] / 4095F));
            }
        }

        expected.Mutate(context => context.Resize(destinationWidth, destinationHeight, KnownResamplers.Box));
        Av1YuvConverter.ComposeAlpha(
            configuration,
            frameBuffer,
            destination.Frames.RootFrame,
            destination.Size,
            destination.Bounds,
            false);

        for (int y = 0; y < destinationHeight; y++)
        {
            Span<L16> expectedRow = expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            Span<Rgba64> actualRow = destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            for (int x = 0; x < destinationWidth; x++)
            {
                Assert.Equal((ushort)1000, actualRow[x].R);
                Assert.Equal((ushort)2000, actualRow[x].G);
                Assert.Equal((ushort)3000, actualRow[x].B);
                Assert.Equal(expectedRow[x].PackedValue, actualRow[x].A);
            }
        }
    }

    /// <summary>
    /// Exercises luma-range expansion and clamping for 8-bit, 10-bit, and 12-bit alpha samples.
    /// </summary>
    private static void ValidateLimitedRangeAlphaComposition()
    {
        foreach (Av1BitDepth bitDepth in new[] { Av1BitDepth.EightBit, Av1BitDepth.TenBit, Av1BitDepth.TwelveBit })
        {
            int bitCount = bitDepth.GetBitCount();
            ushort minimum = (ushort)(16 << (bitCount - 8));
            ushort maximum = (ushort)(235 << (bitCount - 8));
            ushort storageMaximum = (ushort)((1 << bitCount) - 1);
            ObuSequenceHeader sequenceHeader = CreateSequenceHeader(
                4,
                1,
                fullRange: false,
                colorFormat: Av1ColorFormat.Yuv400,
                bitDepth: bitDepth);

            using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv400, false);
            using Image<Rgba64> destination = new(4, 1, new Rgba64(1000, 2000, 3000, ushort.MaxValue));
            if (bitDepth == Av1BitDepth.EightBit)
            {
                Span<byte> luma = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).DangerousGetRowSpan(0);
                luma[0] = 0;
                luma[1] = (byte)minimum;
                luma[2] = (byte)maximum;
                luma[3] = byte.MaxValue;
            }
            else
            {
                Span<ushort> luma = frameBuffer.GetHighBitDepthRowSpan(Av1Plane.Y, 0, 0, 0);
                luma[0] = 0;
                luma[1] = minimum;
                luma[2] = maximum;
                luma[3] = storageMaximum;
            }

            Av1YuvConverter.ComposeAlpha(
                Configuration.Default,
                frameBuffer,
                destination.Frames.RootFrame,
                destination.Size,
                destination.Bounds,
                false);

            Span<Rgba64> actual = destination.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0);
            Assert.Equal((ushort)0, actual[0].A);
            Assert.Equal((ushort)0, actual[1].A);
            Assert.Equal(ushort.MaxValue, actual[2].A);
            Assert.Equal(ushort.MaxValue, actual[3].A);
        }
    }

    /// <summary>
    /// Creates a sequence header containing the color signaling required by a conversion test.
    /// </summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="fullRange">Whether encoded samples use the full range.</param>
    /// <param name="matrixCoefficients">The matrix coefficients used for conversion.</param>
    /// <param name="colorFormat">The encoded plane layout.</param>
    /// <param name="chromaSamplePosition">The signaled chroma sample position.</param>
    /// <param name="bitDepth">The encoded sample bit depth.</param>
    /// <param name="transferCharacteristics">The transfer characteristics used by nonlinear matrices.</param>
    /// <param name="colorPrimaries">The color primaries used by derived matrices.</param>
    /// <returns>The configured sequence header.</returns>
    private static ObuSequenceHeader CreateSequenceHeader(
        int width,
        int height,
        bool fullRange = true,
        ObuMatrixCoefficients matrixCoefficients = ObuMatrixCoefficients.Bt709,
        Av1ColorFormat colorFormat = Av1ColorFormat.Yuv444,
        ObuChromoSamplePosition chromaSamplePosition = ObuChromoSamplePosition.Unknown,
        Av1BitDepth bitDepth = Av1BitDepth.EightBit,
        ObuTransferCharacteristics transferCharacteristics = ObuTransferCharacteristics.Bt709,
        ObuColorPrimaries colorPrimaries = ObuColorPrimaries.Bt709)
        => new()
        {
            MaxFrameWidth = width,
            MaxFrameHeight = height,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
                BitDepth = bitDepth,
                MatrixCoefficients = matrixCoefficients,
                TransferCharacteristics = transferCharacteristics,
                ColorPrimaries = colorPrimaries,
                ColorRange = fullRange,
                SubSamplingX = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422,
                SubSamplingY = colorFormat is Av1ColorFormat.Yuv400 or Av1ColorFormat.Yuv420,
                ChromaSamplePosition = chromaSamplePosition,
            },
        };

    /// <summary>
    /// Verifies that every high-bit-depth sample in a plane matches its first sample.
    /// </summary>
    /// <param name="frameBuffer">The encoded frame buffer.</param>
    /// <param name="plane">The plane to inspect.</param>
    /// <param name="subX">The horizontal subsampling shift.</param>
    /// <param name="subY">The vertical subsampling shift.</param>
    private static void AssertPlaneContainsRepeatedSample(Av1FrameBuffer<byte> frameBuffer, Av1Plane plane, int subX, int subY)
    {
        Span<ushort> samples = frameBuffer.GetHighBitDepthRowSpan(plane, 0, subX, subY);
        for (int x = 1; x < samples.Length; x++)
        {
            Assert.Equal(samples[0], samples[x]);
        }
    }

    /// <summary>
    /// Verifies that a subsampled plane contains the rounded mean of the corresponding full-resolution samples.
    /// </summary>
    /// <param name="fullResolution">The full-resolution encoded frame.</param>
    /// <param name="subsampled">The subsampled encoded frame.</param>
    /// <param name="plane">The chroma plane to compare.</param>
    private static void AssertSubsampledPlaneMatchesAverage(
        Av1FrameBuffer<byte> fullResolution,
        Av1FrameBuffer<byte> subsampled,
        Av1Plane plane)
    {
        int subY = subsampled.ColorConfig.SubSamplingY ? 1 : 0;
        int chromaHeight = (subsampled.Height + subY) >> subY;
        int chromaWidth = (subsampled.Width + 1) >> 1;
        for (int y = 0; y < chromaHeight; y++)
        {
            int sourceY = y << subY;
            int rowCount = subY == 0 ? 1 : Math.Min(2, fullResolution.Height - sourceY);
            for (int x = 0; x < chromaWidth; x++)
            {
                int sourceX = x << 1;
                int columnCount = Math.Min(2, fullResolution.Width - sourceX);
                int sum = 0;
                for (int row = 0; row < rowCount; row++)
                {
                    for (int column = 0; column < columnCount; column++)
                    {
                        sum += GetPlaneSample(fullResolution, plane, sourceX + column, sourceY + row, 0, 0);
                    }
                }

                int expected = (int)MathF.Round((float)sum / (rowCount * columnCount), MidpointRounding.AwayFromZero);
                int actual = GetPlaneSample(subsampled, plane, x, y, 1, subY);
                Assert.True(
                    actual >= expected - 1 && actual <= expected + 1,
                    $"Plane {plane}, sample ({x}, {y}): expected {expected} +/- 1 from sum {sum} over {rowCount * columnCount} samples but found {actual}.");
            }
        }
    }

    /// <summary>
    /// Gets one encoded sample from an eight-bit or high-bit-depth frame plane.
    /// </summary>
    /// <param name="frameBuffer">The encoded frame buffer.</param>
    /// <param name="plane">The plane containing the sample.</param>
    /// <param name="x">The horizontal sample coordinate.</param>
    /// <param name="y">The vertical sample coordinate.</param>
    /// <param name="subX">The horizontal subsampling shift.</param>
    /// <param name="subY">The vertical subsampling shift.</param>
    /// <returns>The encoded sample value.</returns>
    private static int GetPlaneSample(Av1FrameBuffer<byte> frameBuffer, Av1Plane plane, int x, int y, int subX, int subY)
        => frameBuffer.BitDepth == Av1BitDepth.EightBit
            ? frameBuffer.DeriveBlockPointer(plane, subX, subY).DangerousGetRowSpan(y)[x]
            : frameBuffer.GetHighBitDepthRowSpan(plane, y, subX, subY)[x];
}
