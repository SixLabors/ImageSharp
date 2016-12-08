// <copyright file="CropProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processors
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods to allow the cropping of an image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class CropProcessor<TColor, TPacked> : ImageFilteringProcessor<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CropProcessor{TColor,TPacked}"/> class.
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

        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle, int startY, int endY)
        {
            if (this.CropRectangle == sourceRectangle)
            {
                return;
            }

            int minY = Math.Max(this.CropRectangle.Y, startY);
            int maxY = Math.Min(this.CropRectangle.Bottom, endY);
            int minX = Math.Max(this.CropRectangle.X, sourceRectangle.X);
            int maxX = Math.Min(this.CropRectangle.Right, sourceRectangle.Right);

            TColor[] target = new TColor[this.CropRectangle.Width * this.CropRectangle.Height];

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock<TColor, TPacked>(this.CropRectangle.Width, this.CropRectangle.Height))
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