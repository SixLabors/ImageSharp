// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Advanced.ParallelUtils;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="BinaryThresholdProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public BinaryThresholdProcessor(Configuration configuration, BinaryThresholdProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            byte threshold = (byte)MathF.Round(this.definition.Threshold * 255F);
            TPixel upper = this.definition.UpperColor.ToPixel<TPixel>();
            TPixel lower = this.definition.LowerColor.ToPixel<TPixel>();

            Rectangle sourceRectangle = this.SourceRectangle;
            Configuration configuration = this.Configuration;

            var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());
            int startY = interest.Y;
            int endY = interest.Bottom;
            int startX = interest.X;
            int endX = interest.Right;

            bool isAlphaOnly = typeof(TPixel) == typeof(A8);

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
