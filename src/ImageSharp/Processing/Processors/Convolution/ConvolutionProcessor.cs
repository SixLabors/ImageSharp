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
    /// Defines a sampler that uses a 2 dimensional matrix to perform convolution against an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ConvolutionProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConvolutionProcessor{TPixel}"/> class.
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
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            int kernelLength = this.KernelXY.Height;
            int radius = kernelLength >> 1;

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
                         float red = 0;
                         float green = 0;
                         float blue = 0;

                         // Apply each matrix multiplier to the color components for each pixel.
                         for (int fy = 0; fy < kernelLength; fy++)
                         {
                             int fyr = fy - radius;
                             int offsetY = y + fyr;

                             offsetY = offsetY.Clamp(0, maxY);
                             Span<TPixel> sourceOffsetRow = source.GetPixelRowSpan(offsetY);

                             for (int fx = 0; fx < kernelLength; fx++)
                             {
                                 int fxr = fx - radius;
                                 int offsetX = x + fxr;

                                 offsetX = offsetX.Clamp(0, maxX);

                                 var currentColor = sourceOffsetRow[offsetX].ToVector4();
                                 currentColor *= this.KernelXY[fy, fx];

                                 red += currentColor.X;
                                 green += currentColor.Y;
                                 blue += currentColor.Z;
                             }
                         }

                         ref TPixel pixel = ref targetRow[x];
                         pixel.PackFromVector4(new Vector4(red, green, blue, sourceRow[x].ToVector4().W));
                     }
                 });

                source.SwapPixelsBuffers(targetPixels);
            }
        }
    }
}