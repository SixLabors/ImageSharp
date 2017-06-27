// <copyright file="BinaryThresholdProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Threading.Tasks;

    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// An <see cref="IImageProcessor{TPixel}"/> to perform binary threshold filtering against an
    /// <see cref="Image{TPixel}"/>. The image will be converted to grayscale before thresholding occurs.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BinaryThresholdProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        public BinaryThresholdProcessor(float threshold)
        {
            // TODO: Check thresholding limit. Colors should probably have Max/Min/Middle properties.
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Threshold = threshold;

            // Default to white/black for upper/lower.
            this.UpperColor = NamedColors<TPixel>.White;
            this.LowerColor = NamedColors<TPixel>.Black;
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Threshold { get; }

        /// <summary>
        /// Gets or sets the color to use for pixels that are above the threshold.
        /// </summary>
        public TPixel UpperColor { get; set; }

        /// <summary>
        /// Gets or sets the color to use for pixels that fall below the threshold.
        /// </summary>
        public TPixel LowerColor { get; set; }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            new GrayscaleBt709Processor<TPixel>().Apply(source, sourceRectangle);
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            float threshold = this.Threshold;
            TPixel upper = this.UpperColor;
            TPixel lower = this.LowerColor;

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

            Parallel.For(
                minY,
                maxY,
                this.ParallelOptions,
                y =>
                {
                    Span<TPixel> row = source.GetRowSpan(y - startY);

                    for (int x = minX; x < maxX; x++)
                    {
                        ref TPixel color = ref row[x - startX];

                        // Any channel will do since it's Grayscale.
                        color = color.ToVector4().X >= threshold ? upper : lower;
                    }
                });
        }
    }
}