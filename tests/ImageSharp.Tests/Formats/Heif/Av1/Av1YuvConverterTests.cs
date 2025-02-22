// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Iced.Intel;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1YuvConverterTests
{
    [Theory]
    [InlineData(255, 255, 255)]
    [InlineData(0, 0, 0)]
    [InlineData(42, 42, 42)]
    [InlineData(42, 0, 0)]
    [InlineData(42, 42, 0)]
    [InlineData(42, 0, 42)]
    [InlineData(0, 42, 42)]
    [InlineData(0, 0, 42)]
    [InlineData(50, 100, 150)]
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
