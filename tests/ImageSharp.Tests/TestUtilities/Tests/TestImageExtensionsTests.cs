// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using System;

    using ImageSharp.PixelFormats;

    using Xunit;

    public class TestImageExtensionsTests
    {
        [Theory]
        [WithSolidFilledImages(10, 10, 0, 0, 255, PixelTypes.Rgba32)]
        public void CompareToReferenceOutput_WhenReferenceOutputMatches_ShouldNotThrow<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.CompareToReferenceOutput(provider);
            }
        }

        [Theory]
        [WithSolidFilledImages(10, 10, 0, 0, 255, PixelTypes.Rgba32)]
        public void CompareToReferenceOutput_WhenReferenceOutputDoesNotMatch_Throws<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.ThrowsAny<Exception>(() => image.CompareToReferenceOutput(provider));
            }
        }

        [Theory]
        [WithSolidFilledImages(10, 10, 0, 0, 255, PixelTypes.Rgba32)]
        public void CompareToReferenceOutput_WhenReferenceFileMissing_Throws<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.ThrowsAny<Exception>(() => image.CompareToReferenceOutput(provider));
            }
        }
    }
}