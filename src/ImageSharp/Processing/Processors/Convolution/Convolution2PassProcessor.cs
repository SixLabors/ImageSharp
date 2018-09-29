// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines a processor that uses two one-dimensional matrices to perform two-pass convolution against an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class Convolution2PassProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Convolution2PassProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="kernelX">The horizontal gradient operator.</param>
        /// <param name="kernelY">The vertical gradient operator.</param>
        public Convolution2PassProcessor(DenseMatrix<float> kernelX, DenseMatrix<float> kernelY)
        {
            this.KernelX = kernelX;
            this.KernelY = kernelY;
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelY { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            using (Buffer2D<TPixel> firstPassPixels = configuration.MemoryAllocator.Allocate2D<TPixel>(source.Size()))
            {
                this.ApplyConvolution(firstPassPixels, source.PixelBuffer, source.Bounds(), this.KernelX, configuration);
                this.ApplyConvolution(source.PixelBuffer, firstPassPixels, sourceRectangle, this.KernelY, configuration);
            }
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageFrame{TPixel}"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="targetPixels">The target pixels to apply the process to.</param>
        /// <param name="sourcePixels">The source pixels. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="kernel">The kernel operator.</param>
        /// <param name="configuration">The <see cref="Configuration"/></param>
        private void ApplyConvolution(
            Buffer2D<TPixel> targetPixels,
            Buffer2D<TPixel> sourcePixels,
            Rectangle sourceRectangle,
            DenseMatrix<float> kernel, // TODO: Can't use 'in' as pass by ref to lambda expression.
            Configuration configuration)
        {
            int kernelHeight = kernel.Rows;
            int kernelWidth = kernel.Columns;
            int radiusY = kernelHeight >> 1;
            int radiusX = kernelWidth >> 1;

            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = endY - 1;
            int maxX = endX - 1;

            var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);

            ParallelHelper.IterateRows(
                workingRectangle,
                configuration,
                rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> targetRow = targetPixels.GetRowSpan(y);

                            for (int x = startX; x < endX; x++)
                            {
                                Vector4 destination = default;

                                // Apply each matrix multiplier to the color components for each pixel.
                                for (int fy = 0; fy < kernelHeight; fy++)
                                {
                                    int fyr = fy - radiusY;
                                    int offsetY = y + fyr;

                                    offsetY = offsetY.Clamp(0, maxY);
                                    Span<TPixel> row = sourcePixels.GetRowSpan(offsetY);

                                    for (int fx = 0; fx < kernelWidth; fx++)
                                    {
                                        int fxr = fx - radiusX;
                                        int offsetX = x + fxr;

                                        offsetX = offsetX.Clamp(0, maxX);

                                        Vector4 currentColor = row[offsetX].ToVector4().Premultiply();
                                        destination += kernel[fy, fx] * currentColor;
                                    }
                                }

                                ref TPixel pixel = ref targetRow[x];
                                pixel.PackFromVector4(destination.UnPremultiply());
                            }
                        }
                    });
        }
    }
}