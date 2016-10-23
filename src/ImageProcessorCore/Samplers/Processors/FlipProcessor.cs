// <copyright file="FlipProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the flipping of an image around its center point.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class FlipProcessor<TColor, TPacked> : ImageSampler<TColor, TPacked>
        where TColor : IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FlipProcessor{TColor, TPacked}"/> class.
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
        public override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            target.ClonePixels(target.Width, target.Height, source.Pixels);

            switch (this.FlipType)
            {
                // No default needed as we have already set the pixels.
                case FlipType.Vertical:
                    this.FlipX(target);
                    break;
                case FlipType.Horizontal:
                    this.FlipY(target);
                    break;
            }
        }

        /// <summary>
        /// Swaps the image at the X-axis, which goes horizontally through the middle
        /// at half the height of the image.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        private void FlipX(ImageBase<TColor, TPacked> target)
        {
            int width = target.Width;
            int height = target.Height;
            int halfHeight = (int)Math.Ceiling(target.Height * .5F);
            Image<TColor, TPacked> temp = new Image<TColor, TPacked>(width, height);
            temp.ClonePixels(width, height, target.Pixels);

            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
            using (PixelAccessor<TColor, TPacked> tempPixels = temp.Lock())
            {
                Parallel.For(
                    0,
                    halfHeight,
                    this.ParallelOptions,
                    y =>
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int newY = height - y - 1;
                                targetPixels[x, y] = tempPixels[x, newY];
                                targetPixels[x, newY] = tempPixels[x, y];
                            }
                        });
            }
        }

        /// <summary>
        /// Swaps the image at the Y-axis, which goes vertically through the middle
        /// at half of the width of the image.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        private void FlipY(ImageBase<TColor, TPacked> target)
        {
            int width = target.Width;
            int height = target.Height;
            int halfWidth = (int)Math.Ceiling(width * .5F);
            Image<TColor, TPacked> temp = new Image<TColor, TPacked>(width, height);
            temp.ClonePixels(width, height, target.Pixels);

            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
            using (PixelAccessor<TColor, TPacked> tempPixels = temp.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    this.ParallelOptions,
                    y =>
                        {
                            for (int x = 0; x < halfWidth; x++)
                            {
                                int newX = width - x - 1;
                                targetPixels[x, y] = tempPixels[newX, y];
                                targetPixels[newX, y] = tempPixels[x, y];
                            }
                        });
            }
        }
    }
}