// <copyright file="CropProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Threading.Tasks;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Provides methods to allow the cropping of an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class CropProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CropProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="cropRectangle">The target cropped rectangle.</param>
        public CropProcessor(Rectangle cropRectangle)
        {
            this.CropRectangle = cropRectangle;
        }

        /// <summary>
        /// Gets the width.
        /// </summary>
        public Rectangle CropRectangle { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            if (this.CropRectangle == sourceRectangle)
            {
                return;
            }

            int minY = Math.Max(this.CropRectangle.Y, sourceRectangle.Y);
            int maxY = Math.Min(this.CropRectangle.Bottom, sourceRectangle.Bottom);
            int minX = Math.Max(this.CropRectangle.X, sourceRectangle.X);
            int maxX = Math.Min(this.CropRectangle.Right, sourceRectangle.Right);

            using (PixelAccessor<TPixel> targetPixels = new PixelAccessor<TPixel>(this.CropRectangle.Width, this.CropRectangle.Height))
            {
                using (PixelAccessor<TPixel> sourcePixels = source.Lock())
                {
                    Parallel.For(
                        minY,
                        maxY,
                        this.ParallelOptions,
                        y =>
                        {
                            for (int x = minX; x < maxX; x++)
                            {
                                targetPixels[x - minX, y - minY] = sourcePixels[x, y];
                            }
                        });
                }

                source.SwapPixelsBuffers(targetPixels);
            }
        }
    }
}