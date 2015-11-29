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

            // TODO: Should we detect edges on a grayscale image? 
            new Sobel() { Greyscale = true }.Apply(temp, source, sourceRectangle);

            // Apply threshold binarization filter.
            new Threshold(.5f).Apply(temp, temp, sourceRectangle);

            // Search for the first white pixels
            Rectangle rectangle = ImageMaths.GetFilteredBoundingRectangle(temp, 0);
            target.SetPixels(rectangle.Width, rectangle.Height, new float[rectangle.Width * rectangle.Height * 4]);

        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int targetY = targetRectangle.Y;
            int targetBottom = targetRectangle.Height;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Width;

            Parallel.For(
            startY,
            endY,
            y =>
            {
                if (y >= targetY && y < targetBottom)
                {
                    for (int x = startX; x < endX; x++)
                    {
                        target[x, y] = source[x, y];
                    }
                }
            });
        }
    }
}
