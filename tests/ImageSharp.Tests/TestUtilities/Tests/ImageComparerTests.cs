// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using Moq;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests;

public class ImageComparerTests
{
    /// <summary>
    /// Checks that images with different alpha representations are compared in the same component representation.
    /// </summary>
    /// <typeparam name="TPixel">The associated floating-point pixel format.</typeparam>
    /// <param name="provider">The existing PNG fixture with partial and zero alpha.</param>
    [Theory]
    [WithFile(TestImages.Png.AverageFilter4BytesPerPixel, PixelTypes.RgbaVectorP)]
    public void ImageComparer_MixedAlphaRepresentations_CompareEqual<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> associated = provider.GetImage();
        using Image<RgbaVector> unassociated = associated.CloneAs<RgbaVector>();

        // CloneAs changes the alpha representation, not the colors. Comparison must agree in either argument order.
        ImageComparer.Exact.VerifySimilarity(associated, unassociated);
        ImageComparer.Exact.VerifySimilarity(unassociated, associated);
    }

    /// <summary>
    /// Checks that image comparison retains sub-code floating-point differences and reports their pixel positions.
    /// </summary>
    /// <typeparam name="TPixel">The floating-point pixel format.</typeparam>
    /// <param name="provider">The existing PNG fixture.</param>
    [Theory]
    [WithFile(TestImages.Png.AverageFilter4BytesPerPixel, PixelTypes.RgbaVector)]
    public void ImageComparers_ReportFloatingPointDifferences<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> expected = provider.GetImage();
        using Image<TPixel> actual = expected.Clone();

        // A quarter of a sixteen-bit code would disappear during integer pixel conversion.
        // Start at zero so both values would round to the same Rgba64 red code.
        float difference = .25F / ushort.MaxValue;
        Vector4 sample = expected[0, 0].ToUnassociatedScaledVector4();
        sample.X = 0F;
        expected[0, 0] = TPixel.FromUnassociatedScaledVector4(sample);
        sample.X = difference;
        actual[0, 0] = TPixel.FromUnassociatedScaledVector4(sample);

        PixelDifference pixel = ImageComparer.Exact.CompareImages(expected, actual).Single().Differences.Single();

        Assert.Equal(new Point(0, 0), pixel.Position);
        Assert.Equal(.25F, pixel.RedDifference);

        // Per-pixel thresholds remain in sixteen-bit units. They apply before differences enter the image average.
        ImageComparer.Tolerant(0F, 1).VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Checks that image comparison retains component differences outside the integer pixel range.
    /// </summary>
    /// <typeparam name="TPixel">The floating-point pixel format.</typeparam>
    /// <param name="provider">The existing PNG fixture.</param>
    /// <param name="red">The reference red component outside the range zero to one.</param>
    [Theory]
    [WithFile(TestImages.Png.AverageFilter4BytesPerPixel, PixelTypes.RgbaVector, -2F)]
    [WithFile(TestImages.Png.AverageFilter4BytesPerPixel, PixelTypes.RgbaVector, 2F)]
    public void ImageComparers_ReportOutOfRangeDifferences<TPixel>(TestImageProvider<TPixel> provider, float red)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> expected = provider.GetImage();
        using Image<TPixel> actual = expected.Clone();

        // Both red values are outside the integer range and would clip to the same endpoint during Rgba64 conversion.
        Vector4 sample = expected[0, 0].ToUnassociatedScaledVector4();
        sample.X = red;
        expected[0, 0] = TPixel.FromUnassociatedScaledVector4(sample);
        sample.X = red < 0F ? red - 1F : red + 1F;
        actual[0, 0] = TPixel.FromUnassociatedScaledVector4(sample);

        PixelDifference pixel = ImageComparer.Exact.CompareImages(expected, actual).Single().Differences.Single();

        Assert.Equal(new Point(0, 0), pixel.Position);
        Assert.Equal(red < 0F ? -ushort.MaxValue : ushort.MaxValue, pixel.RedDifference);
    }

    public ImageComparerTests(ITestOutputHelper output)
    {
        this.Output = output;
    }

    private ITestOutputHelper Output { get; }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 0.0001f, 1)]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 0, 0)]
    public void TolerantImageComparer_ApprovesPerfectSimilarity<TPixel>(
        TestImageProvider<TPixel> provider,
        float imageThreshold,
        int pixelThreshold)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            using (Image<TPixel> clone = image.Clone())
            {
                ImageComparer comparer = ImageComparer.Tolerant(imageThreshold, pixelThreshold);
                comparer.VerifySimilarity(image, clone);
            }
        }
    }

    [Theory]
    [WithTestPatternImages(110, 110, PixelTypes.Rgba32)]
    public void TolerantImageComparer_ApprovesSimilarityBelowTolerance<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            using (Image<TPixel> clone = image.Clone())
            {
                ImagingTestCaseUtility.ModifyPixel(clone, 0, 0, 1);

                ImageComparer comparer = ImageComparer.Tolerant();
                comparer.VerifySimilarity(image, clone);
            }
        }
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void TolerantImageComparer_DoesNotApproveSimilarityAboveTolerance<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            using (Image<TPixel> clone = image.Clone())
            {
                byte perChannelChange = 20;
                ImagingTestCaseUtility.ModifyPixel(clone, 3, 1, perChannelChange);

                ImageComparer comparer = ImageComparer.Tolerant();

                ImageDifferenceIsOverThresholdException ex = Assert.ThrowsAny<ImageDifferenceIsOverThresholdException>(
                    () => comparer.VerifySimilarity(image, clone));

                PixelDifference diff = ex.Reports.Single().Differences.Single();
                Assert.Equal(new Point(3, 1), diff.Position);
            }
        }
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba64)]
    public void TolerantImageComparer_TestPerPixelThreshold<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            using (Image<TPixel> clone = image.Clone())
            {
                ImagingTestCaseUtility.ModifyPixel(clone, 0, 0, 1);
                ImagingTestCaseUtility.ModifyPixel(clone, 1, 0, 1);
                ImagingTestCaseUtility.ModifyPixel(clone, 2, 0, 1);

                ImageComparer comparer = ImageComparer.Tolerant(perPixelManhattanThreshold: 257 * 3);
                comparer.VerifySimilarity(image, clone);
            }
        }
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 99, 100)]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 100, 99)]
    public void VerifySimilarity_ThrowsOnSizeMismatch<TPixel>(TestImageProvider<TPixel> provider, int w, int h)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            using (Image<TPixel> clone = image.Clone(ctx => ctx.Resize(w, h)))
            {
                ImageDimensionsMismatchException ex = Assert.ThrowsAny<ImageDimensionsMismatchException>(
                    () =>
                        {
                            ImageComparer comparer = Mock.Of<ImageComparer>();
                            comparer.VerifySimilarity(image, clone);
                        });
                this.Output.WriteLine(ex.Message);
            }
        }
    }

    [Theory]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
    public void VerifySimilarity_WhenAnImageFrameIsDifferent_Reports<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            using (Image<TPixel> clone = image.Clone())
            {
                ImagingTestCaseUtility.ModifyPixel(clone.Frames[0], 42, 43, 1);

                IEnumerable<ImageSimilarityReport> reports = ImageComparer.Exact.CompareImages(image, clone);

                PixelDifference difference = reports.Single().Differences.Single();
                Assert.Equal(new Point(42, 43), difference.Position);
            }
        }
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void ExactComparer_ApprovesExactEquality<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            using (Image<TPixel> clone = image.Clone())
            {
                ExactImageComparer.Instance.CompareImages(image, clone);
            }
        }
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void ExactComparer_DoesNotTolerateAnyPixelDifference<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            using (Image<TPixel> clone = image.Clone())
            {
                ImagingTestCaseUtility.ModifyPixel(clone, 42, 24, 1);
                ImagingTestCaseUtility.ModifyPixel(clone, 7, 93, 1);

                IEnumerable<ImageSimilarityReport> reports = ExactImageComparer.Instance.CompareImages(image, clone);

                this.Output.WriteLine(reports.Single().ToString());
                PixelDifference[] differences = reports.Single().Differences;
                Assert.Equal(2, differences.Length);
                Assert.Contains(differences, d => d.Position == new Point(42, 24));
                Assert.Contains(differences, d => d.Position == new Point(7, 93));
            }
        }
    }
}
