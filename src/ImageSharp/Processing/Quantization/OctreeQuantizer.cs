// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Dithering;
using SixLabors.ImageSharp.Processing.Dithering.ErrorDiffusion;
using SixLabors.ImageSharp.Processing.Quantization.FrameQuantizers;

namespace SixLabors.ImageSharp.Processing.Quantization
{
    /// <summary>
    /// Allows the quantization of images pixels using Octrees.
    /// <see href="http://msdn.microsoft.com/en-us/library/aa479306.aspx"/>
    /// </summary>
    public class OctreeQuantizer : IQuantizer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer"/> class.
        /// </summary>
        public OctreeQuantizer()
            : this(255)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer"/> class.
        /// </summary>
        /// <param name="dither">Whether to apply dithering to the output image</param>
        public OctreeQuantizer(bool dither)
            : this(dither, DiffuseMode.FloydSteinberg, 255)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer"/> class.
        /// </summary>
        /// <param name="maxColors">The maximum number of colors to hold in the color palette</param>
        public OctreeQuantizer(int maxColors)
            : this(true, DiffuseMode.FloydSteinberg, maxColors)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OctreeQuantizer"/> class.
        /// </summary>
        /// <param name="dither">Whether to apply dithering to the output image</param>
        /// <param name="ditherType">The dithering algorithm to apply to the output image</param>
        /// <param name="maxColors">The maximum number of colors to hold in the color palette</param>
        public OctreeQuantizer(bool dither, IErrorDiffuser ditherType, int maxColors)
        {
            Guard.NotNull(ditherType, nameof(ditherType));
            Guard.MustBeBetweenOrEqualTo(maxColors, 1, 255, nameof(maxColors));

            this.Dither = dither;
            this.DitherType = ditherType;
            this.MaxColors = maxColors;
        }

        /// <inheritdoc />
        public bool Dither { get; }

        /// <inheritdoc />
        public IErrorDiffuser DitherType { get; }

        /// <summary>
        /// Gets the maximum number of colors to hold in the color palette.
        /// </summary>
        public int MaxColors { get; }

        /// <inheritdoc />
        public IFrameQuantizer<TPixel> CreateFrameQuantizer<TPixel>()
            where TPixel : struct, IPixel<TPixel>
            => new OctreeFrameQuantizer<TPixel>(this);
    }
}