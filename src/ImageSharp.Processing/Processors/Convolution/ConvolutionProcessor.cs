// <copyright file="ConvolutionProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a sampler that uses a 2 dimensional matrix to perform convolution against an image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class ConvolutionProcessor<TColor> : ImageProcessor<TColor>
    where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConvolutionProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="kernelXY">The 2d gradient operator.</param>
        public ConvolutionProcessor(float[][] kernelXY)
        {
            this.KernelXY = kernelXY;
        }

        /// <summary>
        /// Gets the 2d gradient operator.
        /// </summary>
        public virtual float[][] KernelXY { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            float[][] kernelX = this.KernelXY;
            int kernelLength = kernelX.GetLength(0);
            int radius = kernelLength >> 1;

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

                        // Apply each matrix multiplier to the color components for each pixel.
                        for (int fy = 0; fy < kernelLength; fy++)
                        {
                            int fyr = fy - radius;
                            int offsetY = y + fyr;

                            offsetY = offsetY.Clamp(0, maxY);

                            for (int fx = 0; fx < kernelLength; fx++)
                            {
                                int fxr = fx - radius;
                                int offsetX = x + fxr;

                                offsetX = offsetX.Clamp(0, maxX);

                                Vector4 currentColor = sourcePixels[offsetX, offsetY].ToVector4();
                                float r = currentColor.X;
                                float g = currentColor.Y;
                                float b = currentColor.Z;

                                rX += kernelX[fy][fx] * r;
                                gX += kernelX[fy][fx] * g;
                                bX += kernelX[fy][fx] * b;
                            }
                        }

                        float red = rX;
                        float green = gX;
                        float blue = bX;

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