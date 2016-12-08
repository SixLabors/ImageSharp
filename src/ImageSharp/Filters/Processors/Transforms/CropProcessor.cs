// <copyright file="CropProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processors
{
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
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        public CropProcessor(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Gets the width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle, int startY, int endY)
        {
            int minX = 0;
            int maxX = this.Width;
            int minY = 0;
            int maxY = this.Height;
            int sourceX = sourceRectangle.X;
            int sourceY = sourceRectangle.Y;

            Guard.MustBeGreaterThanOrEqualTo(minX, sourceX, nameof(minX));
            Guard.MustBeGreaterThanOrEqualTo(minY, startY, nameof(startY));
            Guard.MustBeLessThanOrEqualTo(maxX, sourceRectangle.Right, nameof(maxX));
            Guard.MustBeLessThanOrEqualTo(maxY, endY, nameof(maxY));

            TColor[] target = new TColor[this.Width * this.Height];

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock<TColor, TPacked>(this.Width, this.Height))
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                    {
                        for (int x = minX; x < maxX; x++)
                        {
                            targetPixels[x, y] = sourcePixels[x + sourceX, y + sourceY];
                        }
                    });
            }

            source.SetPixels(this.Width, this.Height, target);
        }
    }
}