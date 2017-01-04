// <copyright file="CropProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods to allow the cropping of an image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class CropProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CropProcessor{TColor}"/> class.
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
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            if (this.CropRectangle == sourceRectangle)
            {
                return;
            }

            int minY = Math.Max(this.CropRectangle.Y, sourceRectangle.Y);
            int maxY = Math.Min(this.CropRectangle.Bottom, sourceRectangle.Bottom);
            int minX = Math.Max(this.CropRectangle.X, sourceRectangle.X);
            int maxX = Math.Min(this.CropRectangle.Right, sourceRectangle.Right);

            TColor[] target = new TColor[this.CropRectangle.Width * this.CropRectangle.Height];

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            using (PixelAccessor<TColor> targetPixels = target.Lock<TColor>(this.CropRectangle.Width, this.CropRectangle.Height))
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

            source.SetPixels(this.CropRectangle.Width, this.CropRectangle.Height, target);
        }
    }
}