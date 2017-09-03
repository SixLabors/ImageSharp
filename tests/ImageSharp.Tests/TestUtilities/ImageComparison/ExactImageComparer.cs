namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;
    using System.Collections.Generic;
    using SixLabors.ImageSharp.Advanced;
    using SixLabors.ImageSharp.PixelFormats;

    using SixLabors.Primitives;

    public class ExactImageComparer : ImageComparer
    {
        public static ExactImageComparer Instance { get; } = new ExactImageComparer();

        public override ImageSimilarityReport CompareImagesOrFrames<TPixelA, TPixelB>(
            ImageBase<TPixelA> expected,
            ImageBase<TPixelB> actual)
        {
            if (expected.Size() != actual.Size())
            {
                throw new InvalidOperationException("Calling ImageComparer is invalid when dimensions mismatch!");
            }

            int width = actual.Width;

            // TODO: Comparing through Rgba32 is not robust enough because of the existance of super high precision pixel types.

            Rgba32[] aBuffer = new Rgba32[width];
            Rgba32[] bBuffer = new Rgba32[width];

            var differences = new List<PixelDifference>();

            for (int y = 0; y < actual.Height; y++)
            {
                Span<TPixelA> aSpan = expected.GetPixelRowSpan(y);
                Span<TPixelB> bSpan = actual.GetPixelRowSpan(y);

                PixelOperations<TPixelA>.Instance.ToRgba32(aSpan, aBuffer, width);
                PixelOperations<TPixelB>.Instance.ToRgba32(bSpan, bBuffer, width);

                for (int x = 0; x < width; x++)
                {
                    Rgba32 aPixel = aBuffer[x];
                    Rgba32 bPixel = bBuffer[x];

                    if (aPixel != bPixel)
                    {
                        var diff = new PixelDifference(new Point(x, y), aPixel, bPixel);
                        differences.Add(diff);
                    }
                }
            }

            return new ImageSimilarityReport(expected, actual, differences);
        }
    }
}