// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization
{
    /// <summary>
    /// Performs binary threshold filtering against an image using error diffusion.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BinaryErrorDiffusionProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryErrorDiffusionProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        public BinaryErrorDiffusionProcessor(IErrorDiffuser diffuser)
            : this(diffuser, .5F)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryErrorDiffusionProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        public BinaryErrorDiffusionProcessor(IErrorDiffuser diffuser, float threshold)
            : this(diffuser, threshold, NamedColors<TPixel>.White, NamedColors<TPixel>.Black)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryErrorDiffusionProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="diffuser">The error diffuser</param>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold.</param>
        public BinaryErrorDiffusionProcessor(IErrorDiffuser diffuser, float threshold, TPixel upperColor, TPixel lowerColor)
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
        public TPixel UpperColor { get; }

        /// <summary>
        /// Gets the color to use for pixels that fall below the threshold.
        /// </summary>
        public TPixel LowerColor { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            float threshold = this.Threshold * 255F;
            Rgba32 rgba = default;
            bool isAlphaOnly = typeof(TPixel) == typeof(Alpha8);

            var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());
            int startY = interest.Y;
            int endY = interest.Bottom;
            int startX = interest.X;
            int endX = interest.Right;

            // Collect the values before looping so we can reduce our calculation count for identical sibling pixels
            TPixel sourcePixel = source[startX, startY];
            TPixel previousPixel = sourcePixel;
            sourcePixel.ToRgba32(ref rgba);

            // Convert to grayscale using ITU-R Recommendation BT.709 if required
            float luminance = isAlphaOnly ? rgba.A : (.2126F * rgba.R) + (.7152F * rgba.G) + (.0722F * rgba.B);

            for (int y = startY; y < endY; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);

                for (int x = startX; x < endX; x++)
                {
                    sourcePixel = row[x];

                    // Check if this is the same as the last pixel. If so use that value
                    // rather than calculating it again. This is an inexpensive optimization.
                    if (!previousPixel.Equals(sourcePixel))
                    {
                        sourcePixel.ToRgba32(ref rgba);
                        luminance = isAlphaOnly ? rgba.A : (.2126F * rgba.R) + (.7152F * rgba.G) + (.0722F * rgba.B);

                        // Setup the previous pointer
                        previousPixel = sourcePixel;
                    }

                    TPixel transformedPixel = luminance >= threshold ? this.UpperColor : this.LowerColor;
                    this.Diffuser.Dither(source, sourcePixel, transformedPixel, x, y, startX, startY, endX, endY);
                }
            }
        }
    }
}