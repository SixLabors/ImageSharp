// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison
{
    public class TolerantImageComparer : ImageComparer
    {
        // 1% of all pixels in a 100*100 pixel area are allowed to have a difference of 1 unit
        // 257 = (1 / 255) * 65535.
        public const float DefaultImageThreshold = 257F / (100 * 100 * 65535);

        /// <summary>
        /// Individual manhattan pixel difference is only added to total image difference when the individual difference is over 'perPixelManhattanThreshold'.
        /// </summary>
        /// <param name="imageThreshold">The maximal tolerated difference represented by a value between 0.0 and 1.0 scaled to 0 and 65535.</param>
        /// <param name="perPixelManhattanThreshold">Gets the threshold of the individual pixels before they accumulate towards the overall difference.</param>
        public TolerantImageComparer(float imageThreshold, int perPixelManhattanThreshold = 0)
        {
            Guard.MustBeGreaterThanOrEqualTo(imageThreshold, 0, nameof(imageThreshold));

            this.ImageThreshold = imageThreshold;
            this.PerPixelManhattanThreshold = perPixelManhattanThreshold;
        }

        /// <summary>
        /// <para>
        /// Gets the maximal tolerated difference represented by a value between 0.0 and 1.0 scaled to 0 and 65535.
        /// Examples of percentage differences on a single pixel:
        /// 1. PixelA = (65535,65535,65535,0) PixelB =(0,0,0,65535) leads to 100% difference on a single pixel
        /// 2. PixelA = (65535,65535,65535,0) PixelB =(65535,65535,65535,65535) leads to 25% difference on a single pixel
        /// 3. PixelA = (65535,65535,65535,0) PixelB =(32767,32767,32767,32767) leads to 50% difference on a single pixel
        /// </para>
        /// <para>
        /// The total differences is the sum of all pixel differences normalized by image dimensions!
        /// The individual distances are calculated using the Manhattan function:
        /// <see>
        ///     <cref>https://en.wikipedia.org/wiki/Taxicab_geometry</cref>
        /// </see>
        /// ImageThresholdInPercents = 1/255 =  257/65535 means that we allow one unit difference per channel on a 1x1 image
        /// ImageThresholdInPercents = 1/(100*100*255) = 257/(100*100*65535) means that we allow only one unit difference per channel on a 100x100 image
        /// </para>
        /// </summary>
        public float ImageThreshold { get; }

        /// <summary>
        /// Gets the threshold of the individual pixels before they accumulate towards the overall difference.
        /// For an individual <see cref="Rgba64"/> pixel pair the value is the Manhattan distance of pixels:
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

            // TODO: Comparing through Rgba64 may not robust enough because of the existence of super high precision pixel types.
            var aBuffer = new Rgba64[width];
            var bBuffer = new Rgba64[width];

            float totalDifference = 0F;

            var differences = new List<PixelDifference>();
            Configuration configuration = expected.GetConfiguration();

            for (int y = 0; y < actual.Height; y++)
            {
                Span<TPixelA> aSpan = expected.GetPixelRowSpan(y);
                Span<TPixelB> bSpan = actual.GetPixelRowSpan(y);

                PixelOperations<TPixelA>.Instance.ToRgba64(configuration, aSpan, aBuffer);
                PixelOperations<TPixelB>.Instance.ToRgba64(configuration, bSpan, bBuffer);

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

            float normalizedDifference = totalDifference / (actual.Width * (float)actual.Height);
            normalizedDifference /= 4F * 65535F;

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
        private static int GetManhattanDistanceInRgbaSpace(ref Rgba64 a, ref Rgba64 b)
        {
            return Diff(a.R, b.R) + Diff(a.G, b.G) + Diff(a.B, b.B) + Diff(a.A, b.A);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Diff(ushort a, ushort b) => Math.Abs(a - b);
    }
}
