using System;
using System.Collections.Generic;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Normalization
{
    internal class AdaptiveHistEqualizationProcessor<TPixel> : HistogramEqualizationProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        public AdaptiveHistEqualizationProcessor(int luminanceLevels)
            : base(luminanceLevels)
        {
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            MemoryAllocator memoryAllocator = configuration.MemoryAllocator;
            int numberOfPixels = source.Width * source.Height;
            Span<TPixel> pixels = source.GetPixelSpan();

            int gridSize = 64;
            int halfGridSize = gridSize / 2;
            int[] histogram = new int[this.LuminanceLevels];
            int[] histogramCopy = new int[this.LuminanceLevels];
            int[] cdf = new int[this.LuminanceLevels];
            using (Buffer2D<TPixel> targetPixels = configuration.MemoryAllocator.Allocate2D<TPixel>(source.Width, source.Height))
            {
                for (int y = halfGridSize; y < source.Height - halfGridSize; y++)
                {
                    var pixelsGrid = new List<TPixel>();
                    int x = halfGridSize;
                    for (int dy = y - halfGridSize; dy < (y + halfGridSize); dy++)
                    {
                        Span<TPixel> rowSpan = source.GetPixelRowSpan(dy);
                        for (int dx = x - halfGridSize; dx < (x + halfGridSize); dx++)
                        {
                            pixelsGrid.Add(rowSpan[dx]);
                        }
                    }

                    this.CleanArray(histogram);
                    this.CleanArray(cdf);
                    this.CalcHistogram(pixelsGrid, histogram, this.LuminanceLevels);

                    for (x = halfGridSize + 1; x < source.Width - halfGridSize; x++)
                    {
                        // remove left most column from the histogram
                        var leftMostColumnPixels = new List<TPixel>();
                        int dx = x - halfGridSize - 1;
                        for (int dy = y - halfGridSize; dy < (y + halfGridSize); dy++)
                        {
                            leftMostColumnPixels.Add(pixels[(dy * source.Width) + dx]);
                        }

                        this.RemovePixelsFromHistogram(leftMostColumnPixels, histogram, this.LuminanceLevels);

                        // add right column from the histogram
                        dx = x + halfGridSize - 1;
                        var rightMostColumnPixels = new List<TPixel>();
                        for (int dy = y - halfGridSize; dy < (y + halfGridSize); dy++)
                        {
                            rightMostColumnPixels.Add(pixels[(dy * source.Width) + dx]);
                        }

                        this.AddPixelsTooHistogram(rightMostColumnPixels, histogram, this.LuminanceLevels);

                        histogram.AsSpan().CopyTo(histogramCopy);
                        this.ClipHistogram(histogramCopy, 5, gridSize);
                        int cdfMin = this.CalculateCdf(cdf, histogramCopy);
                        double numberOfPixelsMinusCdfMin = (double)(pixelsGrid.Count - cdfMin);

                        int luminance = this.GetLuminance(pixels[(y * source.Width) + x], this.LuminanceLevels);
                        double luminanceEqualized = cdf[luminance] / numberOfPixelsMinusCdfMin;

                        targetPixels[x, y].PackFromVector4(new Vector4((float)luminanceEqualized));

                        this.CleanArray(cdf);
                    }
                }

                Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
            }
        }

        private void ClipHistogram(Span<int> histogram, int contrastLimit, int gridSize)
        {
            int clipLimit = (contrastLimit * (gridSize * gridSize)) / this.LuminanceLevels;

            int sumOverClip = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                if (histogram[i] > clipLimit)
                {
                    sumOverClip += histogram[i] - clipLimit;
                    histogram[i] = clipLimit;
                }
            }

            int addToEachBin = (int)Math.Floor(sumOverClip / (double)this.LuminanceLevels);
            for (int i = 0; i < histogram.Length; i++)
            {
                histogram[i] += addToEachBin;
            }
        }

        private void AddPixelsTooHistogram(List<TPixel> greyValues, Span<int> histogram, int luminanceLevels)
        {
            for (int i = 0; i < greyValues.Count; i++)
            {
                int luminance = this.GetLuminance(greyValues[i], luminanceLevels);
                histogram[luminance]++;
            }
        }

        private void RemovePixelsFromHistogram(List<TPixel> greyValues, Span<int> histogram, int luminanceLevels)
        {
            for (int i = 0; i < greyValues.Count; i++)
            {
                int luminance = this.GetLuminance(greyValues[i], luminanceLevels);
                histogram[luminance]--;
            }
        }

        private void CalcHistogram(List<TPixel> greyValues, Span<int> histogram, int luminanceLevels)
        {
            for (int i = 0; i < greyValues.Count; i++)
            {
                int luminance = this.GetLuminance(greyValues[i], luminanceLevels);
                histogram[luminance]++;
            }
        }

        private void CleanArray(Span<int> array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = 0;
            }
        }
    }
}
