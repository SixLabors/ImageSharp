// <copyright file="OrderedDitherProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Buffers;

    using ImageSharp.Dithering;

    /// <summary>
    /// An <see cref="IImageProcessor{TColor}"/> that dithers an image using error diffusion.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class OrderedDitherProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDitherProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="index">The component index to test the threshold against. Must range from 0 to 3.</param>
        public OrderedDitherProcessor(IOrderedDither dither, int index)
        {
            Guard.NotNull(dither, nameof(dither));
            Guard.MustBeBetweenOrEqualTo(index, 0, 3, nameof(index));

            // Alpha8 only stores the pixel data in the alpha channel.
            if (typeof(TColor) == typeof(Alpha8))
            {
                index = 3;
            }

            this.Dither = dither;
            this.Index = index;

            // Default to white/black for upper/lower.
            this.UpperColor = NamedColors<TColor>.White;
            this.LowerColor = NamedColors<TColor>.Black;
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
                    byte[] bytes = ArrayPool<byte>.Shared.Rent(4);

                    for (int x = minX; x < maxX; x++)
                    {
                        int offsetX = x - startX;
                        TColor sourceColor = sourcePixels[offsetX, offsetY];
                        this.Dither.Dither(sourcePixels, sourceColor, this.UpperColor, this.LowerColor, bytes, this.Index, offsetX, offsetY, maxX, maxY);
                    }

                    ArrayPool<byte>.Shared.Return(bytes);
                }
            }
        }
    }
}