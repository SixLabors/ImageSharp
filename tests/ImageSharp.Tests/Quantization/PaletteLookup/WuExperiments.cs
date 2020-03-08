using System;
using System.Linq;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Processing.Processors.Quantization.PaletteLookup;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1515

namespace SixLabors.ImageSharp.Tests.Quantization.PaletteLookup
{
    public class WuExperiments
    {
        public static readonly TheoryData<int, string> TestData = new TheoryData<int, string>()
        {
            { 255, nameof(KnownDitherings.FloydSteinberg) },
            { 128, nameof(KnownDitherings.FloydSteinberg) },
            { 32, nameof(KnownDitherings.FloydSteinberg) },
            { 32, nameof(KnownDitherings.Bayer4x4) },
        };

        private readonly ITestOutputHelper output;

        public WuExperiments(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [WithFile(TestImages.Png.Bike, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.BikeGrayscale, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.CalliphoraPartial, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.Ducky, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.Rgb48Bpp, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Gif.Giphy, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Png.Bike, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Png.Blur, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Png.CalliphoraPartial, nameof(TestData), PixelTypes.Rgba32)]
        public void OwnPalette<TPixel>(TestImageProvider<TPixel> provider, int maxColors)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSave(provider, new
            {
                T = "ORIGINAL"
            });

            using var map = new WuPaletteMap<TPixel>(Configuration.Default, maxColors)
                {
                    DebugLog = msg => this.output.WriteLine(msg)
                };

            ReadOnlySpan<TPixel> palette = map.BuildPalette(image.Frames.RootFrame, image.Bounds());
            QuantizeImage(image, map);

            image.DebugSave(provider, new
            {
                T = "QUANTIZED",
                MaxColors = maxColors
            });
        }

        [Theory]
        [WithFile(TestImages.Png.Bike, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.BikeGrayscale, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.CalliphoraPartial, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.Ducky, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Bmp.F, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.Rgb48Bpp, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Gif.Giphy, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Png.Bike, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Png.Blur, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Png.CalliphoraPartial, nameof(TestData), PixelTypes.Rgba32)]
        public void OctreePalette<TPixel>(TestImageProvider<TPixel> provider, int maxColors)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSave(provider, new
            {
                T = "ORIGINAL"
            });

            using var map = new WuPaletteMap<TPixel>(Configuration.Default, maxColors);

            using var octreeQuantizer = new OctreeFrameQuantizer<TPixel>(provider.Configuration, new QuantizerOptions()
            {
                Dither = null,
                MaxColors = maxColors
            });
            ReadOnlySpan<TPixel> palette = octreeQuantizer.BuildPalette(image.Frames.RootFrame, image.Bounds());

            map.BuildPalette(palette);
            QuantizeImage(image, map);

            image.DebugSave(provider, new
            {
                T = "QUANTIZED",
                MaxColors = maxColors
            });
        }

        [Theory]
        [WithFile(TestImages.Png.Bike, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.BikeGrayscale, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.CalliphoraPartial, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.Ducky, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Bmp.F, nameof(TestData), PixelTypes.Rgb24)]
        // [WithFile(TestImages.Png.Rgb48Bpp, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Gif.Giphy, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Png.Bike, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Png.Blur, nameof(TestData), PixelTypes.Rgba32)]
        // [WithFile(TestImages.Png.CalliphoraPartial, nameof(TestData), PixelTypes.Rgba32)]
        public void WebSafePalette<TPixel>(TestImageProvider<TPixel> provider, int maxColors)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            image.DebugSave(provider, new
            {
                T = "ORIGINAL"
            });

            using var map = new WuPaletteMap<TPixel>(Configuration.Default, maxColors);

            var palette = new TPixel[Color.WebSafePalette.Length];
            Color.ToPixel<TPixel>(provider.Configuration, Color.WebSafePalette.Span, palette);

            map.BuildPalette(palette);
            QuantizeImage(image, map);

            image.DebugSave(provider, new
            {
                T = "QUANTIZED",
                MaxColors = maxColors
            });
        }

        [Theory]
        [WithFile(TestImages.Png.BikeGrayscale,  PixelTypes.Rgb24, 32)]
        [WithFile(TestImages.Png.CalliphoraPartial,  PixelTypes.Rgb24, 32)]
        [WithFile(TestImages.Png.Ducky,  PixelTypes.Rgb24, 255)]
        public void TestOctreeQuantizer<TPixel>(TestImageProvider<TPixel> provider, int maxColors)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();
            using var q = new OctreeFrameQuantizer<TPixel>(Configuration.Default, new QuantizerOptions()
            {
                MaxColors = maxColors
            });

            q.BuildPalette(image.Frames.RootFrame, image.Bounds());
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
        public void OctreePlz<TPixel>(TestImageProvider<TPixel> provider, int maxColors, string ditherName)
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
            using QuantizedFrame<TPixel> qq = q.QuantizeFrame(image.Frames.RootFrame, image.Bounds());
            QuantizeImage(image, qq);

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
                Span<byte> qRow = map.GetPixelRowSpan(y);
                for (int x = 0; x < imageRow.Length; x++)
                {
                    imageRow[x] = palette[qRow[x]];
                }
            }
        }

        private static void QuantizeImage<TPixel>(Image<TPixel> image, WuPaletteMap<TPixel> map)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            for (int y = 0; y < image.Height; y++)
            {
                Span<TPixel> row = image.GetPixelRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    ref TPixel pixRef = ref row[x];
                    map.GetQuantizedColor(pixRef, out pixRef);
                }
            }
        }
    }
}
