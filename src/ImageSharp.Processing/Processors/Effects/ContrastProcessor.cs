// <copyright file="ContrastProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor{TColor}"/> to change the contrast of an <see cref="Image{TColor}"/>.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class ContrastProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContrastProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="contrast">The new contrast of the image. Must be between -100 and 100.</param>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="contrast"/> is less than -100 or is greater than 100.
        /// </exception>
        public ContrastProcessor(int contrast)
        {
            Guard.MustBeBetweenOrEqualTo(contrast, -100, 100, nameof(contrast));
            this.Value = contrast;
        }

        /// <summary>
        /// Gets the contrast value.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            float contrast = (100F + this.Value) / 100F;

            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Vector4 contrastVector = new Vector4(contrast, contrast, contrast, 1);
            Vector4 shiftVector = new Vector4(.5F, .5F, .5F, 1);

            // Align start/end positions.
            int minX = Math.Max(0, startX);
            int maxX = Math.Min(source.Width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(source.Height, endY);

            // Reset offset if necessary.
            if (minX > 0)
            {
                startX = 0;
            }

            if (minY > 0)
            {
                startY = 0;
            }

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                        {
                            int offsetY = y - startY;
                            for (int x = minX; x < maxX; x++)
                            {
                                int offsetX = x - startX;

                                Vector4 vector = sourcePixels[offsetX, offsetY].ToVector4().Expand();
                                vector -= shiftVector;
                                vector *= contrastVector;
                                vector += shiftVector;
                                TColor packed = default(TColor);
                                packed.PackFromVector4(vector.Compress());
                                sourcePixels[offsetX, offsetY] = packed;
                            }
                        });
            }
        }
    }
}