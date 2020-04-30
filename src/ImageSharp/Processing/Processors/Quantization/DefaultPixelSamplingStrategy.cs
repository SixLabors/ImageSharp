// Copyright (c) Six Labors and contributors.
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
        // (Maximum quality while decoding time is still tolerable.)
        private const int DefaultMaximumPixels = 8192 * 8192;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultPixelSamplingStrategy"/> class.
        /// </summary>
        public DefaultPixelSamplingStrategy()
            : this(DefaultMaximumPixels)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultPixelSamplingStrategy"/> class.
        /// </summary>
        /// <param name="maximumPixels">The maximum number of pixels to process.</param>
        public DefaultPixelSamplingStrategy(int maximumPixels)
        {
            Guard.MustBeGreaterThan(maximumPixels, 0, nameof(maximumPixels));
            this.MaximumPixels = maximumPixels;
        }

        /// <summary>
        /// Gets the maximum number of pixels to process. (The threshold.)
        /// </summary>
        public long MaximumPixels { get; }

        /// <inheritdoc />
        public IEnumerable<BufferRegion<TPixel>> EnumeratePixelRegions<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            long maximumPixels = Math.Min(MaximumPixels, (long)image.Width * image.Height * image.Frames.Count);
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

                r = Math.Round(r, 1); // Use a rough approximation to make sure we don't leave out large contiguous regions:
                r = Math.Max(0.1, r); // always visit at least 10% of the image

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

                BufferRegion<TPixel> GetRow(int pos)
                {
                    int frameIdx = pos / image.Height;
                    int y = pos % image.Height;
                    return image.Frames[frameIdx].PixelBuffer.GetRegion(0, y, image.Width, 1);
                }
            }
        }
    }
}
