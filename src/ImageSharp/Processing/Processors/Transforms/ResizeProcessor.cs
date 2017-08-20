﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
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

        /// <summary>
        /// Gets or sets a value indicating whether to compress or expand individual pixel color values on processing.
        /// </summary>
        public bool Compand { get; set; }

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            // We will always be creating the clone even for mutate because thats the way this base processor works
            // ------------
            // For resize we know we are going to populate every pixel with fresh data and we want a different target size so
            // let's manually clone an empty set of images at the correct target and then have the base class processs them in turn.
            var image = new Image<TPixel>(source.Configuration, this.Width, this.Height, source.MetaData.Clone());

            // Now 'clone' the ImageFrames
            foreach (ImageFrame<TPixel> sourceFrame in source.Frames)
            {
                var targetFrame = new ImageFrame<TPixel>(sourceFrame.Configuration, this.Width, this.Height, sourceFrame.MetaData.Clone());
                image.Frames.Add(targetFrame);
            }

            return image;
        }

        /// <inheritdoc/>
        protected override unsafe void OnApply(ImageBase<TPixel> source, ImageBase<TPixel> cloned, Rectangle sourceRectangle)
        {
            // Jump out, we'll deal with that later.
            if (source.Width == cloned.Width && source.Height == cloned.Height && sourceRectangle == this.ResizeRectangle)
            {
                // the cloned will be blank here copy all the pixel data over
                source.Pixels.CopyTo(cloned.Pixels);
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

                Parallel.For(
                    minY,
                    maxY,
                    source.Configuration.ParallelOptions,
                    y =>
                    {
                        // Y coordinates of source points
                        Span<TPixel> sourceRow = source.GetRowSpan((int)(((y - startY) * heightFactor) + sourceY));
                        Span<TPixel> targetRow = cloned.GetRowSpan(y);

                        for (int x = minX; x < maxX; x++)
                        {
                            // X coordinates of source points
                            targetRow[x] = sourceRow[(int)(((x - startX) * widthFactor) + sourceX)];
                        }
                    });

                return;
            }

            // Interpolate the image using the calculated weights.
            // A 2-pass 1D algorithm appears to be faster than splitting a 1-pass 2D algorithm
            // First process the columns. Since we are not using multiple threads startY and endY
            // are the upper and lower bounds of the source rectangle.
            // TODO: Using a transposed variant of 'firstPassPixels' could eliminate the need for the WeightsWindow.ComputeWeightedColumnSum() method, and improve speed!
            using (var firstPassPixels = new Buffer2D<Vector4>(width, source.Height))
            {
                firstPassPixels.Clear();

                Parallel.For(
                    0,
                    sourceRectangle.Bottom,
                    source.Configuration.ParallelOptions,
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
                    source.Configuration.ParallelOptions,
                    y =>
                    {
                        // Ensure offsets are normalised for cropping and padding.
                        WeightsWindow window = this.VerticalWeights.Weights[y - startY];
                        Span<TPixel> targetRow = cloned.GetRowSpan(y);

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
        }
    }
}