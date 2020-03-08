using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Quantization
{
    public class QuantizerExperiments
    {
        public static readonly TheoryData<int, string> TestData = new TheoryData<int, string>()
        {
            { 255, nameof(KnownDitherings.FloydSteinberg) },
            { 128, nameof(KnownDitherings.FloydSteinberg) },
            { 32, nameof(KnownDitherings.FloydSteinberg) },
            { 32, nameof(KnownDitherings.Bayer4x4) },
        };

        private readonly ITestOutputHelper output;

        public QuantizerExperiments(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [WithFile(TestImages.Png.Bike, nameof(TestData), PixelTypes.Rgb24)]
        [WithFile(TestImages.Png.BikeGrayscale, nameof(TestData), PixelTypes.Rgb24)]
        [WithFile(TestImages.Png.CalliphoraPartial, nameof(TestData), PixelTypes.Rgb24)]
        [WithFile(TestImages.Png.Ducky, nameof(TestData), PixelTypes.Rgb24)]
        [WithFile(TestImages.Png.Rgb48Bpp, nameof(TestData), PixelTypes.Rgba32)]
        [WithFile(TestImages.Gif.Giphy, nameof(TestData), PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Bike, nameof(TestData), PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.Blur, nameof(TestData), PixelTypes.Rgba32)]
        [WithFile(TestImages.Png.CalliphoraPartial, nameof(TestData), PixelTypes.Rgba32)]
        public void StressOctreeQuantizer<TPixel>(TestImageProvider<TPixel> provider, int maxColors, string ditherName)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSave(provider, new
            {
                T = "ORIGINAL"
            });

            using var q = new OctreeFrameQuantizer<TPixel>(Configuration.Default, new QuantizerOptions()
            {
                MaxColors = maxColors,
                Dither = TestUtils.GetDither(ditherName),
            });
            q.BuildPalette(image.Frames.RootFrame, image.Bounds());
            using (QuantizedFrame<TPixel> qq = q.QuantizeFrame(image.Frames.RootFrame, image.Bounds()))
            {
                QuantizeImage(image, qq);
            }

            image.DebugSave(provider, new
            {
                T = "QUANTIZED",
                MaxColors = maxColors,
                Dither = ditherName
            });
        }

        private static void QuantizeImage<TPixel>(Image<TPixel> image, QuantizedFrame<TPixel> map)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ReadOnlySpan<TPixel> palette = map.Palette.Span;
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> imageRow = image.GetPixelRowSpan(y);
                ReadOnlySpan<byte> qRow = map.GetRowSpan(y);
                for (int x = 0; x < imageRow.Length; x++)
                {
                    imageRow[x] = palette[qRow[x]];
                }
            }
        }
    }
}
