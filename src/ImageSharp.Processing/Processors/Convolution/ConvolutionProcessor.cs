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
        public ConvolutionProcessor(Fast2DArray<float> kernelXY)
        {
            this.KernelXY = kernelXY;
        }

        /// <summary>
        /// Gets the 2d gradient operator.
        /// </summary>
        public Fast2DArray<float> KernelXY { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            int kernelLength = this.KernelXY.Height;
            int radius = kernelLength >> 1;

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
                            float red = 0;
                            float green = 0;
                            float blue = 0;

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
                                    currentColor *= this.KernelXY[fy, fx];

                                    red += currentColor.X;
                                    green += currentColor.Y;
                                    blue += currentColor.Z;
                                }
                            }

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