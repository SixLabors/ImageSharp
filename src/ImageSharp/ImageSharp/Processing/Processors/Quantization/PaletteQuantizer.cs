// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Allows the quantization of images pixels using color palettes.
    /// Override this class to provide your own palette.
    /// <para>
    /// By default the quantizer uses <see cref="KnownDiffusers.FloydSteinberg"/> dithering.
    /// </para>
    /// </summary>
    public class PaletteQuantizer : IQuantizer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="palette">The palette.</param>
        public PaletteQuantizer(ReadOnlyMemory<Color> palette)
            : this(palette, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="palette">The palette.</param>
        /// <param name="dither">Whether to apply dithering to the output image</param>
        public PaletteQuantizer(ReadOnlyMemory<Color> palette, bool dither)
            : this(palette, GetDiffuser(dither))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="palette">The palette.</param>
        /// <param name="diffuser">The error diffusion algorithm, if any, to apply to the output image</param>
        public PaletteQuantizer(ReadOnlyMemory<Color> palette, IErrorDiffuser diffuser)
        {
            this.Palette = palette;
            this.Diffuser = diffuser;
        }

        /// <inheritdoc />
        public IErrorDiffuser Diffuser { get; }

        /// <summary>
        /// Gets the palette.
        /// </summary>
        public ReadOnlyMemory<Color> Palette { get; }

        /// <inheritdoc />
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration)
            where TPixel : struct, IPixel<TPixel>
        {
            var palette = new TPixel[this.Palette.Length];
            Color.ToPixel(configuration, this.Palette.Span, palette.AsSpan());
            return new PaletteFrameQuantizer<TPixel>(this.Diffuser, palette);
        }

        /// <inheritdoc/>
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration, int maxColors)
            where TPixel : struct, IPixel<TPixel>
        {
            maxColors = maxColors.Clamp(QuantizerConstants.MinColors, QuantizerConstants.MaxColors);
            int max = Math.Min(maxColors, this.Palette.Length);

            var palette = new TPixel[max];
            Color.ToPixel(configuration, this.Palette.Span.Slice(0, max), palette.AsSpan());
            return new PaletteFrameQuantizer<TPixel>(this.Diffuser, palette);
        }

        private static IErrorDiffuser GetDiffuser(bool dither) => dither ? KnownDiffusers.FloydSteinberg : null;
    }
}
