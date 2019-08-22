// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// Defines a dither operation using error diffusion.
    /// If no palette is given this will default to the web safe colors defined in the CSS Color Module Level 4.
    /// </summary>
    public sealed class ErrorDiffusionPaletteProcessor : PaletteDitherProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorDiffusionPaletteProcessor"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        public ErrorDiffusionPaletteProcessor(IErrorDiffuser diffuser)
            : this(diffuser, .5F)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorDiffusionPaletteProcessor"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        public ErrorDiffusionPaletteProcessor(IErrorDiffuser diffuser, float threshold)
            : this(diffuser, threshold, Color.WebSafePalette)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorDiffusionPaletteProcessor"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <param name="palette">The palette to select substitute colors from.</param>
        public ErrorDiffusionPaletteProcessor(IErrorDiffuser diffuser, float threshold, ReadOnlyMemory<Color> palette)
            : base(palette)
        {
            Guard.NotNull(diffuser, nameof(diffuser));
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));

            this.Diffuser = diffuser;
            this.Threshold = threshold;
        }

        /// <summary>
        /// Gets the error diffuser.
        /// </summary>
        public IErrorDiffuser Diffuser { get; }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Threshold { get; }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
        {
            return new ErrorDiffusionPaletteProcessor<TPixel>(this);
        }
    }
}