// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Comparison;
using SixLabors.ImageSharp.Comparison.Exceptions;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Comparison;

[Trait("Category", "Comparison")]
public class ImageComparerTests
{
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    [Fact]
    public void IdenticalImagesAreEmpty()
    {
        using Image<Rgba32> expected = new(3, 2, Black);
        using Image<Rgba32> actual = new(3, 2, Black);

        ImageComparer comparer = ImageComparer.Exact;
        ImageSimilarityReport report = comparer.CompareImagesOrFrames(expected, actual);

        Assert.True(report.IsEmpty);
        Assert.Equal(0F, report.TotalNormalizedDifference);
        Assert.Equal("0%", report.DifferencePercentageString);
        Assert.Equal("[SimilarImages]", report.ToString());
        Assert.Equal(0, report.Index);
        Assert.Equal(expected.Frames.RootFrame, report.ExpectedImageFrame);
        Assert.Equal(actual.Frames.RootFrame, report.ActualImageFrame);
    }

    [Fact]
    public void ExactComparerReportsOneUnitDifference()
    {
        using Image<Rgba32> expected = new(1, 2, Black);
        using Image<Rgba32> actual = new(1, 2, Black);
        actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[0] = new Rgba32(1, 0, 0, 255);

        ImageComparer comparer = ImageComparer.Exact;
        ImageSimilarityReport report = comparer.CompareImagesOrFrames(expected, actual);

        Assert.False(report.IsEmpty);

        // One eight-bit unit difference on one pixel of a two-pixel image:
        // one unit equals 257 sixteen-bit units, normalized by width * height * 4 * 65535.
        float expectedDifference = 257F / (1 * 2 * 4 * 65535F);
        Assert.True(MathF.Abs(report.TotalNormalizedDifference - expectedDifference) < 1e-6F);
        Assert.Equal("0.0490%", report.DifferencePercentageString);
        Assert.Contains("Total difference:", report.ToString());
    }

    [Fact]
    public void TolerantComparerSkipsDifferencesBelowPerPixelThreshold()
    {
        using Image<Rgba32> expected = new(4, 4, Black);
        using Image<Rgba32> actual = new(4, 4, Black);
        actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(2)[1] = new Rgba32(1, 0, 0, 255);

        // The per-pixel manhattan difference of one eight-bit unit is about 257 sixteen-bit units,
        // so a threshold above that skips the pixel from the total difference.
        ImageComparer comparer = ImageComparer.Tolerant(0, perPixelManhattanThreshold: 260);
        ImageSimilarityReport report = comparer.CompareImagesOrFrames(expected, actual);

        Assert.True(report.IsEmpty);
    }

    [Fact]
    public void TolerantComparerAccumulatesDifferencesOverPerPixelThreshold()
    {
        using Image<Rgba32> expected = new(4, 4, Black);
        using Image<Rgba32> actual = new(4, 4, Black);
        actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(2)[1] = new Rgba32(2, 0, 0, 255);

        ImageComparer comparer = ImageComparer.Tolerant(0, perPixelManhattanThreshold: 260);
        ImageSimilarityReport report = comparer.CompareImagesOrFrames(expected, actual);

        Assert.False(report.IsEmpty);
    }

    [Fact]
    public void TolerantComparerDefaultThresholdAllowsOneUnitDifferenceOnLargeImage()
    {
        using Image<Rgba32> expected = new(100, 100, Black);
        using Image<Rgba32> actual = new(100, 100, Black);
        actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(42)[37] = new Rgba32(1, 0, 0, 255);

        ImageComparer comparer = ImageComparer.Tolerant();
        ImageSimilarityReport report = comparer.CompareImagesOrFrames(expected, actual);

        Assert.True(report.IsEmpty);
    }

    [Fact]
    public void TolerantPercentageComparerMatchesThresholdSemantics()
    {
        using Image<Rgba32> expected = new(1, 1, Black);
        using Image<Rgba32> actual = new(1, 1, Black);
        actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[0] = new Rgba32(1, 0, 0, 255);

        // One eight-bit unit difference on a single pixel is a difference of about 0.098%.
        ImageComparer belowThreshold = ImageComparer.TolerantPercentage(1F);
        ImageSimilarityReport emptyReport = belowThreshold.CompareImagesOrFrames(expected, actual);
        Assert.True(emptyReport.IsEmpty);

        ImageComparer overThreshold = ImageComparer.TolerantPercentage(0.05F);
        ImageSimilarityReport report = overThreshold.CompareImagesOrFrames(expected, actual);
        Assert.False(report.IsEmpty);
    }

    [Fact]
    public void DimensionMismatchThrows()
    {
        using Image<Rgba32> expected = new(2, 2, Black);
        using Image<Rgba32> actual = new(2, 3, Black);

        ImageComparer comparer = ImageComparer.Exact;

        ImageDimensionsMismatchException exception =
            Assert.Throws<ImageDimensionsMismatchException>(() => comparer.CompareImagesOrFrames(expected, actual));

        Assert.Equal(new Size(2, 2), exception.ExpectedSize);
        Assert.Equal(new Size(2, 3), exception.ActualSize);
    }

    [Fact]
    public void FrameCountMismatchThrows()
    {
        using Image<Rgba32> expected = new(2, 1, Black);
        using Image<Rgba32> actual = new(2, 1, Black);
        actual.Frames.CreateFrame(Black);

        ImageComparer comparer = ImageComparer.Exact;

        ImageFramesMismatchException exception =
            Assert.Throws<ImageFramesMismatchException>(() => comparer.CompareImages(expected, actual));

        Assert.Equal(1, exception.ExpectedCount);
        Assert.Equal(2, exception.ActualCount);
    }

