// <copyright file="OrderedDitherProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Buffers;

    using ImageSharp.Dithering;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// An <see cref="IImageProcessor{TPixel}"/> that dithers an image using error diffusion.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class OrderedDitherProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDitherProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="index">The component index to test the threshold against. Must range from 0 to 3.</param>
        public OrderedDitherProcessor(IOrderedDither dither, int index)
        {
            Guard.NotNull(dither, nameof(dither));
            Guard.MustBeBetweenOrEqualTo(index, 0, 3, nameof(index));

            // Alpha8 only stores the pixel data in the alpha channel.
            if (typeof(TPixel) == typeof(Alpha8))
            {
                index = 3;
            }

            this.Dither = dither;
            this.Index = index;

            // Default to white/black for upper/lower.
            this.UpperColor = NamedColors<TPixel>.White;
            this.LowerColor = NamedColors<TPixel>.Black;
        }

        /// <summary>
        /// Gets the ditherer.
        /// </summary>
        public IOrderedDither Dither { get; }

        /// <summary>
        /// Gets the component index to test the threshold against.
        /// </summary>
        public int Index { get; }

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

            byte[] bytes = new byte[4];
            for (int y = minY; y < maxY; y++)
            {
                int offsetY = y - startY;
                Span<TPixel> row = source.GetRowSpan(offsetY);

                for (int x = minX; x < maxX; x++)
                {
                    int offsetX = x - startX;
                    TPixel sourceColor = row[offsetX];
                    this.Dither.Dither(source, sourceColor, this.UpperColor, this.LowerColor, bytes, this.Index, offsetX, offsetY, maxX, maxY);
                }
            }
        }
    }
}