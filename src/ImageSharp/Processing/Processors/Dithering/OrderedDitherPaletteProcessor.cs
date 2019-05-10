// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Defines a dithering operation that dithers an image using error diffusion.
    /// If no palette is given this will default to the web safe colors defined in the CSS Color Module Level 4.
    /// </summary>
    public sealed class OrderedDitherPaletteProcessor : PaletteDitherProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDitherPaletteProcessor"/> class.
        /// </summary>
        /// <param name="dither">The ordered ditherer.</param>
        public OrderedDitherPaletteProcessor(IOrderedDither dither)
            : this(dither, Color.WebSafePalette)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDitherPaletteProcessor"/> class.
        /// </summary>
        /// <param name="dither">The ordered ditherer.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        public OrderedDitherPaletteProcessor(IOrderedDither dither, ReadOnlyMemory<Color> palette)
            : base(palette) => this.Dither = dither ?? throw new ArgumentNullException(nameof(dither));

        /// <summary>
        /// Gets the ditherer.
        /// </summary>
        public IOrderedDither Dither { get; }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
        {
            return new OrderedDitherPaletteProcessor<TPixel>(this);
        }
    }
}