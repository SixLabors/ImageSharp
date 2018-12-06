// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
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
                source.CopyTo(firstPassPixels);

                var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());
                this.ApplyConvolution(firstPassPixels, source.PixelBuffer, interest, this.KernelX, configuration);
                this.ApplyConvolution(source.PixelBuffer, firstPassPixels, interest, this.KernelY, configuration);
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
            in DenseMatrix<float> kernel,
            Configuration configuration)
        {
            DenseMatrix<float> matrix = kernel;
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = endY - 1;
            int maxX = endX - 1;

            var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);
            int width = workingRectangle.Width;

            ParallelHelper.IterateRowsWithTempBuffer<Vector4>(
                workingRectangle,
                configuration,
                (rows, vectorBuffer) =>
                    {
                        Span<Vector4> vectorSpan = vectorBuffer.Span;
                        int length = vectorSpan.Length;

                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> targetRowSpan = targetPixels.GetRowSpan(y).Slice(startX);
                            PixelOperations<TPixel>.Instance.ToVector4(configuration, targetRowSpan.Slice(0, length), vectorSpan);

                            for (int x = 0; x < width; x++)
                            {
                                DenseMatrixUtils.Convolve(in matrix, sourcePixels, vectorSpan, y, x, maxY, maxX, startX);
                            }

                            PixelOperations<TPixel>.Instance.FromVector4(configuration, vectorSpan.Slice(0, length), targetRowSpan);
                        }
                    });
        }
    }
}