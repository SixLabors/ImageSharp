// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Binarization
{
    /// <summary>
    /// Performs simple binary threshold filtering against an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BinaryThresholdProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        public BinaryThresholdProcessor(float threshold)
            : this(threshold, NamedColors<TPixel>.White, NamedColors<TPixel>.Black)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold.</param>
        public BinaryThresholdProcessor(float threshold, TPixel upperColor, TPixel lowerColor)
        {
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Threshold = threshold;
            this.UpperColor = upperColor;
            this.LowerColor = lowerColor;
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Threshold { get; }

        /// <summary>
        /// Gets or sets the color to use for pixels that are above the threshold.
        /// </summary>
        public TPixel UpperColor { get; set; }

        /// <summary>
        /// Gets or sets the color to use for pixels that fall below the threshold.
        /// </summary>
        public TPixel LowerColor { get; set; }

        /// <inheritdoc/>
        protected override void OnFrameApply(
            ImageFrame<TPixel> source,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            float threshold = this.Threshold * 255F;
            TPixel upper = this.UpperColor;
            TPixel lower = this.LowerColor;

            var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());
            int startY = interest.Y;
            int endY = interest.Bottom;
            int startX = interest.X;
            int endX = interest.Right;

            bool isAlphaOnly = typeof(TPixel) == typeof(Alpha8);

            var workingRect = Rectangle.FromLTRB(startX, startY, endX, endY);

            ParallelHelper.IterateRows(
                workingRect,
                configuration,
                rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> row = source.GetPixelRowSpan(y);
                            Rgba32 rgba = default;

                            for (int x = startX; x < endX; x++)
                            {
                                ref TPixel color = ref row[x];
                                color.ToRgba32(ref rgba);

                                // Convert to grayscale using ITU-R Recommendation BT.709 if required
                                float luminance = isAlphaOnly
                                                      ? rgba.A
                                                      : (.2126F * rgba.R) + (.7152F * rgba.G) + (.0722F * rgba.B);
                                color = luminance >= threshold ? upper : lower;
                            }
                        }
                    });
        }
    }
}