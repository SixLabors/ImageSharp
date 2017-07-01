namespace ImageSharp.Tests
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Tests.TestUtilities.Integration;

    using Xunit;
    using Xunit.Abstractions;

    public class IntegrationTestUtilsTests
    {
        private ITestOutputHelper Output { get; }

        public IntegrationTestUtilsTests(ITestOutputHelper output)
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
                using (System.Drawing.Bitmap sdBitmap = IntegrationTestUtils.ToSystemDrawingBitmap(image))
                {
                    string fileName = provider.Utility.GetTestOutputFileName("png");
                    sdBitmap.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void FromSystemDrawingBitmap<TPixel>(TestImageProvider<TPixel> dummyProvider)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = TestFile.GetPath(TestImages.Png.Splash);

            using (var sdBitmap = new System.Drawing.Bitmap(path))
            {
                using (Image<TPixel> image = IntegrationTestUtils.FromSystemDrawingBitmap<TPixel>(sdBitmap))
                {
                    image.DebugSave(dummyProvider);
                }
            }
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32 | PixelTypes.Bgra32)]
        public void OpenWithReferenceDecoder<TPixel>(TestImageProvider<TPixel> dummyProvider)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = TestFile.GetPath(TestImages.Png.Splash);
            using (Image<TPixel> image = Image.Load<TPixel>(path, ReferencePngDecoder.Instance))
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
                provider.Utility.SaveTestOutputFile(image, "png", ReferencePngEncoder.Instance);
            }
        }
    }
}