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
    public abstract class PaletteQuantizer : IQuantizer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        protected PaletteQuantizer()
             : this(true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="dither">Whether to apply dithering to the output image</param>
        protected PaletteQuantizer(bool dither)
            : this(GetDiffuser(dither))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffusion algorithm, if any, to apply to the output image</param>
        protected PaletteQuantizer(IErrorDiffuser diffuser) => this.Diffuser = diffuser;

        /// <inheritdoc />
        public IErrorDiffuser Diffuser { get; }

        /// <inheritdoc />
        public abstract IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration)
            where TPixel : struct, IPixel<TPixel>;

        /// <inheritdoc/>
        public abstract IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration, int maxColors)
            where TPixel : struct, IPixel<TPixel>;

        /// <summary>
        /// Creates the generic frame quantizer.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The <see cref="Configuration"/> to configure internal operations.</param>
        /// <param name="palette">The color palette.</param>
        /// <param name="maxColors">The maximum number of colors to hold in the color palette.</param>
        /// <returns>The <see cref="IFrameQuantizer{TPixel}"/></returns>
        protected IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration, TPixel[] palette, int maxColors)
            where TPixel : struct, IPixel<TPixel>
        {
            int max = Math.Min(QuantizerConstants.MaxColors, Math.Min(maxColors, palette.Length));

            if (max != palette.Length)
            {
                return new PaletteFrameQuantizer<TPixel>(this, palette.AsSpan(0, max).ToArray());
            }

            return new PaletteFrameQuantizer<TPixel>(this, palette);
        }

        private static IErrorDiffuser GetDiffuser(bool dither) => dither ? KnownDiffusers.FloydSteinberg : null;
    }
}