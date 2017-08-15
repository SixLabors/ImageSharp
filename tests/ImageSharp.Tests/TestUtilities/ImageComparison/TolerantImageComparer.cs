namespace ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;
    using System.Collections.Generic;

    using ImageSharp.PixelFormats;

    using SixLabors.Primitives;

    public class TolerantImageComparer : ImageComparer
    {
        public const float DefaultImageThreshold = 1.0f / (100 * 100 * 255);

        public TolerantImageComparer(float imageThreshold, int pixelThresholdInPixelByteSum = 0)
        {
            this.ImageThreshold = imageThreshold;
            this.PixelThresholdInPixelByteSum = pixelThresholdInPixelByteSum;
        }

        /// <summary>
        /// The maximal tolerated difference represented by a value between 0.0 and 1.0.
        /// Examples of percentage differences on a single pixel:
        /// 1. PixelA = (255,255,255,0) PixelB =(0,0,0,255) leads to 100% difference on a single pixel
        /// 2. PixelA = (255,255,255,0) PixelB =(255,255,255,255) leads to 25% difference on a single pixel
        /// 3. PixelA = (255,255,255,0) PixelB =(128,128,128,128) leads to 50% difference on a single pixel
        /// 
        /// The total differences is the sum of all pixel differences normalized by image dimensions!
        /// 
        /// ImageThresholdInPercents = 1.0/255 means that we allow one byte difference per channel on a 1x1 image
        /// ImageThresholdInPercents = 1.0/(100*100*255) means that we allow only one byte difference per channel on a 100x100 image
        /// </summary>
        public float ImageThreshold { get; }

        /// <summary>
        /// The threshold of the individual pixels before they acumulate towards the overall difference.
        /// For an individual <see cref="Rgba32"/> pixel the value it's calculated as: pixel.R + pixel.G + pixel.B + pixel.A
        /// </summary>
        public int PixelThresholdInPixelByteSum { get; }

        public override ImageSimilarityReport CompareImagesOrFrames<TPixelA, TPixelB>(ImageBase<TPixelA> expected, ImageBase<TPixelB> actual)
        {
            if (expected.Size() != actual.Size())
            {
                throw new InvalidOperationException("Calling ImageComparer is invalid when dimensions mismatch!");
            }

            int width = actual.Width;

            // TODO: Comparing through Rgba32 is not robust enough because of the existance of super high precision pixel types.

            Rgba32[] aBuffer = new Rgba32[width];
            Rgba32[] bBuffer = new Rgba32[width];

            float totalDifference = 0.0f;

            var differences = new List<PixelDifference>();

            for (int y = 0; y < actual.Height; y++)
            {
                Span<TPixelA> aSpan = expected.GetRowSpan(y);
                Span<TPixelB> bSpan = actual.GetRowSpan(y);

                PixelOperations<TPixelA>.Instance.ToRgba32(aSpan, aBuffer, width);
                PixelOperations<TPixelB>.Instance.ToRgba32(bSpan, bBuffer, width);

                for (int x = 0; x < width; x++)
                {
                    int d = GetDifferenceInPixelByteSum(ref aBuffer[x], ref bBuffer[x]);

                    if (d > this.PixelThresholdInPixelByteSum)
                    {
                        var diff = new PixelDifference(new Point(x, y), aBuffer[x], bBuffer[x]);
                        differences.Add(diff);

                        float percentageDiff = (float)d / 4.0f / 255.0f;
                        totalDifference += percentageDiff;
                    }
                }
            }

            float normalizedDifference = totalDifference / ((float)actual.Width * (float)actual.Height);
            
            if (normalizedDifference > this.ImageThreshold)
            {
                return new ImageSimilarityReport(expected, actual, differences);
            }
            else
            {
                return ImageSimilarityReport.Empty;
            }
        }


        private static int GetDifferenceInPixelByteSum(ref Rgba32 expected, ref Rgba32 actual)
        {
            return (int)actual.R - (int)expected.R + (int)actual.G - (int)expected.G + (int)actual.B - (int)expected.B
                   + (int)actual.A - (int)expected.A;
        }
    }
}