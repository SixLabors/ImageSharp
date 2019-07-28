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
        private readonly BinaryErrorDiffusionProcessor definition;

        public BinaryErrorDiffusionProcessor(BinaryErrorDiffusionProcessor definition)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            TPixel upperColor = this.definition.UpperColor.ToPixel<TPixel>();
            TPixel lowerColor = this.definition.LowerColor.ToPixel<TPixel>();
            IErrorDiffuser diffuser = this.definition.Diffuser;

            byte threshold = (byte)MathF.Round(this.definition.Threshold * 255F);
            bool isAlphaOnly = typeof(TPixel) == typeof(Alpha8);

            var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());
            int startY = interest.Y;
            int endY = interest.Bottom;
            int startX = interest.X;
            int endX = interest.Right;

            // Collect the values before looping so we can reduce our calculation count for identical sibling pixels
            TPixel sourcePixel = source[startX, startY];
            TPixel previousPixel = sourcePixel;
            Rgba32 rgba = default;
            sourcePixel.ToRgba32(ref rgba);

            // Convert to grayscale using ITU-R Recommendation BT.709 if required
            byte luminance = isAlphaOnly ? rgba.A : ImageMaths.Get8BitBT709Luminance(rgba.R, rgba.G, rgba.B);

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
                        luminance = isAlphaOnly ? rgba.A : ImageMaths.Get8BitBT709Luminance(rgba.R, rgba.G, rgba.B);

                        // Setup the previous pointer
                        previousPixel = sourcePixel;
                    }

                    TPixel transformedPixel = luminance >= threshold ? upperColor : lowerColor;
                    diffuser.Dither(source, sourcePixel, transformedPixel, x, y, startX, startY, endX, endY);
                }
            }
        }
    }
}