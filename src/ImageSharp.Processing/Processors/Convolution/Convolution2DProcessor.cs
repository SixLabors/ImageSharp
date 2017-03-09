// <copyright file="Convolution2DProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a sampler that uses two one-dimensional matrices to perform convolution against an image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class Convolution2DProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Convolution2DProcessor{TColor}"/> class.
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
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
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

            using (PixelAccessor<TColor> targetPixels = new PixelAccessor<TColor>(source.Width, source.Height))
            {
                using (PixelAccessor<TColor> sourcePixels = source.Lock())
                {
                    Parallel.For(
                    startY,
                    endY,
                    this.ParallelOptions,
                    y =>
                    {
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

                                for (int fx = 0; fx < kernelXWidth; fx++)
                                {
                                    int fxr = fx - radiusX;
                                    int offsetX = x + fxr;

                                    offsetX = offsetX.Clamp(0, maxX);

                                    Vector4 currentColor = sourcePixels[offsetX, offsetY].ToVector4();

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

                            TColor packed = default(TColor);
                            packed.PackFromVector4(new Vector4(red, green, blue, sourcePixels[x, y].ToVector4().W));
                            targetPixels[x, y] = packed;
                        }
                    });
                }

                source.SwapPixelsBuffers(targetPixels);
            }
        }
    }
}