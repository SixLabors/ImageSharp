// <copyright file="BrightnessProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor{TColor}"/> to change the brightness of an <see cref="Image{TColor}"/>.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class BrightnessProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrightnessProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="brightness">The new brightness of the image. Must be between -100 and 100.</param>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="brightness"/> is less than -100 or is greater than 100.
        /// </exception>
        public BrightnessProcessor(int brightness)
        {
            Guard.MustBeBetweenOrEqualTo(brightness, -100, 100, nameof(brightness));
            this.Value = brightness;
        }

        /// <summary>
        /// Gets the brightness value.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            float brightness = this.Value / 100F;

            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

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

                                // TODO: Check this with other formats.
                                Vector4 vector = sourcePixels[offsetX, offsetY].ToVector4().Expand();
                                Vector3 transformed = new Vector3(vector.X, vector.Y, vector.Z) + new Vector3(brightness);
                                vector = new Vector4(transformed, vector.W);

                                TColor packed = default(TColor);
                                packed.PackFromVector4(vector.Compress());

                                sourcePixels[offsetX, offsetY] = packed;
                            }
                        });
            }
        }
    }
}