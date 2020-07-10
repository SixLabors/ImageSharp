// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// A pixel sampling strategy that enumerates a limited amount of rows from different frames,
    /// if the total number of pixels is over a threshold.
    /// </summary>
    public class DefaultPixelSamplingStrategy : IPixelSamplingStrategy
    {
        // TODO: This value shall be determined by benchmarking.
        // A smaller value should likely work well, providing better perf.
        private const int DefaultMaximumPixels = 4096 * 4096;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultPixelSamplingStrategy"/> class.
        /// </summary>
        public DefaultPixelSamplingStrategy()
            : this(DefaultMaximumPixels, 0.1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultPixelSamplingStrategy"/> class.
        /// </summary>
        /// <param name="maximumPixels">The maximum number of pixels to process.</param>
        /// <param name="minimumScanRatio">always scan at least this portion of total pixels within the image.</param>
        public DefaultPixelSamplingStrategy(int maximumPixels, double minimumScanRatio)
        {
            Guard.MustBeGreaterThan(maximumPixels, 0, nameof(maximumPixels));
            this.MaximumPixels = maximumPixels;
            this.MinimumScanRatio = minimumScanRatio;
        }

        /// <summary>
        /// Gets the maximum number of pixels to process. (The threshold.)
        /// </summary>
        public long MaximumPixels { get; }

        /// <summary>
        /// Gets a value indicating: always scan at least this portion of total pixels within the image.
        /// The default is 0.1 (10%).
        /// </summary>
        public double MinimumScanRatio { get; }

        /// <inheritdoc />
        public IEnumerable<Buffer2DRegion<TPixel>> EnumeratePixelRegions<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            long maximumPixels = Math.Min(this.MaximumPixels, (long)image.Width * image.Height * image.Frames.Count);
            long maxNumberOfRows = maximumPixels / image.Width;
            long totalNumberOfRows = (long)image.Height * image.Frames.Count;

            if (totalNumberOfRows <= maxNumberOfRows)
            {
                // Enumerate all pixels
                foreach (ImageFrame<TPixel> frame in image.Frames)
                {
                    yield return frame.PixelBuffer.GetRegion();
                }
            }
            else
            {
                double r = maxNumberOfRows / (double)totalNumberOfRows;

                // Use a rough approximation to make sure we don't leave out large contiguous regions:
                if (maxNumberOfRows > 200)
                {
                    r = Math.Round(r, 2);
                }
                else
                {
                    r = Math.Round(r, 1);
                }

                r = Math.Max(this.MinimumScanRatio, r); // always visit the minimum defined portion of the image.

                var ratio = new Rational(r);

                int denom = (int)ratio.Denominator;
                int num = (int)ratio.Numerator;

                for (int pos = 0; pos < totalNumberOfRows; pos++)
                {
                    int subPos = pos % denom;
                    if (subPos < num)
                    {
                        yield return GetRow(pos);
                    }
                }

                Buffer2DRegion<TPixel> GetRow(int pos)
                {
                    int frameIdx = pos / image.Height;
                    int y = pos % image.Height;
                    return image.Frames[frameIdx].PixelBuffer.GetRegion(0, y, image.Width, 1);
                }
            }
        }
    }
}
