using SixLabors.ImageSharp.Formats.WebP;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    using static TestImages.WebP;

    public class WebPEncoderTests
    {
        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32)]
        public void Encode_Lossless_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new WebPEncoder()
            {
                Lossy = false
            };

            using (Image<TPixel> image = provider.GetImage())
            {
                image.VerifyEncoder(provider, "webp", "lossless", encoder);
            }
        }
    }
}
