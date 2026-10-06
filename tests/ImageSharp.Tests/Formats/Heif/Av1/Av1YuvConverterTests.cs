// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 color conversion, sample-range handling, alpha composition, and presentation scaling.
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
    /// Verifies known RGB and YUV values in both conversion directions across coefficient, identity, and YCgCo
    /// matrices and both sample ranges.
    /// </summary>
    [Fact]
    public void ConvertSinglePixelMatchesKnownValues()
    {
        ConvertSinglePixelCase(150, 100, 50, 107, 97, 155, true, ObuMatrixCoefficients.Bt709);
        ConvertSinglePixelCase(150, 100, 50, 100, 50, 150, true, ObuMatrixCoefficients.Identity);
        ConvertSinglePixelCase(150, 100, 50, 100, 128, 178, true, ObuMatrixCoefficients.SmpteYCgCo);
        ConvertSinglePixelCase(150, 100, 50, 110, 96, 155, true, ObuMatrixCoefficients.Bt2020NonConstantLuminance);
        ConvertSinglePixelCase(255, 255, 255, 235, 128, 128, false, ObuMatrixCoefficients.Bt709);
        ConvertSinglePixelCase(150, 100, 50, 108, 101, 152, false, ObuMatrixCoefficients.Bt709);
    }

    private static void ConvertSinglePixelCase(byte r, byte g, byte b, int y, int u, int v, bool fullRange, ObuMatrixCoefficients matrixCoefficients)
    {
        RgbToYuvSinglePixelCase(r, g, b, y, u, v, fullRange, (int)matrixCoefficients);
        YuvToRgbSinglePixelCase(r, g, b, y, u, v, fullRange, (int)matrixCoefficients);
    }

    private static void RgbToYuvSinglePixelCase(byte r, byte g, byte b, int y, int u, int v, bool fullRange, int matrixCoefficients)
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
        byte actualY = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).GetRowSpan(0)[0];
        byte actualU = frameBuffer.DeriveBlockPointer(Av1Plane.U, 0, 0).GetRowSpan(0)[0];
        byte actualV = frameBuffer.DeriveBlockPointer(Av1Plane.V, 0, 0).GetRowSpan(0)[0];
        Assert.Equal(y, actualY);
        Assert.Equal(u, actualU);
        Assert.Equal(v, actualV);
    }

    private static void YuvToRgbSinglePixelCase(byte r, byte g, byte b, int y, int u, int v, bool fullRange, int matrixCoefficients)
    {
        // Assign
        using Image<Rgb24> image = new(1, 1);
        ImageFrame<Rgb24> frame = image.Frames.RootFrame;
        ObuSequenceHeader sequenceHeader = CreateSequenceHeader(1, 1, fullRange, (ObuMatrixCoefficients)matrixCoefficients);
        using Av1FrameBuffer<byte> frameBuffer = new(Configuration.Default, sequenceHeader, Av1ColorFormat.Yuv444, false);
        frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).GetRowSpan(0)[0] = (byte)y;
        frameBuffer.DeriveBlockPointer(Av1Plane.U, 0, 0).GetRowSpan(0)[0] = (byte)u;
        frameBuffer.DeriveBlockPointer(Av1Plane.V, 0, 0).GetRowSpan(0)[0] = (byte)v;

        // Act
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            frameBuffer,
            new Rectangle(Point.Empty, frame.Size),
            frame.PixelBuffer.GetRegion(),
            frame.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            frameBuffer.ColorConfig.ColorRange);

        // Assert
        frame.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> memory);
        Rgb24 actual = memory.Span[0];
        Assert.Equal(r, actual.R, 1d);
        Assert.Equal(g, actual.G, 1d);
        Assert.Equal(b, actual.B, 1d);
    }

    [Fact]
    public void YuvToRgbWritesOnlyRequestedDestinationRegion()
    {
        Rgb24 sentinel = new(201, 202, 203);
        using Image<Rgb24> image = new(7, 5, sentinel);
        ObuSequenceHeader header = CreateSequenceHeader(4, 3, true, ObuMatrixCoefficients.Identity);
        using Av1FrameBuffer<byte> buffer = new(Configuration.Default, header, Av1ColorFormat.Yuv444, false);
        for (int y = 0; y < 3; y++)
        {
            Span<byte> green = buffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).GetRowSpan(y);
            Span<byte> blue = buffer.DeriveBlockPointer(Av1Plane.U, 0, 0).GetRowSpan(y);
            Span<byte> red = buffer.DeriveBlockPointer(Av1Plane.V, 0, 0).GetRowSpan(y);
            for (int x = 0; x < 4; x++)
            {
                red[x] = (byte)(10 + x);
                green[x] = (byte)(20 + y);
                blue[x] = (byte)(30 + x + (y * 4));
            }
        }

        Rectangle source = new(1, 1, 2, 2);
        Rectangle target = new(3, 2, 2, 2);
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            buffer,
            source,
            image.Frames.RootFrame.PixelBuffer.GetRegion(target),
            new Size(4, 3),
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            buffer.ColorConfig.ColorRange);

        // The identity matrix gives independently known RGB values. Every pixel outside the target must retain
        // the sentinel, including the row prefix and suffix surrounding the converted source crop.
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int sourceX = x - target.X + source.X;
                int sourceY = y - target.Y + source.Y;
                Rgb24 expected = target.Contains(x, y)
                    ? new Rgb24((byte)(10 + sourceX), (byte)(20 + sourceY), (byte)(30 + sourceX + (sourceY * 4)))
                    : sentinel;

                Assert.Equal(expected, image[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies libyuv's native two-times presentation filter at byte and twelve-bit precision across every
    /// available intrinsic width and the scalar fallback.
    /// </summary>
    [Fact]
    public void ScaleSelectedSpatialLayerMatchesReference()
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
                    frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).GetRowSpan(y));
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
        Span<byte> yRow = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).GetRowSpan(0);
        Span<byte> uRow = frameBuffer.DeriveBlockPointer(Av1Plane.U, 0, 0).GetRowSpan(0);
        Span<byte> vRow = frameBuffer.DeriveBlockPointer(Av1Plane.V, 0, 0).GetRowSpan(0);
        for (int i = 0; i < frameBuffer.Width; i++)
        {
            Rgb24 pixel = default;
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
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            frameBuffer,
            new Rectangle(Point.Empty, frame.Size),
            frame.PixelBuffer.GetRegion(),
            frame.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            frameBuffer.ColorConfig.ColorRange);

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
    private static void Compare(ReadOnlySpan<Rgb24> referenceOutput, ReadOnlySpan<Rgb24> actual, int allowedDifference)
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
        Av1PlaneRegion<byte> region = frameBuffer.DeriveBlockPointer(plane, 0, 0);
        for (int y = 0; y < region.Height; y++)
        {
            CreateTestData(rnd, region.GetRowSpan(y), bitCount);
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
    /// Verifies 10-bit and 12-bit round trips for coefficient, IPT-C2, and reversible YCgCo matrices.
    /// </summary>
    [Fact]
    public void HighBitDepthRoundTrip()
    {
        HighBitDepthRoundTripCase((int)Av1BitDepth.TenBit, (int)ObuMatrixCoefficients.Bt709);
        HighBitDepthRoundTripCase((int)Av1BitDepth.TenBit, (int)ObuMatrixCoefficients.YCgCoRe);
        HighBitDepthRoundTripCase((int)Av1BitDepth.TwelveBit, (int)ObuMatrixCoefficients.IptC2);
        HighBitDepthRoundTripCase((int)Av1BitDepth.TwelveBit, (int)ObuMatrixCoefficients.YCgCoRo);
    }

    private static void HighBitDepthRoundTripCase(int bitDepth, int matrixCoefficients)
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
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            frameBuffer,
            new Rectangle(Point.Empty, actual.Frames.RootFrame.Size),
            actual.Frames.RootFrame.PixelBuffer.GetRegion(),
            actual.Frames.RootFrame.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            frameBuffer.ColorConfig.ColorRange);

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
    /// Verifies that representative H.273 operator families produce the same result in SIMD batches and the scalar
    /// row tail.
    /// </summary>
    [Fact]
    public void ColorOperatorSimdBatchesMatchScalarTail()
    {
        ColorOperatorSimdBatchesMatchScalarTailCase((int)ObuMatrixCoefficients.Bt709, (int)ObuTransferCharacteristics.Bt709);
        ColorOperatorSimdBatchesMatchScalarTailCase((int)ObuMatrixCoefficients.Identity, (int)ObuTransferCharacteristics.Bt709);
        ColorOperatorSimdBatchesMatchScalarTailCase((int)ObuMatrixCoefficients.Bt2020ConstantLuminance, (int)ObuTransferCharacteristics.Bt202010Bit);
        ColorOperatorSimdBatchesMatchScalarTailCase((int)ObuMatrixCoefficients.ChromaticityDerivedConstantLuminance, (int)ObuTransferCharacteristics.Bt709);
        ColorOperatorSimdBatchesMatchScalarTailCase((int)ObuMatrixCoefficients.Bt2100ICtCp, (int)ObuTransferCharacteristics.Smpte2084);
        ColorOperatorSimdBatchesMatchScalarTailCase((int)ObuMatrixCoefficients.IptC2, (int)ObuTransferCharacteristics.Bt709);
        ColorOperatorSimdBatchesMatchScalarTailCase((int)ObuMatrixCoefficients.YCgCoRe, (int)ObuTransferCharacteristics.Bt709);
    }

    private static void ColorOperatorSimdBatchesMatchScalarTailCase(int matrixCoefficients, int transferCharacteristics)
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
        Av1YuvConverter.ConvertToRgb(
            Configuration.Default,
            frameBuffer,
            new Rectangle(Point.Empty, destination.Frames.RootFrame.Size),
            destination.Frames.RootFrame.PixelBuffer.GetRegion(),
            destination.Frames.RootFrame.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            frameBuffer.ColorConfig.ColorRange);

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
    /// Verifies exact and box-scaled AV1 alpha composition, bounded working windows, and limited-range expansion
    /// with and without hardware intrinsics.
    /// </summary>
    [Fact]
    public void ComposeAlphaMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateAlphaComposition,
            AlphaConfigurations);

    /// <summary>
    /// Runs every alpha-composition check inside one hardware-intrinsics configuration.
    /// </summary>
    private static void ValidateAlphaComposition()
    {
        ValidateEightBitAlphaComposition();
        ValidateHighBitDepthAlphaScaling();
        ValidateSlidingWindowAlphaScaling();
        ValidateLimitedRangeAlphaComposition();
    }

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
        Av1PlaneRegion<byte> luma = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0);
        for (int y = 0; y < height; y++)
        {
            Span<byte> sourceRow = luma.GetRowSpan(y);
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
            destination.Frames.RootFrame.PixelBuffer.GetRegion(destination.Frames.RootFrame.Bounds),
            destination.Size,
            destination.Bounds,
            false,
            default);

        for (int y = 0; y < height; y++)
        {
            Span<byte> sourceRow = luma.GetRowSpan(y);
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
                destination.Frames.RootFrame.PixelBuffer.GetRegion(destination.Frames.RootFrame.Bounds),
                destination.Size,
                destination.Bounds,
                false,
                default);

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
            destination.Frames.RootFrame.PixelBuffer.GetRegion(destination.Frames.RootFrame.Bounds),
            destination.Size,
            destination.Bounds,
            false,
            default);

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
                Span<byte> luma = frameBuffer.DeriveBlockPointer(Av1Plane.Y, 0, 0).GetRowSpan(0);
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
                destination.Frames.RootFrame.PixelBuffer.GetRegion(destination.Frames.RootFrame.Bounds),
                destination.Size,
                destination.Bounds,
                false,
                default);

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
}
