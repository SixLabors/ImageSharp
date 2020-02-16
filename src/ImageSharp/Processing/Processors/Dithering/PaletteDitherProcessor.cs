// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Allows the consumption a palette to dither an image.
    /// </summary>
    public sealed class PaletteDitherProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteDitherProcessor"/> class.
        /// </summary>
        /// <param name="dither">The ordered ditherer.</param>
        public PaletteDitherProcessor(IDither dither)
            : this(dither, Color.WebSafePalette)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteDitherProcessor"/> class.
        /// </summary>
        /// <param name="dither">The dithering algorithm.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        public PaletteDitherProcessor(IDither dither, ReadOnlyMemory<Color> palette)
        {
            this.Dither = dither ?? throw new ArgumentNullException(nameof(dither));
            this.Palette = palette;
        }

        /// <summary>
        /// Gets the dithering algorithm to apply to the output image.
        /// </summary>
        public IDither Dither { get; }

        /// <summary>
        /// Gets the dithering scale used to adjust the amount of dither. Range 0..1.
        /// </summary>
        public float DitherScale { get; }

        /// <summary>
        /// Gets the palette to select substitute colors from.
        /// </summary>
        public ReadOnlyMemory<Color> Palette { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : struct, IPixel<TPixel>
            => new PaletteDitherProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
