namespace ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ImageSharp.PixelFormats;

    using SixLabors.Primitives;

    public abstract class ImageComparer
    {
        public static ImageComparer Exact { get; } = ExactImageComparer.Instance;

        public static ImageComparer Tolerant(
            float imageThresholdInPercents = 0.01f,
            int pixelThresholdInPixelByteSum = 0)
        {
            return new TolerantImageComparer(imageThresholdInPercents, pixelThresholdInPixelByteSum);
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
                throw new ImagePixelsAreDifferentException(reports);
            }
        }
    }
}