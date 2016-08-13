// <copyright file="RotateFlipProcessor.cs" company="James Jackson-South">
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
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class FlipProcessor<T, TP> : ImageSampler<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FlipProcessor{T,TP}"/> class.
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
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
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
        private void FlipX(ImageBase<T, TP> target)
        {
            int width = target.Width;
            int height = target.Height;
            int halfHeight = (int)Math.Ceiling(target.Height * .5F);
            Image<T, TP> temp = new Image<T, TP>(width, height);
            temp.ClonePixels(width, height, target.Pixels);

            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            using (IPixelAccessor<T, TP> tempPixels = temp.Lock())
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

                            this.OnRowProcessed();
                        });
            }
        }

        /// <summary>
        /// Swaps the image at the Y-axis, which goes vertically through the middle
        /// at half of the width of the image.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        private void FlipY(ImageBase<T, TP> target)
        {
            int width = target.Width;
            int height = target.Height;
            int halfWidth = (int)Math.Ceiling(width * .5F);
            Image<T, TP> temp = new Image<T, TP>(width, height);
            temp.ClonePixels(width, height, target.Pixels);

            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            using (IPixelAccessor<T, TP> tempPixels = temp.Lock())
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

                            this.OnRowProcessed();
                        });
            }
        }
    }
}
