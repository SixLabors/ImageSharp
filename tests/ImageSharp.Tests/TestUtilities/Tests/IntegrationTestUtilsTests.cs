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
    }
}