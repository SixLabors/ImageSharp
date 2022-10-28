// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Quantization;

public class PixelSamplingStrategyTests
{
    public static readonly TheoryData<int, int, int, int> DefaultPixelSamplingStrategy_MultiFrame_Data = new()
    {
        { 100, 100, 1, 10000 },
        { 100, 100, 1, 5000 },
        { 100, 100, 10, 50000 },
        { 99, 100, 11, 30000 },
        { 97, 99, 11, 80000 },
        { 99, 100, 11, 20000 },
        { 99, 501, 20, 100000 },
        { 97, 500, 20, 10000 },
        { 103, 501, 20, 1000 },
    };

    public static readonly TheoryData<int, int, int> DefaultPixelSamplingStrategy_Data = new()
    {
        { 100, 100, 9900 },
        { 100, 100, 5000 },
        { 99, 100, 30000 },
        { 97, 99, 80000 },
        { 99, 100, 20000 },
        { 99, 501, 100000 },
        { 97, 500, 10000 },
        { 103, 501, 1000 },
    };

    [Fact]
    public void ExtensivePixelSamplingStrategy_EnumeratesAll_MultiFrame()
    {
        using Image<L8> image = CreateTestImage(100, 100, 100);
        ExtensivePixelSamplingStrategy strategy = new();

        foreach (Buffer2DRegion<L8> region in strategy.EnumeratePixelRegions(image))
        {
            PaintWhite(region);
        }

        using Image<L8> expected = CreateTestImage(100, 100, 100, true);

        ImageComparer.Exact.VerifySimilarity(expected, image);
    }

    [Fact]
    public void ExtensivePixelSamplingStrategy_EnumeratesAll()
    {
        using Image<L8> image = CreateTestImage(100, 100, 100);
        ExtensivePixelSamplingStrategy strategy = new();

        foreach (ImageFrame<L8> frame in image.Frames)
        {
            foreach (Buffer2DRegion<L8> region in strategy.EnumeratePixelRegions(frame))
            {
                PaintWhite(region);
            }
        }

        using Image<L8> expected = CreateTestImage(100, 100, 100, true);

        ImageComparer.Exact.VerifySimilarity(expected, image);
    }

    [Theory]
    [WithBlankImages(nameof(DefaultPixelSamplingStrategy_MultiFrame_Data), 1, 1, PixelTypes.L8)]
    public void DefaultPixelSamplingStrategy_IsFair_MultiFrame(TestImageProvider<L8> dummyProvider, int width, int height, int noOfFrames, int maximumNumberOfPixels)
    {
        using Image<L8> image = CreateTestImage(width, height, noOfFrames);

        DefaultPixelSamplingStrategy strategy = new(maximumNumberOfPixels, 0.1);

        long visitedPixels = 0;
        foreach (Buffer2DRegion<L8> region in strategy.EnumeratePixelRegions(image))
        {
            PaintWhite(region);
            visitedPixels += region.Width * region.Height;
        }

        image.DebugSaveMultiFrame(
            dummyProvider,
            $"W{width}_H{height}_noOfFrames_{noOfFrames}_maximumNumberOfPixels_{maximumNumberOfPixels}",
            appendPixelTypeToFileName: false);

        int maximumPixels = image.Width * image.Height * image.Frames.Count / 10;
        maximumPixels = Math.Max(maximumPixels, (int)strategy.MaximumPixels);

        // allow some inaccuracy:
        double visitRatio = visitedPixels / (double)maximumPixels;
        Assert.True(visitRatio <= 1.1, $"{visitedPixels}>{maximumPixels}");
    }

    [Theory]
    [WithBlankImages(nameof(DefaultPixelSamplingStrategy_Data), 1, 1, PixelTypes.L8)]
    public void DefaultPixelSamplingStrategy_IsFair(TestImageProvider<L8> dummyProvider, int width, int height, int maximumNumberOfPixels)
    {
        using Image<L8> image = CreateTestImage(width, height, 1);

        DefaultPixelSamplingStrategy strategy = new(maximumNumberOfPixels, 0.1);

        long visitedPixels = 0;
        foreach (ImageFrame<L8> frame in image.Frames)
        {
            foreach (Buffer2DRegion<L8> region in strategy.EnumeratePixelRegions(frame))
            {
                PaintWhite(region);
                visitedPixels += region.Width * region.Height;
            }
        }

        image.DebugSave(
            dummyProvider,
            $"W{width}_H{height}_maximumNumberOfPixels_{maximumNumberOfPixels}",
            appendPixelTypeToFileName: false);

        int maximumPixels = image.Width * image.Height * image.Frames.Count / 10;
        maximumPixels = Math.Max(maximumPixels, (int)strategy.MaximumPixels);

        // allow some inaccuracy:
        double visitRatio = visitedPixels / (double)maximumPixels;
        Assert.True(visitRatio <= 1.1, $"{visitedPixels}>{maximumPixels}");
    }

    private static void PaintWhite(Buffer2DRegion<L8> region)
    {
        L8 white = new(255);
        for (int y = 0; y < region.Height; y++)
        {
            region.DangerousGetRowSpan(y).Fill(white);
        }
    }

    private static Image<L8> CreateTestImage(int width, int height, int noOfFrames, bool paintWhite = false)
    {
        L8 bg = paintWhite ? new L8(255) : default;
        Image<L8> image = new(width, height, bg);

        for (int i = 1; i < noOfFrames; i++)
        {
            ImageFrame<L8> f = image.Frames.CreateFrame();
            if (paintWhite)
            {
                f.PixelBuffer.MemoryGroup.Fill(bg);
            }
        }

        return image;
    }
}
