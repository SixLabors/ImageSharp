// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities.Tests
{
    public class SystemDrawingReferenceCodecTests
    {
        private ITestOutputHelper Output { get; }

        public SystemDrawingReferenceCodecTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        [Theory]
        [WithTestPatternImages(20, 20, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void To32bppArgbSystemDrawingBitmap<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (System.Drawing.Bitmap sdBitmap = SystemDrawingBridge.To32bppArgbSystemDrawingBitmap(image))
                {
                    string fileName = provider.Utility.GetTestOutputFileName("png");
                    sdBitmap.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void From32bppArgbSystemDrawingBitmap<TPixel>(TestImageProvider<TPixel> dummyProvider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            string path = TestFile.GetInputFileFullPath(TestImages.Png.Splash);

            using (var sdBitmap = new System.Drawing.Bitmap(path))
            {
                using (Image<TPixel> image = SystemDrawingBridge.From32bppArgbSystemDrawingBitmap<TPixel>(sdBitmap))
                {
                    image.DebugSave(dummyProvider);
                }
            }
        }

        private static string SavePng<TPixel>(TestImageProvider<TPixel> provider, PngColorType pngColorType)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> sourceImage = provider.GetImage())
            {
                if (pngColorType != PngColorType.RgbWithAlpha)
                {
                    sourceImage.Mutate(c => c.MakeOpaque());
                }

                var encoder = new PngEncoder { ColorType = pngColorType };
                return provider.Utility.SaveTestOutputFile(sourceImage, "png", encoder);
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
        public void From32bppArgbSystemDrawingBitmap2<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (TestEnvironment.IsLinux)
            {
                return;
            }

            string path = SavePng(provider, PngColorType.RgbWithAlpha);

            using (var sdBitmap = new System.Drawing.Bitmap(path))
            {
                using (Image<TPixel> original = provider.GetImage())
                using (Image<TPixel> resaved = SystemDrawingBridge.From32bppArgbSystemDrawingBitmap<TPixel>(sdBitmap))
                {
                    ImageComparer comparer = ImageComparer.Exact;
                    comparer.VerifySimilarity(original, resaved);
                }
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.Rgb24)]
        public void From24bppRgbSystemDrawingBitmap<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            string path = SavePng(provider, PngColorType.Rgb);

            using (Image<TPixel> original = provider.GetImage())
            {
                using (var sdBitmap = new System.Drawing.Bitmap(path))
                {
                    using (Image<TPixel> resaved = SystemDrawingBridge.From24bppRgbSystemDrawingBitmap<TPixel>(sdBitmap))
                    {
                        ImageComparer comparer = ImageComparer.Exact;
                        comparer.VerifySimilarity(original, resaved);
                    }
                }
            }
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void OpenWithReferenceDecoder<TPixel>(TestImageProvider<TPixel> dummyProvider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            string path = TestFile.GetInputFileFullPath(TestImages.Png.Splash);
            using (var image = Image.Load<TPixel>(path, SystemDrawingReferenceDecoder.Instance))
            {
                image.DebugSave(dummyProvider);
            }
        }

        [Theory]
        [WithTestPatternImages(20, 20, PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void SaveWithReferenceEncoder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                provider.Utility.SaveTestOutputFile(image, "png", SystemDrawingReferenceEncoder.Png);
            }
        }
    }
}
