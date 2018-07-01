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

            // calculate the cumulative distribution function which will be the cumulative histogram
            int[] cdf = new int[luminanceLevels];
            int histSum = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                histSum += histogram[i];
                cdf[i] = histSum;
            }

            int cdfMin = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                if (histogram[i] != 0)
                {
                    cdfMin = cdf[i];
                    break;
                }
            }

            int[] lut = new int[luminanceLevels];
            for (int i = 0; i < histogram.Length; i++)
            {
                lut[i] = cdf[i] - cdfMin;
            }

            // apply the cdf to each pixel of the image
            double numberOfPixelsMinusCdfMin = (double)(numberOfPixels - cdfMin);
            int luminanceLevelsMinusOne = luminanceLevels - 1;
            for (int y = 0; y < source.Height; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);
                for (int x = 0; x < source.Width; x++)
                {
                    TPixel sourcePixel = row[x];
                    sourcePixel.ToRgb24(ref rgb);
                    int luminance = (int)((.2126F * rgb.R) + (.7152F * rgb.G) + (.0722F * rgb.B));
                    double luminanceEqualized = (lut[luminance] / numberOfPixelsMinusCdfMin) * luminanceLevelsMinusOne;
                    luminanceEqualized = Math.Round(luminanceEqualized);
                    row[x].PackFromRgba32(new Rgba32((byte)luminanceEqualized, (byte)luminanceEqualized, (byte)luminanceEqualized));
                }
            }
        }
    }
}
