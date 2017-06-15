// <copyright file="ErrorDiffusionDitherProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;

    using ImageSharp.Dithering;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// An <see cref="IImageProcessor{TPixel}"/> that dithers an image using error diffusion.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ErrorDiffusionDitherProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorDiffusionDitherProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        public ErrorDiffusionDitherProcessor(IErrorDiffuser diffuser, float threshold)
        {
            Guard.NotNull(diffuser, nameof(diffuser));

            this.Diffuser = diffuser;
            this.Threshold = threshold;

            // Default to white/black for upper/lower.
            this.UpperColor = NamedColors<TPixel>.White;
            this.LowerColor = NamedColors<TPixel>.Black;
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

            for (int y = minY; y < maxY; y++)
            {
                int offsetY = y - startY;
                Span<TPixel> row = source.GetRowSpan(offsetY);

                for (int x = minX; x < maxX; x++)
                {
                    int offsetX = x - startX;
                    TPixel sourceColor = row[offsetX];
                    TPixel transformedColor = sourceColor.ToVector4().X >= this.Threshold ? this.UpperColor : this.LowerColor;
                    this.Diffuser.Dither(source, sourceColor, transformedColor, offsetX, offsetY, maxX, maxY);
                }
            }
        }
    }
}