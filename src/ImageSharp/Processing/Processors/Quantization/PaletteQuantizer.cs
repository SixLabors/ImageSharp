// Copyright (c) Six Labors.
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
        private static readonly QuantizerOptions DefaultOptions = new QuantizerOptions();
        private readonly ReadOnlyMemory<Color> colorPalette;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="palette">The color palette.</param>
        public PaletteQuantizer(ReadOnlyMemory<Color> palette)
            : this(palette, DefaultOptions)
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

            this.colorPalette = palette;
            this.Options = options;
        }

        /// <inheritdoc />
        public QuantizerOptions Options { get; }

        /// <inheritdoc />
        public IQuantizer<TPixel> CreatePixelSpecificQuantizer<TPixel>(Configuration configuration)
            where TPixel : unmanaged, IPixel<TPixel>
            => this.CreatePixelSpecificQuantizer<TPixel>(configuration, this.Options);

        /// <inheritdoc />
        public IQuantizer<TPixel> CreatePixelSpecificQuantizer<TPixel>(Configuration configuration, QuantizerOptions options)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(options, nameof(options));

            // The palette quantizer can reuse the same pixel map across multiple frames
            // since the palette is unchanging. This allows a reduction of memory usage across
            // multi frame gifs using a global palette.
            int length = Math.Min(this.colorPalette.Length, options.MaxColors);
            var palette = new TPixel[length];

            Color.ToPixel(configuration, this.colorPalette.Span, palette.AsSpan());

            var pixelMap = new EuclideanPixelMap<TPixel>(configuration, palette);
            return new PaletteQuantizer<TPixel>(configuration, options, pixelMap);
        }
    }
}
