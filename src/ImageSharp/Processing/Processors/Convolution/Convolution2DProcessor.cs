// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines a processor that uses two one-dimensional matrices to perform convolution against an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class Convolution2DProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Convolution2DProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="kernelX">The horizontal gradient operator.</param>
        /// <param name="kernelY">The vertical gradient operator.</param>
        public Convolution2DProcessor(DenseMatrix<float> kernelX, DenseMatrix<float> kernelY)
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
        protected override void OnFrameApply(
            ImageFrame<TPixel> source,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            int kernelYHeight = this.KernelY.Rows;
            int kernelYWidth = this.KernelY.Columns;
            int kernelXHeight = this.KernelX.Rows;
            int kernelXWidth = this.KernelX.Columns;
            int radiusY = kernelYHeight >> 1;
            int radiusX = kernelXWidth >> 1;

            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = endY - 1;
            int maxX = endX - 1;

            using (Buffer2D<TPixel> targetPixels =
                configuration.MemoryAllocator.Allocate2D<TPixel>(source.Width, source.Height))
            {
                source.CopyTo(targetPixels);

                var workingRectangle = Rectangle.FromLTRB(startX, startY, endX, endY);

                ParallelHelper.IterateRows(
                    workingRectangle,
                    configuration,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
                                Span<TPixel> targetRow = targetPixels.GetRowSpan(y);

                                for (int x = startX; x < endX; x++)
                                {
                                    float rX = 0;
                                    float gX = 0;
                                    float bX = 0;
                                    float rY = 0;
                                    float gY = 0;
                                    float bY = 0;

                                    // Apply each matrix multiplier to the color components for each pixel.
                                    for (int fy = 0; fy < kernelYHeight; fy++)
                                    {
                                        int fyr = fy - radiusY;
                                        int offsetY = y + fyr;

                                        offsetY = offsetY.Clamp(0, maxY);
                                        Span<TPixel> sourceOffsetRow = source.GetPixelRowSpan(offsetY);

                                        for (int fx = 0; fx < kernelXWidth; fx++)
                                        {
                                            int fxr = fx - radiusX;
                                            int offsetX = x + fxr;

                                            offsetX = offsetX.Clamp(0, maxX);
                                            Vector4 currentColor = sourceOffsetRow[offsetX].ToVector4().Premultiply();

                                            if (fy < kernelXHeight)
                                            {
                                                Vector4 kx = this.KernelX[fy, fx] * currentColor;
                                                rX += kx.X;
                                                gX += kx.Y;
                                                bX += kx.Z;
                                            }

                                            if (fx < kernelYWidth)
                                            {
                                                Vector4 ky = this.KernelY[fy, fx] * currentColor;
                                                rY += ky.X;
                                                gY += ky.Y;
                                                bY += ky.Z;
                                            }
                                        }
                                    }

                                    float red = MathF.Sqrt((rX * rX) + (rY * rY));
                                    float green = MathF.Sqrt((gX * gX) + (gY * gY));
                                    float blue = MathF.Sqrt((bX * bX) + (bY * bY));

                                    ref TPixel pixel = ref targetRow[x];
                                    pixel.PackFromVector4(
                                        new Vector4(red, green, blue, sourceRow[x].ToVector4().W).UnPremultiply());
                                }
                            }
                        });

                Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
            }
        }
    }
}