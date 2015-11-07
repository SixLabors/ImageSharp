// <copyright file="Convolution2PassFilter.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a filter that uses a matrix to perform convolution across two dimensions against an image.
    /// </summary>
    public abstract class Convolution2PassFilter : ParallelImageProcessor
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
        protected override void Apply(
            ImageBase target,
            ImageBase source,
            Rectangle targetRectangle,
            Rectangle sourceRectangle,
            int startY,
            int endY)
        {
            float[,] kernelX = this.KernelX;
            float[,] kernelY = this.KernelY;

            ImageBase firstPass = new Image(source.Width, source.Height);
            this.ApplyConvolution(firstPass, source, sourceRectangle, startY, endY, kernelX);
            this.ApplyConvolution(target, firstPass, sourceRectangle, startY, endY, kernelY);
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageBase"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="startY">The index of the row within the source image to start processing.</param>
        /// <param name="endY">The index of the row within the source image to end processing.</param>
        /// <param name="kernel">The kernel operator.</param>
        private void ApplyConvolution(
            ImageBase target,
            ImageBase source,
            Rectangle sourceRectangle,
            int startY,
            int endY,
            float[,] kernel)
        {
            int kernelHeight = kernel.GetLength(0);
            int kernelWidth = kernel.GetLength(1);
            int radiusY = kernelHeight >> 1;
            int radiusX = kernelWidth >> 1;

            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = sourceBottom - 1;
            int maxX = endX - 1;

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

                        // Apply each matrix multiplier to the color components for each pixel.
                        for (int fy = 0; fy < kernelHeight; fy++)
                            {
                                int fyr = fy - radiusY;
                                int offsetY = y + fyr;

                                offsetY = offsetY.Clamp(0, maxY);

                                for (int fx = 0; fx < kernelWidth; fx++)
                                {
                                    int fxr = fx - radiusX;
                                    int offsetX = x + fxr;

                                    offsetX = offsetX.Clamp(0, maxX);

                                    Color currentColor = source[offsetX, offsetY];
                                    float r = currentColor.R;
                                    float g = currentColor.G;
                                    float b = currentColor.B;

                                    rX += kernel[fy, fx] * r;
                                    gX += kernel[fy, fx] * g;
                                    bX += kernel[fy, fx] * b;
                                }
                            }

                            float red = rX;
                            float green = gX;
                            float blue = bX;

                            Color targetColor = target[x, y];
                            target[x, y] = new Color(red, green, blue, targetColor.A);
                        }
                    }
                });
        }
    }
}