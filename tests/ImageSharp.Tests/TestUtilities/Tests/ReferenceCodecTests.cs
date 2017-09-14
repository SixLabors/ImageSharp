namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Formats.Png;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
    using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

    using Xunit;
    using Xunit.Abstractions;

    public class ReferenceCodecTests
    {
        private ITestOutputHelper Output { get; }

        public ReferenceCodecTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        [Theory]
        [WithTestPatternImages(20, 20, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void ToSystemDrawingBitmap<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (System.Drawing.Bitmap sdBitmap = SystemDrawingBridge.ToSystemDrawingBitmap(image))
                {
                    string fileName = provider.Utility.GetTestOutputFileName("png");
                    sdBitmap.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void FromFromArgb32SystemDrawingBitmap<TPixel>(TestImageProvider<TPixel> dummyProvider)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = TestFile.GetInputFileFullPath(TestImages.Png.Splash);

            using (var sdBitmap = new System.Drawing.Bitmap(path))
            {
                using (Image<TPixel> image = SystemDrawingBridge.FromFromArgb32SystemDrawingBitmap<TPixel>(sdBitmap))
                {
                    image.DebugSave(dummyProvider);
                }
            }
        }

        private static string SavePng<TPixel>(TestImageProvider<TPixel> provider, PngColorType pngColorType)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> sourceImage = provider.GetImage())
            {
                if (pngColorType != PngColorType.RgbWithAlpha)
                {
                    sourceImage.Mutate(c => c.Alpha(1));
                }

                var encoder = new PngEncoder() { PngColorType = pngColorType };
                return provider.Utility.SaveTestOutputFile(sourceImage, "png", encoder);
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
        public void FromFromArgb32SystemDrawingBitmap2<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            if (TestEnvironment.IsLinux) return;

            string path = SavePng(provider, PngColorType.RgbWithAlpha);
            
            using (var sdBitmap = new System.Drawing.Bitmap(path))
            {
                using (Image<TPixel> original = provider.GetImage())
                using (Image<TPixel> resaved = SystemDrawingBridge.FromFromArgb32SystemDrawingBitmap<TPixel>(sdBitmap))
                {
                    ImageComparer comparer = ImageComparer.Exact;
                    comparer.VerifySimilarity(original, resaved);
                }
            }
        }

        [Theory(Skip = "Doesen't work yet :(")]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
        public void FromFromRgb24SystemDrawingBitmap2<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = SavePng(provider, PngColorType.Rgb);

            using (Image<TPixel> original = provider.GetImage())
            {
                original.Mutate(c => c.Alpha(1));
                using (var sdBitmap = new System.Drawing.Bitmap(path))
                {
                    using (Image<TPixel> resaved = SystemDrawingBridge.FromFromRgb24SystemDrawingBitmap<TPixel>(sdBitmap))
                    {
                        resaved.Mutate(c => c.Alpha(1));
                        ImageComparer comparer = ImageComparer.Exact;
                        comparer.VerifySimilarity(original, resaved);
                    }
                }
            }
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void OpenWithReferenceDecoder<TPixel>(TestImageProvider<TPixel> dummyProvider)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = TestFile.GetInputFileFullPath(TestImages.Png.Splash);
            using (Image<TPixel> image = Image.Load<TPixel>(path, SystemDrawingReferenceDecoder.Instance))
            {
                image.DebugSave(dummyProvider);
            }
        }

        [Theory]
        [WithTestPatternImages(20, 20, PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void SaveWithReferenceEncoder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                provider.Utility.SaveTestOutputFile(image, "png", SystemDrawingReferenceEncoder.Png);
            }
        }
    }
}