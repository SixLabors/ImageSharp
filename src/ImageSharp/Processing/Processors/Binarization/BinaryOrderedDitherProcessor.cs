// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization
{
    /// <summary>
    /// Defines a binary threshold filtering using ordered dithering.
    /// </summary>
    public class BinaryOrderedDitherProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryOrderedDitherProcessor"/> class.
        /// </summary>
        /// <param name="dither">The ordered ditherer.</param>
        public BinaryOrderedDitherProcessor(IOrderedDither dither)
            : this(dither, Color.White, Color.Black)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryOrderedDitherProcessor"/> class.
        /// </summary>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold.</param>
        public BinaryOrderedDitherProcessor(IOrderedDither dither, Color upperColor, Color lowerColor)
        {
            this.Dither = dither ?? throw new ArgumentNullException(nameof(dither));
            this.UpperColor = upperColor;
            this.LowerColor = lowerColor;
        }

        /// <summary>
        /// Gets the ditherer.
        /// </summary>
        public IOrderedDither Dither { get; }

        /// <summary>
        /// Gets the color to use for pixels that are above the threshold.
        /// </summary>
        public Color UpperColor { get; }

        /// <summary>
        /// Gets the color to use for pixels that fall below the threshold.
        /// </summary>
        public Color LowerColor { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new BinaryOrderedDitherProcessor<TPixel>(this);
        }
    }
}