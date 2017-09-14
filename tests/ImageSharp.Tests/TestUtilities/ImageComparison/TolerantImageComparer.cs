namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    using SixLabors.ImageSharp.Advanced;
    using SixLabors.ImageSharp.Helpers;
    using SixLabors.ImageSharp.PixelFormats;

    using SixLabors.Primitives;

    public class TolerantImageComparer : ImageComparer
    {
        public const float DefaultImageThreshold = 1.0f / (100 * 100 * 255);

        public TolerantImageComparer(float imageThreshold, int perPixelManhattanThreshold = 0)
        {
            this.ImageThreshold = imageThreshold;
            this.PerPixelManhattanThreshold = perPixelManhattanThreshold;
        }

        /// <summary>
        /// The maximal tolerated difference represented by a value between 0.0 and 1.0.
        /// Examples of percentage differences on a single pixel:
        /// 1. PixelA = (255,255,255,0) PixelB =(0,0,0,255) leads to 100% difference on a single pixel
        /// 2. PixelA = (255,255,255,0) PixelB =(255,255,255,255) leads to 25% difference on a single pixel
        /// 3. PixelA = (255,255,255,0) PixelB =(128,128,128,128) leads to 50% difference on a single pixel
        /// 
        /// The total differences is the sum of all pixel differences normalized by image dimensions!
        /// The individual distances are calculated using the Manhattan function:
        /// <see>
        ///     <cref>https://en.wikipedia.org/wiki/Taxicab_geometry</cref>
        /// </see>
        /// ImageThresholdInPercents = 1.0/255 means that we allow one byte difference per channel on a 1x1 image
        /// ImageThresholdInPercents = 1.0/(100*100*255) means that we allow only one byte difference per channel on a 100x100 image
        /// </summary>
        public float ImageThreshold { get; }

        /// <summary>
        /// The threshold of the individual pixels before they acumulate towards the overall difference.
        /// For an individual <see cref="Rgba32"/> pixel pair the value is the Manhattan distance of pixels:
        /// <see>
        ///     <cref>https://en.wikipedia.org/wiki/Taxicab_geometry</cref>
        /// </see>
        /// </summary>
        public int PerPixelManhattanThreshold { get; }
        
        public override ImageSimilarityReport<TPixelA, TPixelB> CompareImagesOrFrames<TPixelA, TPixelB>(ImageFrame<TPixelA> expected, ImageFrame<TPixelB> actual)
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
                Span<TPixelA> aSpan = expected.GetPixelRowSpan(y);
                Span<TPixelB> bSpan = actual.GetPixelRowSpan(y);

                PixelOperations<TPixelA>.Instance.ToRgba32(aSpan, aBuffer, width);
                PixelOperations<TPixelB>.Instance.ToRgba32(bSpan, bBuffer, width);

                for (int x = 0; x < width; x++)
                {
                    int d = GetManhattanDistanceInRgbaSpace(ref aBuffer[x], ref bBuffer[x]);

                    if (d > this.PerPixelManhattanThreshold)
                    {
                        var diff = new PixelDifference(new Point(x, y), aBuffer[x], bBuffer[x]);
                        differences.Add(diff);

                        totalDifference += d;
                    }
                }
            }

            float normalizedDifference = totalDifference / ((float)actual.Width * (float)actual.Height);
            normalizedDifference /= 4.0f * 255.0f;
            
            if (normalizedDifference > this.ImageThreshold)
            {
                return new ImageSimilarityReport<TPixelA, TPixelB>(expected, actual, differences, normalizedDifference);
            }
            else
            {
                return ImageSimilarityReport<TPixelA, TPixelB>.Empty;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetManhattanDistanceInRgbaSpace(ref Rgba32 a, ref Rgba32 b)
        {
            return Diff(a.R, b.R) + Diff(a.G, b.G) + Diff(a.B, b.B) + Diff(a.A, b.A);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Diff(byte a, byte b) => Math.Abs(a - b);
    }
}