    [Fact]
    public void VerifySimilarityPassesOnIdenticalImages()
    {
        using Image<Rgba32> expected = new(2, 2, Black);
        using Image<Rgba32> actual = new(2, 2, Black);

        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    [Fact]
    public void VerifySimilarityThrowsOnDifference()
    {
        using Image<Rgba32> expected = new(1, 2, Black);
        using Image<Rgba32> actual = new(1, 2, Black);
        actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(1)[0] = new Rgba32(1, 0, 0, 255);

        ImageDifferenceIsOverThresholdException exception =
            Assert.Throws<ImageDifferenceIsOverThresholdException>(() => ImageComparer.Exact.VerifySimilarity(expected, actual));

        Assert.Single(exception.Reports);
        Assert.Equal(0, exception.Reports[0].Index);
        Assert.False(exception.Reports[0].IsEmpty);
        Assert.Contains("Image difference is over threshold!", exception.Message);
    }

    [Fact]
    public void CompareImagesReportsFrameIndices()
    {
        Rgba32 backgroundColor = new(10, 20, 30, 255);
        using Image<Rgba32> expected = new(2, 1, backgroundColor);
        expected.Frames.CreateFrame(backgroundColor);

        using Image<Rgba32> actual = new(2, 1, backgroundColor);
        actual.Frames.CreateFrame(backgroundColor);

        // Only the root frame differs; the second frame matches.
        actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[0] = new Rgba32(11, 20, 30, 255);

        IReadOnlyList<ImageSimilarityReport> reports = ImageComparer.Exact.CompareImages(expected, actual);

        Assert.Single(reports);
        Assert.Equal(0, reports[0].Index);
    }

    [Fact]
    public void DifferenceImageOfIdenticalImagesIsMidGray()
    {
        using Image<Rgba32> expected = new(3, 2, Black);
        using Image<Rgba32> actual = new(3, 2, Black);

        ImageSimilarityReport report = ImageComparer.Exact.CompareImagesOrFrames(expected, actual);
        using Image differenceImage = report.CreateDifferenceImage();

        Image<Rgba32> difference = Assert.IsType<Image<Rgba32>>(differenceImage);
        Buffer2D<Rgba32> buffer = difference.Frames.RootFrame.PixelBuffer;
        for (int y = 0; y < difference.Height; y++)
        {
            Span<Rgba32> row = buffer.DangerousGetRowSpan(y);
            for (int x = 0; x < difference.Width; x++)
            {
                Assert.Equal(new Rgba32(128, 128, 128, 255), row[x]);
            }
        }
    }

    [Fact]
    public void DifferenceImageEncodesComponentDirection()
    {
        using Image<Rgba32> expected = new(1, 2, Black);
        expected.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(1)[0] = new Rgba32(10, 5, 0, 255);

        using Image<Rgba32> actual = new(1, 2, Black);
        actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[0] = new Rgba32(10, 0, 0, 255);
        actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(1)[0] = new Rgba32(0, 5, 20, 255);

        ImageSimilarityReport report = ImageComparer.Exact.CompareImagesOrFrames(expected, actual);
        using Image differenceImage = report.CreateDifferenceImage();

        Image<Rgba32> difference = Assert.IsType<Image<Rgba32>>(differenceImage);
        Buffer2D<Rgba32> buffer = difference.Frames.RootFrame.PixelBuffer;

        // The red component of the actual image is above the expected one, rendered as white;
        // the equal green and blue components are rendered as mid-gray.
        Assert.Equal(new Rgba32(255, 128, 128, 255), buffer.DangerousGetRowSpan(0)[0]);

        // The red component of the actual image is below the expected one, rendered as black;
        // the blue component of the actual image is above the expected one, rendered as white.
        Assert.Equal(new Rgba32(0, 128, 255, 255), buffer.DangerousGetRowSpan(1)[0]);
    }

    [Fact]
    public void ComparisonWorksThroughNonGenericImages()
    {
        using Image<Rgba32> expectedImage = new(1, 2, Black);
        using Image<Rgba32> actualImage = new(1, 2, Black);
        actualImage.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)[0] = new Rgba32(0, 0, 1, 255);

        // Both images are passed through their non-generic base type, so the pixel types are
        // resolved by double dispatch at run time.
        Image expected = expectedImage;
        Image actual = actualImage;

        ImageComparer comparer = ImageComparer.Exact;
        ImageSimilarityReport report = comparer.CompareImagesOrFrames(expected, actual);

        Assert.False(report.IsEmpty);

        using Image differenceImage = report.CreateDifferenceImage();
        Assert.IsType<Image<Rgba32>>(differenceImage);
    }

    [Fact]
    public void ComparisonSupportsMixedPixelFormats()
    {
        using Image<Rgba32> expected = new(1, 2, new Rgba32(255, 0, 0, 255));
        using Image<Rgb24> actual = new(1, 2, new Rgb24(255, 0, 0));

        // The formats store identical RGB components and both convert to alpha 1, so the images match.
        ImageSimilarityReport emptyReport = ImageComparer.Exact.CompareImagesOrFrames(expected, actual);
        Assert.True(emptyReport.IsEmpty);

        actual.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(1)[0] = new Rgb24(128, 0, 0);

        ImageSimilarityReport report = ImageComparer.Exact.CompareImagesOrFrames(expected, actual);
        Assert.False(report.IsEmpty);

        using Image differenceImage = report.CreateDifferenceImage();
        Image<Rgba32> difference = Assert.IsType<Image<Rgba32>>(differenceImage);
        Buffer2D<Rgba32> buffer = difference.Frames.RootFrame.PixelBuffer;

        // The red component of the actual image is below the expected one, rendered as black.
        Assert.Equal(new Rgba32(0, 128, 128, 255), buffer.DangerousGetRowSpan(1)[0]);
    }
}
