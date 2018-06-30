// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Contrast
{
    internal class HistogramEqualizationProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            var rgb = default(Rgb24);
            int numberOfPixels = source.Width * source.Height;

            // build the histogram of the grayscale levels
            int luminanceLevels = 256;
            int[] histogram = new int[luminanceLevels];
            for (int y = 0; y < source.Height; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);
                for (int x = 0; x < source.Width; x++)
                {
                    TPixel sourcePixel = row[x];
                    sourcePixel.ToRgb24(ref rgb);

                    // Convert to grayscale using ITU-R Recommendation BT.709 if required
                    int luminance = (int)((.2126F * rgb.R) + (.7152F * rgb.G) + (.0722F * rgb.B));
                    histogram[luminance]++;
                }
            }

            // calculate the cumulative distribution function
            double[] cdf = new double[luminanceLevels];
            double sum = 0.0d;
            for (int i = 0; i < histogram.Length; i++)
            {
                double p = (double)histogram[i] / numberOfPixels;
                sum += p;
                cdf[i] = sum;
            }

            // apply the cdf to each pixel of the image
            for (int y = 0; y < source.Height; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);
                for (int x = 0; x < source.Width; x++)
                {
                    TPixel sourcePixel = row[x];
                    sourcePixel.ToRgb24(ref rgb);
                    int luminance = (int)((.2126F * rgb.R) + (.7152F * rgb.G) + (.0722F * rgb.B));
                    byte luminanceEqualized = (byte)(cdf[luminance] * luminance);

                    row[x].PackFromRgba32(new Rgba32(luminanceEqualized, luminanceEqualized, luminanceEqualized));
                }
            }
        }
    }
}
