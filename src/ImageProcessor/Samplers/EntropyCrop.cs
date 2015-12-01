// <copyright file="EntropyCrop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    using System;
    using System.Threading.Tasks;

    using ImageProcessor.Filters;

    /// <summary>
    /// Provides methods to allow the cropping of an image to preserve areas of highest
    /// entropy.
    /// </summary>
    public class EntropyCrop : ParallelImageProcessor
    {
        private Rectangle cropRectangle;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCrop"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="threshold"/> is less than 0 or is greater than 1.
        /// </exception>
        public EntropyCrop(float threshold)
        {
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Value = threshold;
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Value { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            ImageBase temp = new Image(source.Width, source.Height);

            // Detect the edges.
            new Sobel().Apply(temp, source, sourceRectangle);

            // Apply threshold binarization filter.
            new Threshold(.5f).Apply(temp, temp, sourceRectangle);

            // Search for the first white pixels
            Rectangle rectangle = ImageMaths.GetFilteredBoundingRectangle(temp, 0);

            // Reset the target pixel to the correct size.
            target.SetPixels(rectangle.Width, rectangle.Height, new float[rectangle.Width * rectangle.Height * 4]);
            this.cropRectangle = rectangle;
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            if (source.Bounds == target.Bounds)
            {
                target.SetPixels(target.Width, target.Height, source.Pixels);
                return;
            }

            int targetY = this.cropRectangle.Y;
            int startX = targetRectangle.X;
            int targetX = this.cropRectangle.X;
            int endX = this.cropRectangle.Width;
            int maxX = endX - 1;
            int maxY = this.cropRectangle.Bottom - 1;

            Parallel.For(
            startY,
            endY,
            y =>
            {
                for (int x = startX; x < endX; x++)
                {
                    int offsetY = y + targetY;
                    offsetY = offsetY.Clamp(0, maxY);

                    int offsetX = x + targetX;
                    offsetX = offsetX.Clamp(0, maxX);

                    target[x, y] = source[offsetX, offsetY];
                }
            });
        }
    }
}
