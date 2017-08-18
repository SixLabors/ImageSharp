namespace ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ImageSharp.PixelFormats;

    using SixLabors.Primitives;

    public abstract class ImageComparer
    {
        public static ImageComparer Exact { get; } = Tolerant(0, 0);

        public static ImageComparer Tolerant(
            float imageThreshold = TolerantImageComparer.DefaultImageThreshold,
            int pixelThresholdInPixelByteSum = 0)
        {
            return new TolerantImageComparer(imageThreshold, pixelThresholdInPixelByteSum);
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

        /// <summary>
        /// Fills the bounded area with a solid color and does a visual comparison between 2 images asserting the difference outwith
        /// that area is less then a configurable threshold.
        /// </summary>
        /// <typeparam name="TPixelA">The color of the expected image</typeparam>
        /// <typeparam name="TPixelB">The color type fo the the actual image</typeparam>
        /// <param name="comparer">The <see cref="ImageComparer"/> to use</param>
        /// <param name="expected">The expected image</param>
        /// <param name="actual">The actual image</param>
        /// <param name="bounds">The bounds within the image has been altered</param>
        public static void EnsureProcessorChangesAreConstrained<TPixelA, TPixelB>(
            this ImageComparer comparer,
            Image<TPixelA> expected,
            Image<TPixelB> actual,
            Rectangle bounds)
            where TPixelA : struct, IPixel<TPixelA>
            where TPixelB : struct, IPixel<TPixelB>
        {
            // Draw identical shapes over the bounded and compare to ensure changes are constrained.
            expected.Mutate(x => x.Fill(NamedColors<TPixelA>.HotPink, bounds));
            actual.Mutate(x => x.Fill(NamedColors<TPixelB>.HotPink, bounds));

            comparer.VerifySimilarity(expected, actual);
        }
    }
}