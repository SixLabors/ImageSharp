// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Defines a sampler that uses two one-dimensional matrices to perform convolution against an image.
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
        public Convolution2DProcessor(Fast2DArray<float> kernelX, Fast2DArray<float> kernelY)
        {
            this.KernelX = kernelX;
            this.KernelY = kernelY;
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public Fast2DArray<float> KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public Fast2DArray<float> KernelY { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            int kernelYHeight = this.KernelY.Height;
            int kernelYWidth = this.KernelY.Width;
            int kernelXHeight = this.KernelX.Height;
            int kernelXWidth = this.KernelX.Width;
            int radiusY = kernelYHeight >> 1;
            int radiusX = kernelXWidth >> 1;

            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = endY - 1;
            int maxX = endX - 1;

            using (var targetPixels = new PixelAccessor<TPixel>(source.Width, source.Height))
            {
                source.CopyTo(targetPixels);

                Parallel.For(
                    startY,
                    endY,
                    configuration.ParallelOptions,
                    y =>
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
                                    var currentColor = sourceOffsetRow[offsetX].ToVector4();

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

                            float red = (float)Math.Sqrt((rX * rX) + (rY * rY));
                            float green = (float)Math.Sqrt((gX * gX) + (gY * gY));
                            float blue = (float)Math.Sqrt((bX * bX) + (bY * bY));

                            ref TPixel pixel = ref targetRow[x];
                            pixel.PackFromVector4(new Vector4(red, green, blue, sourceRow[x].ToVector4().W));
                        }
                    });

                source.SwapPixelsBuffers(targetPixels);
            }
        }
    }
}