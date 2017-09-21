// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Dithering;
using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
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
        protected override void BeforeApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            new GrayscaleBt709Processor<TPixel>().Apply(source, sourceRectangle, configuration);
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());
            int startY = interest.Y;
            int endY = interest.Bottom;
            int startX = interest.X;
            int endX = interest.Right;

            byte[] bytes = new byte[4];
            for (int y = startY; y < endY; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);

                for (int x = startX; x < endX; x++)
                {
                    TPixel sourceColor = row[x];
                    this.Dither.Dither(source, sourceColor, this.UpperColor, this.LowerColor, bytes, this.Index, x, y);
                }
            }
        }
    }
}