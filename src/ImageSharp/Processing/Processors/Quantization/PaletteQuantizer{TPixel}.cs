// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// A generic palette quantizer.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public class PaletteQuantizer<TPixel> : IQuantizer
            where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="palette">The color palette to use.</param>
        public PaletteQuantizer(TPixel[] palette)
             : this(palette, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="palette">The color palette to use.</param>
        /// <param name="dither">Whether to apply dithering to the output image</param>
        public PaletteQuantizer(TPixel[] palette, bool dither)
            : this(palette, GetDiffuser(dither))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer{TPixel}"/> class.
        /// </summary>
        /// <param name="palette">The color palette to use.</param>
        /// <param name="diffuser">The error diffusion algorithm, if any, to apply to the output image</param>
        public PaletteQuantizer(TPixel[] palette, IErrorDiffuser diffuser)
        {
            Guard.MustBeBetweenOrEqualTo(palette.Length, QuantizerConstants.MinColors, QuantizerConstants.MaxColors, nameof(palette));
            this.Palette = palette;
            this.Diffuser = diffuser;
        }

        /// <inheritdoc/>
        public IErrorDiffuser Diffuser { get; }

        /// <summary>
        /// Gets the palette.
        /// </summary>
        public TPixel[] Palette { get; }

        /// <summary>
        /// Creates the generic frame quantizer.
        /// </summary>
        /// <param name="configuration">The <see cref="Configuration"/> to configure internal operations.</param>
        /// <returns>The <see cref="IFrameQuantizer{TPixel}"/>.</returns>
        public IFrameQuantizer<TPixel> CreateFrameQuantizer(Configuration configuration)
            => ((IQuantizer)this).CreateFrameQuantizer<TPixel>(configuration);

        /// <summary>
        /// Creates the generic frame quantizer.
        /// </summary>
        /// <param name="configuration">The <see cref="Configuration"/> to configure internal operations.</param>
        /// <param name="maxColors">The maximum number of colors to hold in the color palette.</param>
        /// <returns>The <see cref="IFrameQuantizer{TPixel}"/>.</returns>
        public IFrameQuantizer<TPixel> CreateFrameQuantizer(Configuration configuration, int maxColors)
            => ((IQuantizer)this).CreateFrameQuantizer<TPixel>(configuration, maxColors);

        /// <inheritdoc/>
        IFrameQuantizer<TPixel1> IQuantizer.CreateFrameQuantizer<TPixel1>(Configuration configuration)
        {
            if (!typeof(TPixel).Equals(typeof(TPixel1)))
            {
                throw new InvalidOperationException("Generic method type must be the same as class type.");
            }

            TPixel[] paletteRef = this.Palette;
            return new PaletteFrameQuantizer<TPixel1>(this, Unsafe.As<TPixel[], TPixel1[]>(ref paletteRef));
        }

        /// <inheritdoc/>
        IFrameQuantizer<TPixel1> IQuantizer.CreateFrameQuantizer<TPixel1>(Configuration configuration, int maxColors)
        {
            if (!typeof(TPixel).Equals(typeof(TPixel1)))
            {
                throw new InvalidOperationException("Generic method type must be the same as class type.");
            }

            TPixel[] paletteRef = this.Palette;
            TPixel1[] castPalette = Unsafe.As<TPixel[], TPixel1[]>(ref paletteRef);

            maxColors = maxColors.Clamp(QuantizerConstants.MinColors, QuantizerConstants.MaxColors);
            int max = Math.Min(maxColors, castPalette.Length);

            if (max != castPalette.Length)
            {
                return new PaletteFrameQuantizer<TPixel1>(this, castPalette.AsSpan(0, max).ToArray());
            }

            return new PaletteFrameQuantizer<TPixel1>(this, castPalette);
        }

        private static IErrorDiffuser GetDiffuser(bool dither) => dither ? KnownDiffusers.FloydSteinberg : null;
    }
}