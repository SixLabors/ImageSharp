// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Allows the quantization of images pixels using web safe colors defined in the CSS Color Module Level 4.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/> Override this class to provide your own palette.
    /// <para>
    /// By default the quantizer uses <see cref="KnownDiffusers.FloydSteinberg"/> dithering and the <see cref="NamedColors{TPixel}.WebSafePalette"/>
    /// </para>
    /// </summary>
    public class PaletteQuantizer : IQuantizer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        public PaletteQuantizer()
             : this(true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="dither">Whether to apply dithering to the output image</param>
        public PaletteQuantizer(bool dither)
            : this(GetDiffuser(dither))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaletteQuantizer"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffusion algorithm, if any, to apply to the output image</param>
        public PaletteQuantizer(IErrorDiffuser diffuser) => this.Diffuser = diffuser;

        /// <inheritdoc />
        public IErrorDiffuser Diffuser { get; }

        /// <inheritdoc />
        public virtual IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>()
            where TPixel : struct, IPixel<TPixel>
            => this.CreateFrameQuantizer(() => NamedColors<TPixel>.WebSafePalette);

        /// <inheritdoc/>
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(int maxColors)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel[] websafe = NamedColors<TPixel>.WebSafePalette;
            int max = Math.Min(maxColors, websafe.Length);

            if (max != websafe.Length)
            {
                return this.CreateFrameQuantizer(() => NamedColors<TPixel>.WebSafePalette.AsSpan(0, max).ToArray());
            }

            return this.CreateFrameQuantizer(() => websafe);
        }

        /// <summary>
        /// Gets the palette to use to quantize the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="paletteFunction">The method to return the palette.</param>
        /// <returns>The <see cref="IFrameQuantizer{TPixel}"/></returns>
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Func<TPixel[]> paletteFunction)
            where TPixel : struct, IPixel<TPixel>
            => new PaletteFrameQuantizer<TPixel>(this, paletteFunction.Invoke());

        private static IErrorDiffuser GetDiffuser(bool dither) => dither ? KnownDiffusers.FloydSteinberg : null;
    }
}