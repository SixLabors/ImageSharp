using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Might be useful to catch complex bugs
    /// </summary>
    public class ComplexIntegrationTests
    {
        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Snake, PixelTypes.Rgba32, 75, JpegSubsample.Ratio420)]
        [WithFile(TestImages.Jpeg.Baseline.Lake, PixelTypes.Rgba32, 75, JpegSubsample.Ratio420)]
        [WithFile(TestImages.Jpeg.Baseline.Snake, PixelTypes.Rgba32, 75, JpegSubsample.Ratio444)]
        [WithFile(TestImages.Jpeg.Baseline.Lake, PixelTypes.Rgba32, 75, JpegSubsample.Ratio444)]
        public void LoadResizeSave<TPixel>(TestImageProvider<TPixel> provider, int quality, JpegSubsample subsample)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(x => x.Resize(new ResizeOptions { Size = new Size(150, 100), Mode = ResizeMode.Max })))
            {

                image.MetaData.ExifProfile = null; // Reduce the size of the file
                JpegEncoder options = new JpegEncoder { Subsample = subsample, Quality = quality };

                provider.Utility.TestName += $"{subsample}_Q{quality}";
                provider.Utility.SaveTestOutputFile(image, "png");
                provider.Utility.SaveTestOutputFile(image, "jpg", options);
            }
        }
    }
}