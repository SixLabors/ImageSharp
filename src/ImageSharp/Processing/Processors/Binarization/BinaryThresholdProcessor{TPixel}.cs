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
        private readonly BinaryThresholdProcessor definition;

        public BinaryThresholdProcessor(BinaryThresholdProcessor definition)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(
            ImageFrame<TPixel> source,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            byte threshold = (byte)MathF.Round(this.definition.Threshold * 255F);
            TPixel upper = this.definition.UpperColor.ToPixel<TPixel>();
            TPixel lower = this.definition.LowerColor.ToPixel<TPixel>();

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
                        Rgba32 rgba = default;
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> row = source.GetPixelRowSpan(y);

                            for (int x = startX; x < endX; x++)
                            {
                                ref TPixel color = ref row[x];
                                color.ToRgba32(ref rgba);

                                // Convert to grayscale using ITU-R Recommendation BT.709 if required
                                byte luminance = isAlphaOnly ? rgba.A : ImageMaths.Get8BitBT709Luminance(rgba.R, rgba.G, rgba.B);
                                color = luminance >= threshold ? upper : lower;
                            }
                        }
                    });
        }
    }
}