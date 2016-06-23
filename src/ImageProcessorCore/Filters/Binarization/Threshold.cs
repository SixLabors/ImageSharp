// <copyright file="Threshold.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor"/> to perform binary threshold filtering against an 
    /// <see cref="Image"/>. The image will be converted to greyscale before thresholding 
    /// occurs.
    /// </summary>
    public class Threshold : ParallelImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Threshold"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="threshold"/> is less than 0 or is greater than 1.
        /// </exception>
        public Threshold(float threshold)
        {
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Value = threshold;
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Value { get; }

        /// <summary>
        /// The color to use for pixels that are above the threshold.
        /// </summary>
        public Color UpperColor => Color.White;

        /// <summary>
        /// The color to use for pixels that fall below the threshold.
        /// </summary>
        public Color LowerColor => Color.Black;

        /// <inheritdoc/>
        protected override void OnApply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            new GreyscaleBt709().Apply(source, source, sourceRectangle);
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            float threshold = this.Value;
            Color upper = this.UpperColor;
            Color lower = this.LowerColor;
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            using (PixelAccessor sourcePixels = source.Lock())
            using (PixelAccessor targetPixels = target.Lock())
            {
                Parallel.For(
                startY,
                endY,
                y =>
                    {
                        if (y >= sourceY && y < sourceBottom)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                Color color = sourcePixels[x, y];

                                // Any channel will do since it's greyscale.
                                targetPixels[x, y] = color.B >= threshold ? upper : lower;
                            }
                            this.OnRowProcessed();
                        }
                    });
            }
        }
    }
}
