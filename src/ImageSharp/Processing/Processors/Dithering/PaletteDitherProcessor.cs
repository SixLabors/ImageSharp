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
        private readonly Color[] palette;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteDitherProcessor"/> class.
        /// </summary>
        /// <param name="palette">The palette to select substitute colors from.</param>
        protected PaletteDitherProcessor(ReadOnlySpan<Color> palette)
        {
            // This shouldn't be a perf issue:
            // these arrays are small, and created with low frequency.
            this.palette = palette.ToArray();
        }

        /// <summary>
        /// Gets the palette to select substitute colors from.
        /// </summary>
        public ReadOnlySpan<Color> Palette => this.palette;

        /// <inheritdoc />
        public abstract IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>;
    }
}