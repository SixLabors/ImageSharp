// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison
{
    public abstract class ImageComparer
    {
        public static ImageComparer Exact { get; } = Tolerant(0, 0);

        /// <summary>
        /// Returns an instance of <see cref="TolerantImageComparer"/>.
        /// Individual manhattan pixel difference is only added to total image difference when the individual difference is over 'perPixelManhattanThreshold'.
        /// </summary>
        /// <returns>A ImageComparer instance.</returns>
        public static ImageComparer Tolerant(
            float imageThreshold = TolerantImageComparer.DefaultImageThreshold,
            int perPixelManhattanThreshold = 0)
        {
            return new TolerantImageComparer(imageThreshold, perPixelManhattanThreshold);
        }

        /// <summary>
        /// Returns Tolerant(imageThresholdInPercents/100)
        /// </summary>
        /// <returns>A ImageComparer instance.</returns>
        public static ImageComparer TolerantPercentage(float imageThresholdInPercents, int perPixelManhattanThreshold = 0)
            => Tolerant(imageThresholdInPercents / 100F, perPixelManhattanThreshold);

        public abstract ImageSimilarityReport<TPixelA, TPixelB> CompareImagesOrFrames<TPixelA, TPixelB>(
            ImageFrame<TPixelA> expected,
            ImageFrame<TPixelB> actual)
            where TPixelA : unmanaged, IPixel<TPixelA>
            where TPixelB : unmanaged, IPixel<TPixelB>;
    }

    public static class ImageComparerExtensions
    {
        public static ImageSimilarityReport<TPixelA, TPixelB> CompareImagesOrFrames<TPixelA, TPixelB>(
            this ImageComparer comparer,
            Image<TPixelA> expected,
            Image<TPixelB> actual)
            where TPixelA : unmanaged, IPixel<TPixelA>
            where TPixelB : unmanaged, IPixel<TPixelB>
        {
            return comparer.CompareImagesOrFrames(expected.Frames.RootFrame, actual.Frames.RootFrame);
        }

        public static IEnumerable<ImageSimilarityReport<TPixelA, TPixelB>> CompareImages<TPixelA, TPixelB>(
            this ImageComparer comparer,
            Image<TPixelA> expected,
            Image<TPixelB> actual)
            where TPixelA : unmanaged, IPixel<TPixelA>
            where TPixelB : unmanaged, IPixel<TPixelB>
        {
            var result = new List<ImageSimilarityReport<TPixelA, TPixelB>>();

            if (expected.Frames.Count != actual.Frames.Count)
            {
                throw new Exception("Frame count does not match!");
            }

            for (int i = 0; i < expected.Frames.Count; i++)
            {
                ImageSimilarityReport<TPixelA, TPixelB> report = comparer.CompareImagesOrFrames(expected.Frames[i], actual.Frames[i]);
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
            where TPixelA : unmanaged, IPixel<TPixelA>
            where TPixelB : unmanaged, IPixel<TPixelB>
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
            where TPixelA : unmanaged, IPixel<TPixelA>
            where TPixelB : unmanaged, IPixel<TPixelB>
        {
            if (expected.Size() != actual.Size())
            {
                throw new ImageDimensionsMismatchException(expected.Size(), actual.Size());
            }

            if (expected.Frames.Count != actual.Frames.Count)
            {
                throw new ImagesSimilarityException("Image frame count does not match!");
            }

            IEnumerable<ImageSimilarityReport<TPixelA, TPixelB>> reports = comparer.CompareImages(expected, actual);
            if (reports.Any())
            {
                var cleanedReports = new List<ImageSimilarityReport<TPixelA, TPixelB>>(reports.Count());
                foreach (ImageSimilarityReport<TPixelA, TPixelB> r in reports)
                {
                    IEnumerable<PixelDifference> outsideChanges = r.Differences.Where(
                        x =>
                        !(ignoredRegion.X <= x.Position.X
                        && x.Position.X <= ignoredRegion.Right
                        && ignoredRegion.Y <= x.Position.Y
                        && x.Position.Y <= ignoredRegion.Bottom));

                    if (outsideChanges.Any())
                    {
                        cleanedReports.Add(new ImageSimilarityReport<TPixelA, TPixelB>(r.ExpectedImage, r.ActualImage, outsideChanges, null));
                    }
                }

                if (cleanedReports.Count > 0)
                {
                    throw new ImageDifferenceIsOverThresholdException(cleanedReports);
                }
            }
        }
    }
}
