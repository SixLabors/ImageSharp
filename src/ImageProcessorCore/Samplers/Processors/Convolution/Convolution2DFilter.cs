// <copyright file="Convolution2DFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a filter that uses two one-dimensional matrices to perform convolution against an image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class Convolution2DFilter<TColor, TPacked> : ImageSampler<TColor, TPacked>
        where TColor : struct, IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Convolution2DFilter{TColor,TPacked}"/> class.
        /// </summary>
        /// <param name="kernelX">The horizontal gradient operator.</param>
        /// <param name="kernelY">The vertical gradient operator.</param>
        public Convolution2DFilter(float[][] kernelX, float[][] kernelY)
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
        public override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int kernelYHeight = this.KernelY.Length;
            int kernelYWidth = this.KernelY[0].Length;
            int kernelXHeight = this.KernelX.Length;
            int kernelXWidth = this.KernelX[0].Length;
            int radiusY = kernelYHeight >> 1;
            int radiusX = kernelXWidth >> 1;

            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = sourceBottom - 1;
            int maxX = endX - 1;

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
            {
                Parallel.For(
                startY,
                endY,
                this.ParallelOptions,
                y =>
                {
                    if (y >= sourceY && y < sourceBottom)
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

                            Vector4 targetColor = targetPixels[x, y].ToVector4();
                            TColor packed = default(TColor);
                            packed.PackFromVector4(new Vector4(red, green, blue, targetColor.Z));
                            targetPixels[x, y] = packed;
                        }
                    }
                });
            }
        }
    }
}