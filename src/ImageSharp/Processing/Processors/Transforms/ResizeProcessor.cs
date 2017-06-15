// <copyright file="ResizeProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// Provides methods that allow the resizing of images using various algorithms.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ResizeProcessor<TPixel> : ResamplingWeightedProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the resize operation.</param>
        /// <param name="width">The target width.</param>
        /// <param name="height">The target height.</param>
        public ResizeProcessor(IResampler sampler, int width, int height)
            : base(sampler, width, height, new Rectangle(0, 0, width, height))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResizeProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the resize operation.</param>
        /// <param name="width">The target width.</param>
        /// <param name="height">The target height.</param>
        /// <param name="resizeRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the target image object to draw to.
        /// </param>
        public ResizeProcessor(IResampler sampler, int width, int height, Rectangle resizeRectangle)
            : base(sampler, width, height, resizeRectangle)
        {
        }

        /// <inheritdoc/>
        protected override unsafe void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            // Jump out, we'll deal with that later.
            if (source.Width == this.Width && source.Height == this.Height && sourceRectangle == this.ResizeRectangle)
            {
                return;
            }

            int width = this.Width;
            int height = this.Height;
            int sourceX = sourceRectangle.X;
            int sourceY = sourceRectangle.Y;
            int startY = this.ResizeRectangle.Y;
            int endY = this.ResizeRectangle.Bottom;
            int startX = this.ResizeRectangle.X;
            int endX = this.ResizeRectangle.Right;

            int minX = Math.Max(0, startX);
            int maxX = Math.Min(width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(height, endY);

            if (this.Sampler is NearestNeighborResampler)
            {
                // Scaling factors
                float widthFactor = sourceRectangle.Width / (float)this.ResizeRectangle.Width;
                float heightFactor = sourceRectangle.Height / (float)this.ResizeRectangle.Height;

                using (var targetPixels = new PixelAccessor<TPixel>(width, height))
                {
                    Parallel.For(
                        minY,
                        maxY,
                        this.ParallelOptions,
                        y =>
                        {
                            // Y coordinates of source points
                            Span<TPixel> sourceRow = source.GetRowSpan((int)(((y - startY) * heightFactor) + sourceY));
                            Span<TPixel> targetRow = targetPixels.GetRowSpan(y);

                            for (int x = minX; x < maxX; x++)
                            {
                                // X coordinates of source points
                                targetRow[x] = sourceRow[(int)(((x - startX) * widthFactor) + sourceX)];
                            }
                        });

                    // Break out now.
                    source.SwapPixelsBuffers(targetPixels);
                    return;
                }
            }

            // Interpolate the image using the calculated weights.
            // A 2-pass 1D algorithm appears to be faster than splitting a 1-pass 2D algorithm
            // First process the columns. Since we are not using multiple threads startY and endY
            // are the upper and lower bounds of the source rectangle.

            // TODO: Using a transposed variant of 'firstPassPixels' could eliminate the need for the WeightsWindow.ComputeWeightedColumnSum() method, and improve speed!
            using (var targetPixels = new PixelAccessor<TPixel>(width, height))
            {
                using (var firstPassPixels = new Buffer2D<Vector4>(width, source.Height))
                {
                    firstPassPixels.Clear();

                    Parallel.For(
                        0,
                        sourceRectangle.Bottom,
                        this.ParallelOptions,
                        y =>
                            {
                                // TODO: Without Parallel.For() this buffer object could be reused:
                                using (var tempRowBuffer = new Buffer<Vector4>(source.Width))
                                {
                                    Span<Vector4> firstPassRow = firstPassPixels.GetRowSpan(y);
                                    Span<TPixel> sourceRow = source.GetRowSpan(y);
                                    PixelOperations<TPixel>.Instance.ToVector4(sourceRow, tempRowBuffer, sourceRow.Length);

                                    if (this.Compand)
                                    {
                                        for (int x = minX; x < maxX; x++)
                                        {
                                            WeightsWindow window = this.HorizontalWeights.Weights[x - startX];
                                            firstPassRow[x] = window.ComputeExpandedWeightedRowSum(tempRowBuffer, sourceX);
                                        }
                                    }
                                    else
                                    {
                                        for (int x = minX; x < maxX; x++)
                                        {
                                            WeightsWindow window = this.HorizontalWeights.Weights[x - startX];
                                            firstPassRow[x] = window.ComputeWeightedRowSum(tempRowBuffer, sourceX);
                                        }
                                    }
                                }
                            });

                    // Now process the rows.
                    Parallel.For(
                        minY,
                        maxY,
                        this.ParallelOptions,
                        y =>
                        {
                            // Ensure offsets are normalised for cropping and padding.
                            WeightsWindow window = this.VerticalWeights.Weights[y - startY];
                            Span<TPixel> targetRow = targetPixels.GetRowSpan(y);

                            if (this.Compand)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    // Destination color components
                                    Vector4 destination = window.ComputeWeightedColumnSum(firstPassPixels, x, sourceY);
                                    destination = destination.Compress();

                                    ref TPixel pixel = ref targetRow[x];
                                    pixel.PackFromVector4(destination);
                                }
                            }
                            else
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    // Destination color components
                                    Vector4 destination = window.ComputeWeightedColumnSum(firstPassPixels, x, sourceY);

                                    ref TPixel pixel = ref targetRow[x];
                                    pixel.PackFromVector4(destination);
                                }
                            }
                        });
                }

                source.SwapPixelsBuffers(targetPixels);
            }
        }
    }
}