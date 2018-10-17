// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
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
        /// <param name="flipMode">The <see cref="FlipMode"/> used to perform flipping.</param>
        public FlipProcessor(FlipMode flipMode)
        {
            this.FlipMode = flipMode;
        }

        /// <summary>
        /// Gets the <see cref="FlipMode"/> used to perform flipping.
        /// </summary>
        public FlipMode FlipMode { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            switch (this.FlipMode)
            {
                // No default needed as we have already set the pixels.
                case FlipMode.Vertical:
                    this.FlipX(source, configuration);
                    break;
                case FlipMode.Horizontal:
                    this.FlipY(source, configuration);
                    break;
            }
        }

        /// <summary>
        /// Swaps the image at the X-axis, which goes horizontally through the middle at half the height of the image.
        /// </summary>
        /// <param name="source">The source image to apply the process to.</param>
        /// <param name="configuration">The configuration.</param>
        private void FlipX(ImageFrame<TPixel> source, Configuration configuration)
        {
            int height = source.Height;

            using (IMemoryOwner<TPixel> tempBuffer = configuration.MemoryAllocator.Allocate<TPixel>(source.Width))
            {
                Span<TPixel> temp = tempBuffer.Memory.Span;

                for (int yTop = 0; yTop < height / 2; yTop++)
                {
                    int yBottom = height - yTop - 1;
                    Span<TPixel> topRow = source.GetPixelRowSpan(yBottom);
                    Span<TPixel> bottomRow = source.GetPixelRowSpan(yTop);
                    topRow.CopyTo(temp);
                    bottomRow.CopyTo(topRow);
                    temp.CopyTo(bottomRow);
                }
            }
        }

        /// <summary>
        /// Swaps the image at the Y-axis, which goes vertically through the middle at half of the width of the image.
        /// </summary>
        /// <param name="source">The source image to apply the process to.</param>
        /// <param name="configuration">The configuration.</param>
        private void FlipY(ImageFrame<TPixel> source, Configuration configuration)
        {
            ParallelHelper.IterateRows(
                source.Bounds(),
                configuration,
                rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            source.GetPixelRowSpan(y).Reverse();
                        }
                    });
        }
    }
}