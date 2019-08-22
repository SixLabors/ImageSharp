// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization
{
    /// <summary>
    /// Performs binary threshold filtering against an image using error diffusion.
    /// </summary>
    public class BinaryErrorDiffusionProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryErrorDiffusionProcessor"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        public BinaryErrorDiffusionProcessor(IErrorDiffuser diffuser)
            : this(diffuser, .5F)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryErrorDiffusionProcessor"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        public BinaryErrorDiffusionProcessor(IErrorDiffuser diffuser, float threshold)
            : this(diffuser, threshold, Color.White, Color.Black)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryErrorDiffusionProcessor"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold.</param>
        public BinaryErrorDiffusionProcessor(IErrorDiffuser diffuser, float threshold, Color upperColor, Color lowerColor)
        {
            Guard.NotNull(diffuser, nameof(diffuser));
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));

            this.Diffuser = diffuser;
            this.Threshold = threshold;
            this.UpperColor = upperColor;
            this.LowerColor = lowerColor;
        }

        /// <summary>
        /// Gets the error diffuser.
        /// </summary>
        public IErrorDiffuser Diffuser { get; }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Threshold { get; }

        /// <summary>
        /// Gets the color to use for pixels that are above the threshold.
        /// </summary>
        public Color UpperColor { get; }

        /// <summary>
        /// Gets the color to use for pixels that fall below the threshold.
        /// </summary>
        public Color LowerColor { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new BinaryErrorDiffusionProcessor<TPixel>(this);
        }
    }
}