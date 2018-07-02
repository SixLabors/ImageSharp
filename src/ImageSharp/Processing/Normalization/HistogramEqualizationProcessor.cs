// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Normalization
{
    /// <summary>
    /// Applies a global histogram equalization to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class HistogramEqualizationProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            var rgb48 = default(Rgb48);
            var rgb24 = default(Rgb24);
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
            int numberOfPixels = source.Width * source.Height;
            bool is16bitPerChannel = typeof(TPixel) == typeof(Rgb48) || typeof(TPixel) == typeof(Rgba64);

            // build the histogram of the grayscale levels
            int luminanceLevels = is16bitPerChannel ? 65536 : 256;
            Span<int> histogram = memoryAllocator.Allocate<int>(luminanceLevels, clear: true).GetSpan();
            for (int y = 0; y < source.Height; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);
                for (int x = 0; x < source.Width; x++)
                {
                    TPixel sourcePixel = row[x];
                    int luminance = this.GetLuminance(sourcePixel, is16bitPerChannel, ref rgb24, ref rgb48);
                    histogram[luminance]++;
                }
            }

            // calculate the cumulative distribution function (which will be the cumulative histogram)
            Span<int> cdf = memoryAllocator.Allocate<int>(luminanceLevels, clear: true).GetSpan();
            int histSum = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                histSum += histogram[i];
                cdf[i] = histSum;
            }

            // get the first none zero value of the cumulative histogram
            int cdfMin = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                if (histogram[i] != 0)
                {
                    cdfMin = cdf[i];
                    break;
                }
            }

            Span<int> lut = memoryAllocator.Allocate<int>(luminanceLevels, clear: true).GetSpan();
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

                    int luminance = this.GetLuminance(sourcePixel, is16bitPerChannel, ref rgb24, ref rgb48);
                    double luminanceEqualized = (lut[luminance] / numberOfPixelsMinusCdfMin) * luminanceLevelsMinusOne;
                    luminanceEqualized = Math.Round(luminanceEqualized);

                    if (is16bitPerChannel)
                    {
                        row[x].PackFromRgb48(new Rgb48((ushort)luminanceEqualized, (ushort)luminanceEqualized, (ushort)luminanceEqualized));
                    }
                    else
                    {
                        row[x].PackFromRgba32(new Rgba32((byte)luminanceEqualized, (byte)luminanceEqualized, (byte)luminanceEqualized));
                    }
                }
            }
        }

        /// <summary>
        /// Convert the pixel values to grayscale using ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="sourcePixel">The pixel to get the luminance from</param>
        /// <param name="is16bitPerChannel">Flag indicates, if its 16 bits per channel, otherwise its 8</param>
        /// <param name="rgb24">Will store the pixel values in case of 8 bit per channel</param>
        /// <param name="rgb48">Will store the pixel values in case of 16 bit per channel</param>
        private int GetLuminance(TPixel sourcePixel, bool is16bitPerChannel, ref Rgb24 rgb24, ref Rgb48 rgb48)
        {
            // Convert to grayscale using ITU-R Recommendation BT.709
            int luminance;
            if (is16bitPerChannel)
            {
                sourcePixel.ToRgb48(ref rgb48);
                luminance = (int)((.2126F * rgb48.R) + (.7152F * rgb48.G) + (.0722F * rgb48.B));
            }
            else
            {
                sourcePixel.ToRgb24(ref rgb24);
                luminance = (int)((.2126F * rgb24.R) + (.7152F * rgb24.G) + (.0722F * rgb24.B));
            }

            return luminance;
        }
    }
}
