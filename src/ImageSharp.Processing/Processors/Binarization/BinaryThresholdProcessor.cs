// <copyright file="BinaryThresholdProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor{TColor}"/> to perform binary threshold filtering against an
    /// <see cref="Image"/>. The image will be converted to grayscale before thresholding occurs.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class BinaryThresholdProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        public BinaryThresholdProcessor(float threshold)
        {
            // TODO: Check thresholding limit. Colors should probably have Max/Min/Middle properties.
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Threshold = threshold;

            // Default to white/black for upper/lower.
            this.UpperColor = NamedColors<TColor>.White;
            this.LowerColor = NamedColors<TColor>.Black;
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Threshold { get; }

        /// <summary>
        /// Gets or sets the color to use for pixels that are above the threshold.
        /// </summary>
        public TColor UpperColor { get; set; }

        /// <summary>
        /// Gets or sets the color to use for pixels that fall below the threshold.
        /// </summary>
        public TColor LowerColor { get; set; }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            new GrayscaleBt709Processor<TColor>().Apply(source, sourceRectangle);
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            float threshold = this.Threshold;
            TColor upper = this.UpperColor;
            TColor lower = this.LowerColor;

            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            // Align start/end positions.
            int minX = Math.Max(0, startX);
            int maxX = Math.Min(source.Width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(source.Height, endY);

            // Reset offset if necessary.
            if (minX > 0)
            {
                startX = 0;
            }

            if (minY > 0)
            {
                startY = 0;
            }

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                    {
                        int offsetY = y - startY;
                        for (int x = minX; x < maxX; x++)
                        {
                            int offsetX = x - startX;
                            TColor color = sourcePixels[offsetX, offsetY];

                            // Any channel will do since it's Grayscale.
                            sourcePixels[offsetX, offsetY] = color.ToVector4().X >= threshold ? upper : lower;
                        }
                    });
            }
        }
    }
}