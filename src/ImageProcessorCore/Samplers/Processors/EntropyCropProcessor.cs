// <copyright file="EntropyCropProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods to allow the cropping of an image to preserve areas of highest
    /// entropy.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class EntropyCropProcessor<TColor, TPacked> : ImageSampler<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The rectangle for cropping
        /// </summary>
        private Rectangle cropRectangle;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCropProcessor{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="threshold"/> is less than 0 or is greater than 1.
        /// </exception>
        public EntropyCropProcessor(float threshold)
        {
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Value = threshold;
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Value { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            ImageBase<TColor, TPacked> temp = new Image<TColor, TPacked>(source.Width, source.Height);

            // Detect the edges.
            new SobelProcessor<TColor, TPacked>().Apply(temp, source, sourceRectangle);

            // Apply threshold binarization filter.
            new BinaryThresholdProcessor<TColor, TPacked>(.5f).Apply(temp, sourceRectangle);

            // Search for the first white pixels
            Rectangle rectangle = ImageMaths.GetFilteredBoundingRectangle(temp, 0);

            // Reset the target pixel to the correct size.
            target.SetPixels(rectangle.Width, rectangle.Height, new TColor[rectangle.Width * rectangle.Height]);
            this.cropRectangle = rectangle;
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            // Jump out, we'll deal with that later.
            if (source.Bounds == target.Bounds)
            {
                return;
            }

            int targetY = this.cropRectangle.Y;
            int targetBottom = this.cropRectangle.Bottom;
            int startX = this.cropRectangle.X;
            int endX = this.cropRectangle.Right;

            int minY = Math.Max(targetY, startY);
            int maxY = Math.Min(targetBottom, endY);

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                targetPixels[x - startX, y - targetY] = sourcePixels[x, y];
                            }

                            this.OnRowProcessed();
                        });
            }
        }

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            // Copy the pixels over.
            if (source.Bounds == target.Bounds)
            {
                target.ClonePixels(target.Width, target.Height, source.Pixels);
            }
        }
    }
}
