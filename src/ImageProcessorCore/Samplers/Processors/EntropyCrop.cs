// <copyright file="EntropyCrop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Threading.Tasks;

    using ImageProcessorCore.Filters;

    /// <summary>
    /// Provides methods to allow the cropping of an image to preserve areas of highest
    /// entropy.
    /// </summary>
    public class EntropyCrop : ImageSampler
    {
        /// <summary>
        /// The rectangle for cropping
        /// </summary>
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
        protected override void OnApply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            using (ImageBase temp = new Image(source.Width, source.Height))
            {
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
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            // Jump out, we'll deal with that later.
            if (source.Bounds == target.Bounds)
            {
                return;
            }

            int targetY = this.cropRectangle.Y;
            int targetBottom = this.cropRectangle.Bottom;
            int startX = this.cropRectangle.X;
            int endX = this.cropRectangle.Right;

            Parallel.For(
            startY,
            endY,
            y =>
            {
                if (y >= targetY && y < targetBottom)
                {
                    for (int x = startX; x < endX; x++)
                    {
                        target[x - startX, y - targetY] = source[x, y];
                    }
                }

                this.OnRowProcessed();
            });
        }

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            // Copy the pixels over.
            if (source.Bounds == target.Bounds)
            {
                target.ClonePixels(target.Width, target.Height, source.Pixels);
            }
        }
    }
}
