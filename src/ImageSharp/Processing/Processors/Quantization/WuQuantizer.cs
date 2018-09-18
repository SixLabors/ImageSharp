// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Allows the quantization of images pixels using Xiaolin Wu's Color Quantizer <see href="http://www.ece.mcmaster.ca/~xwu/cq.c"/>
    /// <para>
    /// By default the quantizer uses <see cref="KnownDiffusers.FloydSteinberg"/> dithering and a color palette of a maximum length of <value>255</value>
    /// </para>
    /// </summary>
    public class WuQuantizer : IQuantizer
    {
        /// <summary>
        /// The default maximum number of colors to use when quantizing the image.
        /// </summary>
        public const int DefaultMaxColors = 256;

        /// <summary>
        /// Initializes a new instance of the <see cref="WuQuantizer"/> class.
        /// </summary>
        public WuQuantizer()
            : this(true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WuQuantizer"/> class.
        /// </summary>
        /// <param name="maxColors">The maximum number of colors to hold in the color palette</param>
        public WuQuantizer(int maxColors)
            : this(GetDiffuser(true), maxColors)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WuQuantizer"/> class.
        /// </summary>
        /// <param name="dither">Whether to apply dithering to the output image</param>
        public WuQuantizer(bool dither)
            : this(GetDiffuser(dither), DefaultMaxColors)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WuQuantizer"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffusion algorithm, if any, to apply to the output image</param>
        public WuQuantizer(IErrorDiffuser diffuser)
            : this(diffuser, DefaultMaxColors)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WuQuantizer"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffusion algorithm, if any, to apply to the output image</param>
        /// <param name="maxColors">The maximum number of colors to hold in the color palette</param>
        public WuQuantizer(IErrorDiffuser diffuser, int maxColors)
        {
            this.Diffuser = diffuser;
            this.MaxColors = maxColors.Clamp(1, DefaultMaxColors);
        }

        /// <inheritdoc />
        public IErrorDiffuser Diffuser { get; }

        /// <summary>
        /// Gets the maximum number of colors to hold in the color palette.
        /// </summary>
        public int MaxColors { get; }

        /// <inheritdoc />
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>()
            where TPixel : struct, IPixel<TPixel>
            => new WuFrameQuantizer<TPixel>(this);

        /// <inheritdoc/>
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>(int maxColors)
            where TPixel : struct, IPixel<TPixel>
        {
            maxColors = maxColors.Clamp(1, DefaultMaxColors);
            return new WuFrameQuantizer<TPixel>(this, maxColors);
        }

        private static IErrorDiffuser GetDiffuser(bool dither) => dither ? KnownDiffusers.FloydSteinberg : null;
    }
}