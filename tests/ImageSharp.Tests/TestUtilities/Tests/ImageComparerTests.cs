// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using ImageSharp.PixelFormats;

    using SixLabors.Primitives;

    using Xunit;
    using Xunit.Abstractions;

    public class ImageComparerTests
    {
        public ImageComparerTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Theory]
        [WithTestPatternImages(
            100,
            100,
            PixelTypes.Rgba32,
            PercentageImageComparer.DefaultImageThreshold,
            PercentageImageComparer.DefaultSegmentThreshold,
            PercentageImageComparer.DefaultScaleIntoSize)]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 0, 0, 100)]
        public void PercentageComparer_ApprovesPerfectSimilarity<TPixel>(
            TestImageProvider<TPixel> provider,
            float imageTheshold,
            byte segmentThreshold,
            int scaleIntoSize)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (Image<TPixel> clone = image.Clone())
                {
                    PercentageImageComparer.VerifySimilarity(
                        image,
                        clone,
                        imageTheshold,
                        segmentThreshold,
                        scaleIntoSize);
                }
            }
        }

        private static void ModifyPixel<TPixel>(Image<TPixel> img, int x, int y, byte value)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel pixel = img[x, y];
            var rgbaPixel = default(Rgba32);
            pixel.ToRgba32(ref rgbaPixel);
            rgbaPixel.R += value;
            pixel.PackFromRgba32(rgbaPixel);
            img[x, y] = pixel;
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
        public void PercentageComparer_ApprovesImperfectSimilarity<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (Image<TPixel> clone = image.Clone())
                {
                    ModifyPixel(clone, 0, 0, 2);

                    PercentageImageComparer.VerifySimilarity(image, clone, scaleIntoSize: 100);
                }
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 99, 100)]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 100, 99)]
        public void PercentageComparer_ThrowsOnSizeMismatch<TPixel>(TestImageProvider<TPixel> provider, int w, int h)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (Image<TPixel> clone = image.Clone(ctx => ctx.Resize(w, h)))
                {
                    ImageDimensionsMismatchException ex = Assert.ThrowsAny<ImageDimensionsMismatchException>(
                        () =>
                            {
                                PercentageImageComparer.VerifySimilarity(image, clone);
                            });
                    this.Output.WriteLine(ex.Message);
                }
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
        public void PercentageComparer_WhenDifferenceIsTooLarge_Throws<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (Image<TPixel> clone = image.Clone())
                {
                    ModifyPixel(clone, 0, 0, 42);
                    ModifyPixel(clone, 1, 0, 42);
                    ModifyPixel(clone, 2, 0, 42);

                    Assert.ThrowsAny<ImagesSimilarityException>(
                        () => { PercentageImageComparer.VerifySimilarity(image, clone, scaleIntoSize: 100); });
                }
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
        public void ExactComparer_ApprovesExactEquality<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (Image<TPixel> clone = image.Clone())
                {
                    ExactComparer.Instance.Verify(image, clone);
                }
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 99, 100)]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 100, 99)]
        public void ExactComparer_ThrowsOnSizeMismatch<TPixel>(TestImageProvider<TPixel> provider, int w, int h)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (Image<TPixel> clone = image.Clone(ctx => ctx.Resize(w, h)))
                {
                    ImageDimensionsMismatchException ex = Assert.ThrowsAny<ImageDimensionsMismatchException>(
                        () =>
                            {
                                ExactComparer.Instance.Verify(image, clone);
                            });
                    this.Output.WriteLine(ex.Message);
                }
            }
        }

        [Theory]
        [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
        public void ExactComparer_ThrowsOnSmallestPixelDifference<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                using (Image<TPixel> clone = image.Clone())
                {
                    ModifyPixel(clone, 42, 42, 1);
                    ModifyPixel(clone, 7, 93, 1);

                    ImagesAreNotEqualException ex = Assert.ThrowsAny<ImagesAreNotEqualException>(
                        () =>
                            {
                                ExactComparer.Instance.Verify(image, clone);
                            });
                    this.Output.WriteLine(ex.Message);
                    Assert.Equal(2, ex.Differences.Length);
                    Assert.Contains(new Point(42, 24), ex.Differences);
                    Assert.Contains(new Point(7, 93), ex.Differences);
                }
            }
        }
    }
}