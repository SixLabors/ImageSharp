// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;

using SixLabors.ImageSharp.PixelFormats;

using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison
{
    public abstract class ImageComparer
    {
        public static ImageComparer Exact { get; } = Tolerant(0, 0);

        public static ImageComparer Tolerant(
            float imageThreshold = TolerantImageComparer.DefaultImageThreshold,
            int perPixelManhattanThreshold = 0)
        {
            return new TolerantImageComparer(imageThreshold, perPixelManhattanThreshold);
        }

        public abstract ImageSimilarityReport CompareImagesOrFrames<TPixelA, TPixelB>(
            ImageBase<TPixelA> expected,
            ImageBase<TPixelB> actual)
            where TPixelA : struct, IPixel<TPixelA> where TPixelB : struct, IPixel<TPixelB>;
    }

    public static class ImageComparerExtensions
    {
        public static IEnumerable<ImageSimilarityReport> CompareImages<TPixelA, TPixelB>(
            this ImageComparer comparer,
            Image<TPixelA> expected,
            Image<TPixelB> actual)
            where TPixelA : struct, IPixel<TPixelA> where TPixelB : struct, IPixel<TPixelB>
        {
            var result = new List<ImageSimilarityReport>();
            ImageSimilarityReport report = comparer.CompareImagesOrFrames(expected, actual);

            if (!report.IsEmpty)
            {
                result.Add(report);
            }

            if (expected.Frames.Count != actual.Frames.Count)
            {
                throw new Exception("Frame count does not match!");
            }
            for (int i = 0; i < expected.Frames.Count; i++)
            {
                report = comparer.CompareImagesOrFrames(expected.Frames[i], actual.Frames[i]);
                if (!report.IsEmpty)
                {
                    result.Add(report);
                }
            }
            return result;
        }

        public static void VerifySimilarity<TPixelA, TPixelB>(
            this ImageComparer comparer,
            Image<TPixelA> expected,
            Image<TPixelB> actual)
            where TPixelA : struct, IPixel<TPixelA> where TPixelB : struct, IPixel<TPixelB>
        {
            if (expected.Size() != actual.Size())
            {
                throw new ImageDimensionsMismatchException(expected.Size(), actual.Size());
            }

            if (expected.Frames.Count != actual.Frames.Count)
            {
                throw new ImagesSimilarityException("Image frame count does not match!");
            }

            IEnumerable<ImageSimilarityReport> reports = comparer.CompareImages(expected, actual);
            if (reports.Any())
            {
                throw new ImageDifferenceIsOverThresholdException(reports);
            }
        }

        public static void VerifySimilarityIgnoreRegion<TPixelA, TPixelB>(
            this ImageComparer comparer,
            Image<TPixelA> expected,
            Image<TPixelB> actual,
            Rectangle ignoredRegion)
            where TPixelA : struct, IPixel<TPixelA>
            where TPixelB : struct, IPixel<TPixelB>
        {
            if (expected.Size() != actual.Size())
            {
                throw new ImageDimensionsMismatchException(expected.Size(), actual.Size());
            }

            if (expected.Frames.Count != actual.Frames.Count)
            {
                throw new ImagesSimilarityException("Image frame count does not match!");
            }

            IEnumerable<ImageSimilarityReport> reports = comparer.CompareImages(expected, actual);
            if (reports.Any())
            {
                List<ImageSimilarityReport> cleanedReports = new List<ImageSimilarityReport>(reports.Count());
                foreach (var r in reports)
                {
                    var outsideChanges = r.Differences.Where(x => !(
                        ignoredRegion.X <= x.Position.X &&
                        x.Position.X <= ignoredRegion.Right &&
                        ignoredRegion.Y <= x.Position.Y &&
                        x.Position.Y <= ignoredRegion.Bottom));
                    if (outsideChanges.Any())
                    {
                        cleanedReports.Add(new ImageSimilarityReport(r.ExpectedImage, r.ActualImage, outsideChanges, null));
                    }
                }

                if (cleanedReports.Any())
                {
                    throw new ImageDifferenceIsOverThresholdException(cleanedReports);
                }
            }
        }
    }
}