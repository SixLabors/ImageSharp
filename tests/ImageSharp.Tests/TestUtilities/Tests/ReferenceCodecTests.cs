namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.PixelFormats;
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
        public void FromSystemDrawingBitmap<TPixel>(TestImageProvider<TPixel> dummyProvider)
            where TPixel : struct, IPixel<TPixel>
        {
            string path = TestFile.GetInputFileFullPath(TestImages.Png.Splash);

            using (var sdBitmap = new System.Drawing.Bitmap(path))
            {
                using (Image<TPixel> image = SystemDrawingBridge.FromSystemDrawingBitmap<TPixel>(sdBitmap))
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