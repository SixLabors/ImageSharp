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
        private readonly TPixel[] palette;

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
            this.palette = palette;
            this.Diffuser = diffuser;
        }

        /// <inheritdoc/>
        public IErrorDiffuser Diffuser { get; }

        /// <inheritdoc/>
        public IFrameQuantizer<TPixel1> CreateFrameQuantizer<TPixel1>(Configuration configuration)
            where TPixel1 : struct, IPixel<TPixel1>
        {
            if (!typeof(TPixel).Equals(typeof(TPixel1)))
            {
                throw new InvalidOperationException("Generic method type must be the same as class type.");
            }

            TPixel[] paletteRef = this.palette;
            return new PaletteFrameQuantizer<TPixel1>(this, Unsafe.As<TPixel[], TPixel1[]>(ref paletteRef));
        }

        /// <inheritdoc/>
        public IFrameQuantizer<TPixel1> CreateFrameQuantizer<TPixel1>(Configuration configuration, int maxColors)
            where TPixel1 : struct, IPixel<TPixel1>
        {
            if (!typeof(TPixel).Equals(typeof(TPixel1)))
            {
                throw new InvalidOperationException("Generic method type must be the same as class type.");
            }

            TPixel[] paletteRef = this.palette;
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