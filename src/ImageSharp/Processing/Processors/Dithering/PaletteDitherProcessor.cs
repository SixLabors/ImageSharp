// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// The base class for dither and diffusion processors that consume a palette.
    /// </summary>
    public abstract class PaletteDitherProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteDitherProcessor"/> class.
        /// </summary>
        /// <param name="palette">The palette to select substitute colors from.</param>
        protected PaletteDitherProcessor(ReadOnlyMemory<Color> palette)
        {
            this.Palette = palette;
        }

        /// <summary>
        /// Gets the palette to select substitute colors from.
        /// </summary>
        public ReadOnlyMemory<Color> Palette { get; }

        /// <inheritdoc />
        public abstract IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>;
    }
}