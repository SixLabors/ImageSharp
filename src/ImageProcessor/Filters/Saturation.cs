// <copyright file="Saturation.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor"/> to change the saturation of an <see cref="Image"/>.
    /// </summary>
    public class Saturation : ParallelImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Saturation"/> class.
        /// </summary>
        /// <param name="saturation">The new saturation of the image. Must be between -100 and 100.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="saturation"/> is less than -100 or is greater than 100.
        /// </exception>
        public Saturation(int saturation)
        {
            Guard.MustBeBetweenOrEqualTo(saturation, -100, 100, nameof(saturation));
            this.Value = saturation;
        }

        /// <summary>
        /// Gets the saturation value.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            float saturation = this.Value / 100f;
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            Parallel.For(
                startY,
                endY,
                y =>
                    {
                        if (y >= sourceY && y < sourceBottom)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                target[x, y] = AdjustSaturation(source[x, y], saturation);
                            }
                        }
                    });
        }

        /// <summary>
        /// Returns a <see cref="Color"/> with the saturation adjusted.
        /// </summary>
        /// <param name="color">The source color.</param>
        /// <param name="saturation">The saturation adjustment factor.</param>
        /// <returns>
        /// The <see cref="Color"/>.
        /// </returns>
        private static Color AdjustSaturation(Color color, float saturation)
        {
            //color = PixelOperations.ToLinear(color);

            // TODO: This can be done with a matrix. But why can I not get conversion to work?
            Hsv hsv = color;
            return new Hsv(hsv.H, saturation, hsv.V);

            //return PixelOperations.ToSrgb(newHsv);
        }
    }
}
