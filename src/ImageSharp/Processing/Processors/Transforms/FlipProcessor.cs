// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Provides methods that allow the flipping of an image around its center point.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class FlipProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FlipProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="flipType">The <see cref="FlipType"/> used to perform flipping.</param>
        public FlipProcessor(FlipType flipType)
        {
            this.FlipType = flipType;
        }

        /// <summary>
        /// Gets the <see cref="FlipType"/> used to perform flipping.
        /// </summary>
        public FlipType FlipType { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            switch (this.FlipType)
            {
                // No default needed as we have already set the pixels.
                case FlipType.Vertical:
                    this.FlipX(source, configuration);
                    break;
                case FlipType.Horizontal:
                    this.FlipY(source, configuration);
                    break;
            }
        }

        /// <summary>
        /// Swaps the image at the X-axis, which goes horizontally through the middle
        /// at half the height of the image.
        /// </summary>
        /// <param name="source">The source image to apply the process to.</param>
        /// <param name="configuration">The configuration.</param>
        private void FlipX(ImageFrame<TPixel> source, Configuration configuration)
        {
            int width = source.Width;
            int height = source.Height;
            int halfHeight = (int)Math.Ceiling(source.Height * .5F);

            using (var targetPixels = new PixelAccessor<TPixel>(width, height))
            {
                Parallel.For(
                    0,
                    halfHeight,
                    configuration.ParallelOptions,
                    y =>
                        {
                            int newY = height - y - 1;
                            Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
                            Span<TPixel> altSourceRow = source.GetPixelRowSpan(newY);
                            Span<TPixel> targetRow = targetPixels.GetRowSpan(y);
                            Span<TPixel> altTargetRow = targetPixels.GetRowSpan(newY);

                            sourceRow.CopyTo(altTargetRow);
                            altSourceRow.CopyTo(targetRow);
                        });

                source.SwapPixelsBuffers(targetPixels);
            }
        }

        /// <summary>
        /// Swaps the image at the Y-axis, which goes vertically through the middle
        /// at half of the width of the image.
        /// </summary>
        /// <param name="source">The source image to apply the process to.</param>
        /// <param name="configuration">The configuration.</param>
        private void FlipY(ImageFrame<TPixel> source, Configuration configuration)
        {
            int width = source.Width;
            int height = source.Height;
            int halfWidth = (int)Math.Ceiling(width * .5F);

            using (var targetPixels = new PixelAccessor<TPixel>(width, height))
            {
                Parallel.For(
                    0,
                    height,
                    configuration.ParallelOptions,
                    y =>
                        {
                            Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
                            Span<TPixel> targetRow = targetPixels.GetRowSpan(y);

                            for (int x = 0; x < halfWidth; x++)
                            {
                                int newX = width - x - 1;
                                targetRow[x] = sourceRow[newX];
                                targetRow[newX] = sourceRow[x];
                            }
                        });

                source.SwapPixelsBuffers(targetPixels);
            }
        }
    }
}