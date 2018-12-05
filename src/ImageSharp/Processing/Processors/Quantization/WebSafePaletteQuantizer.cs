// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// A palette quantizer consisting of web safe colors as defined in the CSS Color Module Level 4.
    /// </summary>
    public class WebSafePaletteQuantizer : PaletteQuantizer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebSafePaletteQuantizer" /> class.
        /// </summary>
        public WebSafePaletteQuantizer()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSafePaletteQuantizer" /> class.
        /// </summary>
        /// <param name="dither">Whether to apply dithering to the output image</param>
        public WebSafePaletteQuantizer(bool dither)
            : base(dither)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSafePaletteQuantizer" /> class.
        /// </summary>
        /// <param name="diffuser">The error diffusion algorithm, if any, to apply to the output image</param>
        public WebSafePaletteQuantizer(IErrorDiffuser diffuser)
            : base(diffuser)
        {
        }

        /// <inheritdoc />
        public override IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration)
            => this.CreateFrameQuantizer<TPixel>(configuration, NamedColors<TPixel>.WebSafePalette.Length);

        /// <inheritdoc/>
        public override IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(Configuration configuration, int maxColors)
            => this.CreateFrameQuantizer(configuration, NamedColors<TPixel>.WebSafePalette, maxColors);
    }
}