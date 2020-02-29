// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Allows the quantization of images pixels using color palettes.
    /// </summary>
    public class PaletteQuantizer : IQuantizer
    {
        private readonly ReadOnlyMemory<Color> palette;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="palette">The color palette.</param>
        public PaletteQuantizer(ReadOnlyMemory<Color> palette)
            : this(palette, new QuantizerOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="palette">The color palette.</param>
        /// <param name="options">The quantizer options defining quantization rules.</param>
        public PaletteQuantizer(ReadOnlyMemory<Color> palette, QuantizerOptions options)
        {
            Guard.MustBeGreaterThan(palette.Length, 0, nameof(palette));
            Guard.NotNull(options, nameof(options));

            this.palette = palette;
            this.Options = options;
        }

        /// <inheritdoc />
        public QuantizerOptions Options { get; }

        /// <inheritdoc />
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration)
            where TPixel : unmanaged, IPixel<TPixel>
            => this.CreateFrameQuantizer<TPixel>(configuration, this.Options);

        /// <inheritdoc />
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration, QuantizerOptions options)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(options, nameof(options));
            return new PaletteFrameQuantizer<TPixel>(configuration, options, this.palette.Span);
        }
    }
}
