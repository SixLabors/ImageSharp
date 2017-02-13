// <copyright file="ErrorDiffusionDitherProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;

    using ImageSharp.Dithering;

    /// <summary>
    /// An <see cref="IImageProcessor{TColor}"/> that dithers an image using error diffusion.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class ErrorDiffusionDitherProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorDiffusionDitherProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        public ErrorDiffusionDitherProcessor(IErrorDiffuser diffuser, float threshold)
        {
            Guard.NotNull(diffuser, nameof(diffuser));

            // TODO: Check thresholding limit. Colors should probably have Max/Min/Middle properties.
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));

            this.Diffuser = diffuser;
            this.Threshold = threshold;

            // Default to white/black for upper/lower.
            TColor upper = default(TColor);
            upper.PackFromBytes(255, 255, 255, 255);
            this.UpperColor = upper;

            TColor lower = default(TColor);
            lower.PackFromBytes(0, 0, 0, 255);
            this.LowerColor = lower;
        }

        /// <summary>
        /// Gets the error diffuser.
        /// </summary>
        public IErrorDiffuser Diffuser { get; }

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
                for (int y = minY; y < maxY; y++)
                {
                    int offsetY = y - startY;
                    for (int x = minX; x < maxX; x++)
                    {
                        int offsetX = x - startX;
                        TColor sourceColor = sourcePixels[offsetX, offsetY];
                        TColor transformedColor = sourceColor.ToVector4().X >= this.Threshold ? this.UpperColor : this.LowerColor;
                        this.Diffuser.Dither(sourcePixels, sourceColor, transformedColor, offsetX, offsetY, maxX, maxY);
                    }
                }
            }
        }
    }
}