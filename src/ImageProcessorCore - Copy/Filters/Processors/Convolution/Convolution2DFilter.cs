// <copyright file="Convolution2DFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a filter that uses two one-dimensional matrices to perform convolution against an image.
    /// </summary>
    public abstract class Convolution2DFilter : ImageProcessor
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public abstract float[,] KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public abstract float[,] KernelY { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            float[,] kernelX = this.KernelX;
            float[,] kernelY = this.KernelY;
            int kernelYHeight = kernelY.GetLength(0);
            int kernelYWidth = kernelY.GetLength(1);
            int kernelXHeight = kernelX.GetLength(0);
            int kernelXWidth = kernelX.GetLength(1);
            int radiusY = kernelYHeight >> 1;
            int radiusX = kernelXWidth >> 1;

            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = sourceBottom - 1;
            int maxX = endX - 1;

            using (PixelAccessor sourcePixels = source.Lock())
            using (PixelAccessor targetPixels = target.Lock())
            {
                Parallel.For(
                startY,
                endY,
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

                                    Color currentColor = sourcePixels[offsetX, offsetY];
                                    float r = currentColor.R;
                                    float g = currentColor.G;
                                    float b = currentColor.B;

                                    if (fy < kernelXHeight)
                                    {
                                        rX += kernelX[fy, fx] * r;
                                        gX += kernelX[fy, fx] * g;
                                        bX += kernelX[fy, fx] * b;
                                    }

                                    if (fx < kernelYWidth)
                                    {
                                        rY += kernelY[fy, fx] * r;
                                        gY += kernelY[fy, fx] * g;
                                        bY += kernelY[fy, fx] * b;
                                    }
                                }
                            }

                            float red = (float)Math.Sqrt((rX * rX) + (rY * rY));
                            float green = (float)Math.Sqrt((gX * gX) + (gY * gY));
                            float blue = (float)Math.Sqrt((bX * bX) + (bY * bY));

                            Color targetColor = targetPixels[x, y];
                            targetPixels[x, y] = new Color(red, green, blue, targetColor.A);
                        }
                        this.OnRowProcessed();
                    }
                });
            }
        }
    }
}
