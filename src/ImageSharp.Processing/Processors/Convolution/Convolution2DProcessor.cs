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
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Convolution2DProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="kernelX">The horizontal gradient operator.</param>
        /// <param name="kernelY">The vertical gradient operator.</param>
        public Convolution2DProcessor(float[][] kernelX, float[][] kernelY)
        {
            this.KernelX = kernelX;
            this.KernelY = kernelY;
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public float[][] KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public float[][] KernelY { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            int kernelYHeight = this.KernelY.Length;
            int kernelYWidth = this.KernelY[0].Length;
            int kernelXHeight = this.KernelX.Length;
            int kernelXWidth = this.KernelX[0].Length;
            int radiusY = kernelYHeight >> 1;
            int radiusX = kernelXWidth >> 1;

            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = endY - 1;
            int maxX = endX - 1;

            TColor[] target = new TColor[source.Width * source.Height];
            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (PixelAccessor<TColor> targetPixels = target.Lock<TColor>(source.Width, source.Height))
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
                                float r = currentColor.X;
                                float g = currentColor.Y;
                                float b = currentColor.Z;

                                if (fy < kernelXHeight)
                                {
                                    rX += this.KernelX[fy][fx] * r;
                                    gX += this.KernelX[fy][fx] * g;
                                    bX += this.KernelX[fy][fx] * b;
                                }

                                if (fx < kernelYWidth)
                                {
                                    rY += this.KernelY[fy][fx] * r;
                                    gY += this.KernelY[fy][fx] * g;
                                    bY += this.KernelY[fy][fx] * b;
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

            source.SetPixels(source.Width, source.Height, target);
        }
    }
}